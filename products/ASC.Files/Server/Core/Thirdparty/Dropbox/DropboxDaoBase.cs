/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Core.Tenants;
using ASC.Files.Core;
using ASC.Files.Core.EF;
using ASC.Files.Core.Security;
using ASC.Security.Cryptography;
using ASC.Web.Files.Classes;
using ASC.Web.Studio.Core;

using Dropbox.Api.Files;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ASC.Files.Thirdparty.Dropbox
{
    internal abstract class DropboxDaoBase
    {
        protected DropboxDaoSelector DropboxDaoSelector { get; set; }

        public int TenantID { get; private set; }
        public DropboxProviderInfo DropboxProviderInfo { get; private set; }
        public string PathPrefix { get; private set; }
        public IServiceProvider ServiceProvider { get; }
        public UserManager UserManager { get; }
        public TenantUtil TenantUtil { get; }
        public SetupInfo SetupInfo { get; }
        public FilesDbContext FilesDbContext { get; }

        public DropboxDaoBase(
            IServiceProvider serviceProvider,
            UserManager userManager,
            TenantManager tenantManager,
            TenantUtil tenantUtil,
            DbContextManager<FilesDbContext> dbContextManager,
            SetupInfo setupInfo)
        {
            ServiceProvider = serviceProvider;
            UserManager = userManager;
            TenantUtil = tenantUtil;
            SetupInfo = setupInfo;
            TenantID = tenantManager.GetCurrentTenant().TenantId;
            FilesDbContext = dbContextManager.Get(FileConstant.DatabaseId);
        }

        public void Init(DropboxDaoSelector.DropboxInfo dropboxInfo, DropboxDaoSelector dropboxDaoSelector)
        {
            DropboxProviderInfo = dropboxInfo.DropboxProviderInfo;
            PathPrefix = dropboxInfo.PathPrefix;
            DropboxDaoSelector = dropboxDaoSelector;
        }

        public void Dispose()
        {
            DropboxProviderInfo.Dispose();
        }

        protected string MappingID(string id, bool saveIfNotExist = false)
        {
            if (id == null) return null;

            string result;
            if (id.StartsWith("dropbox"))
            {
                result = Regex.Replace(BitConverter.ToString(Hasher.Hash(id, HashAlg.MD5)), "-", "").ToLower();
            }
            else
            {
                result = FilesDbContext.ThirdpartyIdMapping
                    .Where(r => r.HashId == id)
                    .Select(r => r.Id)
                    .FirstOrDefault();
            }
            if (saveIfNotExist)
            {
                var newMapping = new DbFilesThirdpartyIdMapping
                {
                    Id = id,
                    HashId = result,
                    TenantId = TenantID
                };

                FilesDbContext.ThirdpartyIdMapping.Add(newMapping);
                FilesDbContext.SaveChanges();
            }
            return result;
        }

        protected IQueryable<T> Query<T>(DbSet<T> set) where T : class, IDbFile
        {
            return set.Where(r => r.TenantId == TenantID);
        }


        protected static string GetParentFolderPath(Metadata dropboxItem)
        {
            if (dropboxItem == null || IsRoot(dropboxItem.AsFolder))
                return null;

            var pathLength = dropboxItem.PathDisplay.Length - dropboxItem.Name.Length;
            return dropboxItem.PathDisplay.Substring(0, pathLength > 1 ? pathLength - 1 : 0);
        }

        protected static string MakeDropboxPath(object entryId)
        {
            return Convert.ToString(entryId, CultureInfo.InvariantCulture);
        }

        protected string MakeDropboxPath(Metadata dropboxItem)
        {
            string path = null;
            if (dropboxItem != null)
            {
                path = dropboxItem.PathDisplay;
            }

            return path;
        }

        protected string MakeId(Metadata dropboxItem)
        {
            return MakeId(MakeDropboxPath(dropboxItem));
        }

        protected string MakeId(string path = null)
        {
            return string.Format("{0}{1}", PathPrefix, string.IsNullOrEmpty(path) || path == "/" ? "" : ("-" + path.Replace('/', '|')));
        }

        protected string MakeFolderTitle(FolderMetadata dropboxFolder)
        {
            if (dropboxFolder == null || IsRoot(dropboxFolder))
            {
                return DropboxProviderInfo.CustomerTitle;
            }

            return Global.ReplaceInvalidCharsAndTruncate(dropboxFolder.Name);
        }

        protected string MakeFileTitle(FileMetadata dropboxFile)
        {
            if (dropboxFile == null || string.IsNullOrEmpty(dropboxFile.Name))
            {
                return DropboxProviderInfo.ProviderKey;
            }

            return Global.ReplaceInvalidCharsAndTruncate(dropboxFile.Name);
        }

        protected Folder<string> ToFolder(FolderMetadata dropboxFolder)
        {
            if (dropboxFolder == null) return null;
            if (dropboxFolder is ErrorFolder)
            {
                //Return error entry
                return ToErrorFolder(dropboxFolder as ErrorFolder);
            }

            var isRoot = IsRoot(dropboxFolder);

            var folder = ServiceProvider.GetService<Folder<string>>();

            folder.ID = MakeId(dropboxFolder);
            folder.ParentFolderID = isRoot ? null : MakeId(GetParentFolderPath(dropboxFolder));
            folder.CreateBy = DropboxProviderInfo.Owner;
            folder.CreateOn = isRoot ? DropboxProviderInfo.CreateOn : default;
            folder.FolderType = FolderType.DEFAULT;
            folder.ModifiedBy = DropboxProviderInfo.Owner;
            folder.ModifiedOn = isRoot ? DropboxProviderInfo.CreateOn : default;
            folder.ProviderId = DropboxProviderInfo.ID;
            folder.ProviderKey = DropboxProviderInfo.ProviderKey;
            folder.RootFolderCreator = DropboxProviderInfo.Owner;
            folder.RootFolderId = MakeId();
            folder.RootFolderType = DropboxProviderInfo.RootFolderType;
            folder.Shareable = false;
            folder.Title = MakeFolderTitle(dropboxFolder);
            folder.TotalFiles = 0;
            folder.TotalSubFolders = 0;

            if (folder.CreateOn != DateTime.MinValue && folder.CreateOn.Kind == DateTimeKind.Utc)
                folder.CreateOn = TenantUtil.DateTimeFromUtc(folder.CreateOn);

            if (folder.ModifiedOn != DateTime.MinValue && folder.ModifiedOn.Kind == DateTimeKind.Utc)
                folder.ModifiedOn = TenantUtil.DateTimeFromUtc(folder.ModifiedOn);

            return folder;
        }

        protected static bool IsRoot(FolderMetadata dropboxFolder)
        {
            return dropboxFolder != null && dropboxFolder.Id == "/";
        }

        private File<string> ToErrorFile(ErrorFile dropboxFile)
        {
            if (dropboxFile == null) return null;
            var file = ServiceProvider.GetService<File<string>>();
            file.ID = MakeId(dropboxFile.ErrorId);
            file.CreateBy = DropboxProviderInfo.Owner;
            file.CreateOn = TenantUtil.DateTimeNow();
            file.ModifiedBy = DropboxProviderInfo.Owner;
            file.ModifiedOn = TenantUtil.DateTimeNow();
            file.ProviderId = DropboxProviderInfo.ID;
            file.ProviderKey = DropboxProviderInfo.ProviderKey;
            file.RootFolderCreator = DropboxProviderInfo.Owner;
            file.RootFolderId = MakeId();
            file.RootFolderType = DropboxProviderInfo.RootFolderType;
            file.Title = MakeFileTitle(dropboxFile);
            file.Error = dropboxFile.Error;

            return file;
        }

        private Folder<string> ToErrorFolder(ErrorFolder dropboxFolder)
        {
            if (dropboxFolder == null) return null;
            var folder = ServiceProvider.GetService<Folder<string>>();

            folder.ID = MakeId(dropboxFolder.ErrorId);
            folder.ParentFolderID = null;
            folder.CreateBy = DropboxProviderInfo.Owner;
            folder.CreateOn = TenantUtil.DateTimeNow();
            folder.FolderType = FolderType.DEFAULT;
            folder.ModifiedBy = DropboxProviderInfo.Owner;
            folder.ModifiedOn = TenantUtil.DateTimeNow();
            folder.ProviderId = DropboxProviderInfo.ID;
            folder.ProviderKey = DropboxProviderInfo.ProviderKey;
            folder.RootFolderCreator = DropboxProviderInfo.Owner;
            folder.RootFolderId = MakeId();
            folder.RootFolderType = DropboxProviderInfo.RootFolderType;
            folder.Shareable = false;
            folder.Title = MakeFolderTitle(dropboxFolder);
            folder.TotalFiles = 0;
            folder.TotalSubFolders = 0;
            folder.Error = dropboxFolder.Error;

            return folder;
        }

        public File<string> ToFile(FileMetadata dropboxFile)
        {
            if (dropboxFile == null) return null;

            if (dropboxFile is ErrorFile)
            {
                //Return error entry
                return ToErrorFile(dropboxFile as ErrorFile);
            }

            var file = ServiceProvider.GetService<File<string>>();

            file.ID = MakeId(dropboxFile);
            file.Access = FileShare.None;
            file.ContentLength = (long)dropboxFile.Size;
            file.CreateBy = DropboxProviderInfo.Owner;
            file.CreateOn = TenantUtil.DateTimeFromUtc(dropboxFile.ServerModified);
            file.FileStatus = FileStatus.None;
            file.FolderID = MakeId(GetParentFolderPath(dropboxFile));
            file.ModifiedBy = DropboxProviderInfo.Owner;
            file.ModifiedOn = TenantUtil.DateTimeFromUtc(dropboxFile.ServerModified);
            file.NativeAccessor = dropboxFile;
            file.ProviderId = DropboxProviderInfo.ID;
            file.ProviderKey = DropboxProviderInfo.ProviderKey;
            file.Title = MakeFileTitle(dropboxFile);
            file.RootFolderId = MakeId();
            file.RootFolderType = DropboxProviderInfo.RootFolderType;
            file.RootFolderCreator = DropboxProviderInfo.Owner;
            file.Shared = false;
            file.Version = 1;

            return file;
        }

        public Folder<string> GetRootFolder(string folderId)
        {
            return ToFolder(GetDropboxFolder(string.Empty));
        }

        protected FolderMetadata GetDropboxFolder(string folderId)
        {
            var dropboxFolderPath = MakeDropboxPath(folderId);
            try
            {
                var folder = DropboxProviderInfo.GetDropboxFolder(dropboxFolderPath);
                return folder;
            }
            catch (Exception ex)
            {
                return new ErrorFolder(ex, dropboxFolderPath);
            }
        }

        protected FileMetadata GetDropboxFile(object fileId)
        {
            var dropboxFilePath = MakeDropboxPath(fileId);
            try
            {
                var file = DropboxProviderInfo.GetDropboxFile(dropboxFilePath);
                return file;
            }
            catch (Exception ex)
            {
                return new ErrorFile(ex, dropboxFilePath);
            }
        }

        protected IEnumerable<string> GetChildren(object folderId)
        {
            return GetDropboxItems(folderId).Select(MakeId);
        }

        protected List<Metadata> GetDropboxItems(object parentId, bool? folder = null)
        {
            var dropboxFolderPath = MakeDropboxPath(parentId);
            var items = DropboxProviderInfo.GetDropboxItems(dropboxFolderPath);

            if (folder.HasValue)
            {
                if (folder.Value)
                {
                    return items.Where(i => i.AsFolder != null).ToList();
                }

                return items.Where(i => i.AsFile != null).ToList();
            }

            return items;
        }

        protected sealed class ErrorFolder : FolderMetadata
        {
            public string Error { get; set; }

            public string ErrorId { get; private set; }


            public ErrorFolder(Exception e, object id)
            {
                ErrorId = id.ToString();
                if (e != null)
                {
                    Error = e.Message;
                }
            }
        }

        protected sealed class ErrorFile : FileMetadata
        {
            public string Error { get; set; }

            public string ErrorId { get; private set; }


            public ErrorFile(Exception e, object id)
            {
                ErrorId = id.ToString();
                if (e != null)
                {
                    Error = e.Message;
                }
            }
        }

        protected string GetAvailableTitle(string requestTitle, string parentFolderPath, Func<string, string, bool> isExist)
        {
            if (!isExist(requestTitle, parentFolderPath)) return requestTitle;

            var re = new Regex(@"( \(((?<index>[0-9])+)\)(\.[^\.]*)?)$");
            var match = re.Match(requestTitle);

            if (!match.Success)
            {
                var insertIndex = requestTitle.Length;
                if (requestTitle.LastIndexOf(".", StringComparison.InvariantCulture) != -1)
                {
                    insertIndex = requestTitle.LastIndexOf(".", StringComparison.InvariantCulture);
                }
                requestTitle = requestTitle.Insert(insertIndex, " (1)");
            }

            while (isExist(requestTitle, parentFolderPath))
            {
                requestTitle = re.Replace(requestTitle, MatchEvaluator);
            }
            return requestTitle;
        }

        private static string MatchEvaluator(Match match)
        {
            var index = Convert.ToInt32(match.Groups[2].Value);
            var staticText = match.Value.Substring(string.Format(" ({0})", index).Length);
            return string.Format(" ({0}){1}", index + 1, staticText);
        }
    }
}