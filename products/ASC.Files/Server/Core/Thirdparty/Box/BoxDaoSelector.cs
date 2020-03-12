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
using System.Text.RegularExpressions;

using ASC.Common;
using ASC.Files.Core;
using ASC.Files.Core.Security;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.Files.Thirdparty.Box
{
    internal class BoxDaoSelector : RegexDaoSelectorBase<string>
    {
        public IServiceProvider ServiceProvider { get; }
        public IDaoFactory DaoFactory { get; }

        internal class BoxInfo
        {
            public BoxProviderInfo BoxProviderInfo { get; set; }

            public string Path { get; set; }
            public string PathPrefix { get; set; }
        }

        public BoxDaoSelector(IServiceProvider serviceProvider, IDaoFactory daoFactory)
            : base(new Regex(@"^box-(?'id'\d+)(-(?'path'.*)){0,1}$", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            ServiceProvider = serviceProvider;
            DaoFactory = daoFactory;
        }

        public override IFileDao<string> GetFileDao(string id)
        {
            var res = ServiceProvider.GetService<BoxFileDao>();

            res.Init(GetInfo(id), this);

            return res;
        }

        public override IFolderDao<string> GetFolderDao(string id)
        {
            var res = ServiceProvider.GetService<BoxFolderDao>();

            res.Init(GetInfo(id), this);

            return res;
        }

        public override ITagDao<string> GetTagDao(string id)
        {
            var res = ServiceProvider.GetService<BoxTagDao>();

            res.Init(GetInfo(id), this);

            return res;
        }

        public override ISecurityDao<string> GetSecurityDao(string id)
        {
            var res = ServiceProvider.GetService<BoxSecurityDao>();

            res.Init(GetInfo(id), this);

            return res;
        }

        public override string ConvertId(string id)
        {
            if (id != null)
            {
                var match = Selector.Match(id);
                if (match.Success)
                {
                    return match.Groups["path"].Value.Replace('|', '/');
                }
                throw new ArgumentException("Id is not a Box id");
            }
            return base.ConvertId(null);
        }

        private BoxInfo GetInfo(string objectId)
        {
            if (objectId == null) throw new ArgumentNullException("objectId");
            var id = objectId;
            var match = Selector.Match(id);
            if (match.Success)
            {
                var providerInfo = GetProviderInfo(Convert.ToInt32(match.Groups["id"].Value));

                return new BoxInfo
                {
                    Path = match.Groups["path"].Value,
                    BoxProviderInfo = providerInfo,
                    PathPrefix = "box-" + match.Groups["id"].Value
                };
            }
            throw new ArgumentException("Id is not a Box id");
        }

        public override string GetIdCode(string id)
        {
            if (id != null)
            {
                var match = Selector.Match(id);
                if (match.Success)
                {
                    return match.Groups["id"].Value;
                }
            }
            return base.GetIdCode(id);
        }

        private BoxProviderInfo GetProviderInfo(int linkId)
        {
            BoxProviderInfo info;

            var dbDao = DaoFactory.ProviderDao;
            try
            {
                info = (BoxProviderInfo)dbDao.GetProviderInfo(linkId);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException("Provider id not found or you have no access");
            }
            return info;
        }

        public void RenameProvider(BoxProviderInfo boxProviderInfo, string newTitle)
        {
            var dbDao = ServiceProvider.GetService<CachedProviderAccountDao>();
            dbDao.UpdateProviderInfo(boxProviderInfo.ID, newTitle, null, boxProviderInfo.RootFolderType);
            boxProviderInfo.UpdateTitle(newTitle); //This will update cached version too
        }
    }
    public static class BoxDaoSelectorExtention
    {
        public static DIHelper AddBoxDaoSelectorService(this DIHelper services)
        {
            services.TryAddScoped<BoxDaoSelector>();

            return services
                .AddBoxSecurityDaoService()
                .AddBoxTagDaoService()
                .AddBoxFolderDaoService()
                .AddBoxFileDaoService();
        }
    }
}