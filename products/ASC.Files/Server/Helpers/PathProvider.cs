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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;

using ASC.Common;
using ASC.Common.Web;
using ASC.Core.Common;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Resources;
using ASC.Security.Cryptography;
using ASC.Web.Core.Files;
using ASC.Web.Core.Utility.Skins;
using ASC.Web.Studio.Utility;

using File = ASC.Files.Core.File;

namespace ASC.Web.Files.Classes
{
    public class PathProvider
    {
        public static readonly string ProjectVirtualPath = "~/Products/Projects/TMDocs.aspx";

        public static readonly string TemplatePath = "/Products/Files/Templates/";

        public static readonly string StartURL = FilesLinkUtility.FilesBaseVirtualPath;

        public readonly string GetFileServicePath;

        public WebImageSupplier WebImageSupplier { get; }
        public IDaoFactory DaoFactory { get; }
        public CommonLinkUtility CommonLinkUtility { get; }
        public FilesLinkUtility FilesLinkUtility { get; }
        public EmailValidationKeyProvider EmailValidationKeyProvider { get; }
        public GlobalStore GlobalStore { get; }
        public BaseCommonLinkUtility BaseCommonLinkUtility { get; }

        public PathProvider(
            WebImageSupplier webImageSupplier,
            IDaoFactory daoFactory,
            CommonLinkUtility commonLinkUtility,
            FilesLinkUtility filesLinkUtility,
            EmailValidationKeyProvider emailValidationKeyProvider,
            GlobalStore globalStore,
            BaseCommonLinkUtility baseCommonLinkUtility)
        {
            WebImageSupplier = webImageSupplier;
            DaoFactory = daoFactory;
            CommonLinkUtility = commonLinkUtility;
            FilesLinkUtility = filesLinkUtility;
            EmailValidationKeyProvider = emailValidationKeyProvider;
            GlobalStore = globalStore;
            BaseCommonLinkUtility = baseCommonLinkUtility;
            GetFileServicePath = BaseCommonLinkUtility.ToAbsolute("~/Products/Files/Services/WCFService/service.svc/");
        }

        public string GetImagePath(string imgFileName)
        {
            return WebImageSupplier.GetAbsoluteWebPath(imgFileName, Configuration.ProductEntryPoint.ID);
        }

        public string GetFileStaticRelativePath(string fileName)
        {
            var ext = FileUtility.GetFileExtension(fileName);
            switch (ext)
            {
                case ".js": //Attention: Only for ResourceBundleControl
                    return VirtualPathUtility.ToAbsolute("~/Products/Files/js/" + fileName);
                case ".ascx":
                    return BaseCommonLinkUtility.ToAbsolute("~/Products/Files/Controls/" + fileName);
                case ".css": //Attention: Only for ResourceBundleControl
                    return VirtualPathUtility.ToAbsolute("~/Products/Files/App_Themes/default/" + fileName);
            }

            return fileName;
        }

        public string GetFileControlPath(string fileName)
        {
            return BaseCommonLinkUtility.ToAbsolute("~/Products/Files/Controls/" + fileName);
        }

        public string GetFolderUrl(Folder folder, int projectID = 0)
        {
            if (folder == null) throw new ArgumentNullException("folder", FilesCommonResource.ErrorMassage_FolderNotFound);

            var folderDao = DaoFactory.FolderDao;

            switch (folder.RootFolderType)
            {
                case FolderType.BUNCH:
                    if (projectID == 0)
                    {
                        var path = folderDao.GetBunchObjectID(folder.RootFolderId);

                        var projectIDFromDao = path.Split('/').Last();

                        if (string.IsNullOrEmpty(projectIDFromDao)) return string.Empty;

                        projectID = Convert.ToInt32(projectIDFromDao);
                    }
                    return CommonLinkUtility.GetFullAbsolutePath(string.Format("{0}?prjid={1}#{2}", ProjectVirtualPath, projectID, folder.ID));
                default:
                    return CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.FilesBaseAbsolutePath + "#" + HttpUtility.UrlPathEncode(folder.ID.ToString()));
            }
        }

        public string GetFolderUrl(object folderId)
        {
            var folder = DaoFactory.FolderDao.GetFolder(folderId);

            return GetFolderUrl(folder);
        }

        public string GetFileStreamUrl(File file, string doc = null, bool lastVersion = false)
        {
            if (file == null) throw new ArgumentNullException("file", FilesCommonResource.ErrorMassage_FileNotFound);

            //NOTE: Always build path to handler!
            var uriBuilder = new UriBuilder(CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.FileHandlerPath));
            var query = uriBuilder.Query;
            query += FilesLinkUtility.Action + "=stream&";
            query += FilesLinkUtility.FileId + "=" + HttpUtility.UrlEncode(file.ID.ToString()) + "&";
            var version = 0;
            if (!lastVersion)
            {
                version = file.Version;
                query += FilesLinkUtility.Version + "=" + file.Version + "&";
            }
            query += FilesLinkUtility.AuthKey + "=" + EmailValidationKeyProvider.GetEmailKey(file.ID.ToString() + version);
            if (!string.IsNullOrEmpty(doc))
            {
                query += "&" + FilesLinkUtility.DocShareKey + "=" + HttpUtility.UrlEncode(doc);
            }

            return uriBuilder.Uri + "?" + query;
        }

        public string GetFileChangesUrl(File file, string doc = null)
        {
            if (file == null) throw new ArgumentNullException("file", FilesCommonResource.ErrorMassage_FileNotFound);

            var uriBuilder = new UriBuilder(CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.FileHandlerPath));
            var query = uriBuilder.Query;
            query += $"{FilesLinkUtility.Action}=diff&";
            query += $"{FilesLinkUtility.FileId}={HttpUtility.UrlEncode(file.ID.ToString())}&";
            query += $"{FilesLinkUtility.Version}={file.Version}&";
            query += $"{FilesLinkUtility.AuthKey}={EmailValidationKeyProvider.GetEmailKey(file.ID + file.Version.ToString(CultureInfo.InvariantCulture))}";
            if (!string.IsNullOrEmpty(doc))
            {
                query += $"&{FilesLinkUtility.DocShareKey}={HttpUtility.UrlEncode(doc)}";
            }

            return $"{uriBuilder.Uri}?{query}";
        }

        public string GetTempUrl(Stream stream, string ext)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            var store = GlobalStore.GetStore();
            var fileName = string.Format("{0}{1}", Guid.NewGuid(), ext);
            var path = Path.Combine("temp_stream", fileName);

            store.Save(
                FileConstant.StorageDomainTmp,
                path,
                stream,
                MimeMapping.GetMimeMapping(ext),
                "attachment; filename=\"" + fileName + "\"");

            var uriBuilder = new UriBuilder(CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.FileHandlerPath));
            var query = uriBuilder.Query;
            query += $"{FilesLinkUtility.Action}=tmp&";
            query += $"{FilesLinkUtility.FileTitle}={HttpUtility.UrlEncode(fileName)}&";
            query += $"{FilesLinkUtility.AuthKey}={EmailValidationKeyProvider.GetEmailKey(fileName)}";

            return $"{uriBuilder.Uri}?{query}";
        }

        public string GetEmptyFileUrl(string extension)
        {
            var uriBuilder = new UriBuilder(CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.FileHandlerPath));
            var query = uriBuilder.Query;
            query += $"{FilesLinkUtility.Action}=empty&";
            query += $"{FilesLinkUtility.FileTitle}={HttpUtility.UrlEncode(extension)}";

            return $"{uriBuilder.Uri}?{query}";
        }
    }

    public static class PathProviderExtention
    {
        public static DIHelper AddPathProviderService(this DIHelper services)
        {
            services.TryAddScoped<PathProvider>();

            return services
                .AddWebImageSupplierService()
                .AddCommonLinkUtilityService()
                .AddEmailValidationKeyProviderService()
                .AddGlobalStoreService()
                .AddBaseCommonLinkUtilityService()
                .AddFilesLinkUtilityService()
                .AddDaoFactoryService();
        }
    }
}