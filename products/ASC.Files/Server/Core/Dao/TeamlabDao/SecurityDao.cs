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
using System.Linq;
using System.Linq.Expressions;

using ASC.Common;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Core.Common.Settings;
using ASC.Core.Tenants;
using ASC.Files.Core.EF;
using ASC.Files.Core.Security;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.UserControls.Statistics;
using ASC.Web.Studio.Utility;

namespace ASC.Files.Core.Data
{
    internal class SecurityDao : AbstractDao, ISecurityDao
    {
        public SecurityDao(UserManager userManager,
            DbContextManager<FilesDbContext> dbContextManager,
            TenantManager tenantManager,
            TenantUtil tenantUtil,
            SetupInfo setupInfo,
            TenantExtra tenantExtra,
            TenantStatisticsProvider tenantStatisticProvider,
            CoreBaseSettings coreBaseSettings,
            CoreConfiguration coreConfiguration,
            SettingsManager settingsManager,
            AuthContext authContext,
            IServiceProvider serviceProvider)
            : base(dbContextManager,
                  userManager,
                  tenantManager,
                  tenantUtil,
                  setupInfo,
                  tenantExtra,
                  tenantStatisticProvider,
                  coreBaseSettings,
                  coreConfiguration,
                  settingsManager,
                  authContext,
                  serviceProvider)
        {
        }

        public void DeleteShareRecords(IEnumerable<FileShareRecord> records)
        {
            using var tx = FilesDbContext.Database.BeginTransaction();

            foreach (var record in records)
            {
                var query = FilesDbContext.Security
                    .Where(r => r.TenantId == record.Tenant)
                    .Where(r => r.EntryId == MappingID(record.EntryId).ToString())
                    .Where(r => r.EntryType == record.EntryType)
                    .Where(r => r.Subject == record.Subject);

                FilesDbContext.RemoveRange(query);
            }

            tx.Commit();
        }

        public bool IsShared(object entryId, FileEntryType type)
        {
            return Query(FilesDbContext.Security)
                .Where(r => r.EntryId == MappingID(entryId).ToString())
                .Where(r => r.EntryType == type)
                .Any();
        }

        public void SetShare(FileShareRecord r)
        {
            if (r.Share == FileShare.None)
            {
                var entryId = (MappingID(r.EntryId) ?? "").ToString();
                if (string.IsNullOrEmpty(entryId)) return;

                using (var tx = FilesDbContext.Database.BeginTransaction())
                {
                    var files = new List<string>();

                    if (r.EntryType == FileEntryType.Folder)
                    {
                        var folders = new List<string>();
                        if (int.TryParse(entryId, out var intEntryId))
                        {
                            var foldersInt = FilesDbContext.Tree
                                .Where(r => r.ParentId.ToString() == entryId)
                                .Select(r => r.FolderId)
                                .ToList();

                            folders.AddRange(foldersInt.Select(folderInt => folderInt.ToString()));
                            files.AddRange(Query(FilesDbContext.Files).Where(r => foldersInt.Any(a => a == r.FolderId)).Select(r => r.Id.ToString()));
                        }
                        else
                        {
                            folders.Add(entryId);
                        }

                        var toDelete = FilesDbContext.Security
                            .Where(a => a.TenantId == r.Tenant)
                            .Where(a => folders.Any(b => b == a.EntryId))
                            .Where(a => a.EntryType == FileEntryType.Folder)
                            .Where(a => a.Subject == r.Subject);

                        FilesDbContext.Security.RemoveRange(toDelete);
                        FilesDbContext.SaveChanges();

                    }
                    else
                    {
                        files.Add(entryId);
                    }

                    if (0 < files.Count)
                    {
                        var toDelete = FilesDbContext.Security
                            .Where(a => a.TenantId == r.Tenant)
                            .Where(a => files.Any(b => b == a.EntryId))
                            .Where(a => a.EntryType == FileEntryType.File)
                            .Where(a => a.Subject == r.Subject);

                        FilesDbContext.Security.RemoveRange(toDelete);
                        FilesDbContext.SaveChanges();
                    }

                    tx.Commit();
                }
            }
            else
            {
                var toInsert = new DbFilesSecurity
                {
                    TenantId = r.Tenant,
                    EntryId = MappingID(r.EntryId, true).ToString(),
                    EntryType = r.EntryType,
                    Subject = r.Subject,
                    Owner = r.Owner,
                    Security = r.Share,
                    TimeStamp = DateTime.UtcNow
                };

                FilesDbContext.AddOrUpdate(r => r.Security, toInsert);
                FilesDbContext.SaveChanges();
            }
        }

        public IEnumerable<FileShareRecord> GetShares(IEnumerable<Guid> subjects)
        {
            var q = GetQuery(r => subjects.Any(a => r.Subject == a));
            return FromQuery(q);
        }

        public IEnumerable<FileShareRecord> GetPureShareRecords(IEnumerable<FileEntry> entries)
        {
            if (entries == null) return new List<FileShareRecord>();

            var files = new List<string>();
            var folders = new List<string>();

            foreach (var entry in entries)
            {
                SelectFilesAndFoldersForShare(entry, files, folders, null);
            }

            return GetPureShareRecordsDb(files, folders);
        }

        public IEnumerable<FileShareRecord> GetPureShareRecords(FileEntry entry)
        {
            if (entry == null) return new List<FileShareRecord>();

            var files = new List<string>();
            var folders = new List<string>();

            SelectFilesAndFoldersForShare(entry, files, folders, null);

            return GetPureShareRecordsDb(files, folders);
        }

        private IEnumerable<FileShareRecord> GetPureShareRecordsDb(List<string> files, List<string> folders)
        {
            var result = new List<FileShareRecord>();

            var q = GetQuery(r => folders.Any(a => a == r.EntryId) && r.EntryType == FileEntryType.Folder);

            if (files.Any())
            {
                q = q.Union(GetQuery(r => files.Any(q => q == r.EntryId) && r.EntryType == FileEntryType.File));
            }

            result.AddRange(FromQuery(q));

            return result;
        }

        /// <summary>
        /// Get file share records with hierarchy.
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        public IEnumerable<FileShareRecord> GetShares(IEnumerable<FileEntry> entries)
        {
            if (entries == null) return new List<FileShareRecord>();

            var files = new List<string>();
            var foldersInt = new List<int>();

            foreach (var entry in entries)
            {
                SelectFilesAndFoldersForShare(entry, files, null, foldersInt);
            }

            return SaveFilesAndFoldersForShare(files, foldersInt);
        }

        /// <summary>
        /// Get file share records with hierarchy.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public IEnumerable<FileShareRecord> GetShares(FileEntry entry)
        {
            if (entry == null) return new List<FileShareRecord>();

            var files = new List<string>();
            var foldersInt = new List<int>();

            SelectFilesAndFoldersForShare(entry, files, null, foldersInt);
            return SaveFilesAndFoldersForShare(files, foldersInt);
        }

        private void SelectFilesAndFoldersForShare(FileEntry entry, ICollection<string> files, ICollection<string> folders, ICollection<int> foldersInt)
        {
            object folderId;
            if (entry.FileEntryType == FileEntryType.File)
            {
                var fileId = MappingID(entry.ID);
                folderId = ((File)entry).FolderID;
                if (!files.Contains(fileId.ToString())) files.Add(fileId.ToString());
            }
            else
            {
                folderId = entry.ID;
            }

            if (foldersInt != null && int.TryParse(folderId.ToString(), out var folderIdInt) && !foldersInt.Contains(folderIdInt)) foldersInt.Add(folderIdInt);

            if (folders != null) folders.Add(MappingID(folderId).ToString());
        }

        private IEnumerable<FileShareRecord> SaveFilesAndFoldersForShare(List<string> files, List<int> folders)
        {
            var q = Query(FilesDbContext.Security)
                .Join(FilesDbContext.Tree, r => r.EntryId, a => a.ParentId.ToString(), (security, tree) => new SecurityTreeRecord { DbFilesSecurity = security, DbFolderTree = tree })
                .Where(r => folders.Any(f => f == r.DbFolderTree.FolderId))
                .Where(r => r.DbFilesSecurity.EntryType == FileEntryType.Folder)
                .ToList();

            if (0 < files.Count)
            {
                var q1 = GetQuery(r => files.Any(f => f == r.EntryId) && r.EntryType == FileEntryType.File)
                    .Select(r => new SecurityTreeRecord { DbFilesSecurity = r })
                    .ToList();
                q = q.Union(q1).ToList();
            }

            return q.Select(ToFileShareRecord)
                .OrderBy(r => r.Level)
                .ThenByDescending(r => r.Share, new FileShareRecord.ShareComparer())
                .ToList();
        }

        public void RemoveSubject(Guid subject)
        {
            using var tr = FilesDbContext.Database.BeginTransaction();

            var toDelete1 = FilesDbContext.Security.Where(r => r.Subject == subject);
            var toDelete2 = FilesDbContext.Security.Where(r => r.Owner == subject);

            FilesDbContext.RemoveRange(toDelete1);
            FilesDbContext.SaveChanges();

            FilesDbContext.RemoveRange(toDelete2);
            FilesDbContext.SaveChanges();

            tr.Commit();
        }

        private IQueryable<DbFilesSecurity> GetQuery(Expression<Func<DbFilesSecurity, bool>> where = null)
        {
            var q = Query(FilesDbContext.Security);
            if (q != null)
            {
                q = q.Where(where);
            }
            return q;
        }

        protected List<FileShareRecord> FromQuery(IQueryable<DbFilesSecurity> filesSecurities)
        {
            return filesSecurities
                .ToList()
                .Select(ToFileShareRecord)
                .ToList();
        }

        protected List<FileShareRecord> FromQuery(IQueryable<SecurityTreeRecord> filesSecurities)
        {
            return filesSecurities
                .ToList()
                .Select(ToFileShareRecord)
                .ToList();
        }

        private FileShareRecord ToFileShareRecord(DbFilesSecurity r) => new FileShareRecord
        {
            Tenant = r.TenantId,
            EntryId = MappingID(r.EntryId),
            EntryType = r.EntryType,
            Subject = r.Subject,
            Owner = r.Owner,
            Share = r.Security
        };
        private FileShareRecord ToFileShareRecord(SecurityTreeRecord r)
        {
            var result = ToFileShareRecord(r.DbFilesSecurity);
            result.Level = r.DbFolderTree?.Level ?? -1;
            return result;
        }
    }

    internal class SecurityTreeRecord
    {
        public DbFilesSecurity DbFilesSecurity { get; set; }
        public DbFolderTree DbFolderTree { get; set; }
    }

    public static class SecurityDaoExtention
    {
        public static DIHelper AddSecurityDaoService(this DIHelper services)
        {
            services.TryAddScoped<ISecurityDao, SecurityDao>();

            return services
                .AddUserManagerService()
                .AddFilesDbContextService()
                .AddTenantManagerService()
                .AddTenantUtilService()
                .AddSetupInfo()
                .AddTenantExtraService()
                .AddTenantStatisticsProviderService()
                .AddCoreBaseSettingsService()
                .AddCoreConfigurationService()
                .AddSettingsManagerService()
                .AddAuthContextService();
        }
    }
}