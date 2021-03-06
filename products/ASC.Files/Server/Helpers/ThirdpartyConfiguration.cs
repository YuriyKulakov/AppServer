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

using ASC.Common;
using ASC.FederatedLogin.LoginProviders;
using ASC.Files.Core;
using ASC.Files.Core.Data;

using Microsoft.Extensions.Configuration;

namespace ASC.Web.Files.Helpers
{
    public class ThirdpartyConfiguration
    {
        public IConfiguration Configuration { get; }
        public IDaoFactory DaoFactory { get; }
        public BoxLoginProvider BoxLoginProvider { get; }
        public DropboxLoginProvider DropboxLoginProvider { get; }
        public OneDriveLoginProvider OneDriveLoginProvider { get; }
        public DocuSignLoginProvider DocuSignLoginProvider { get; }
        public GoogleLoginProvider GoogleLoginProvider { get; }

        public ThirdpartyConfiguration(
            IConfiguration configuration,
            IDaoFactory daoFactory,
            BoxLoginProvider boxLoginProvider,
            DropboxLoginProvider dropboxLoginProvider,
            OneDriveLoginProvider oneDriveLoginProvider,
            DocuSignLoginProvider docuSignLoginProvider,
            GoogleLoginProvider googleLoginProvider)
        {
            Configuration = configuration;
            DaoFactory = daoFactory;
            BoxLoginProvider = boxLoginProvider;
            DropboxLoginProvider = dropboxLoginProvider;
            OneDriveLoginProvider = oneDriveLoginProvider;
            DocuSignLoginProvider = docuSignLoginProvider;
            GoogleLoginProvider = googleLoginProvider;
        }

        public IEnumerable<string> ThirdPartyProviders
        {
            get { return (Configuration["files:thirdparty:enable"] ?? "").Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries); }
        }

        public bool SupportInclusion
        {
            get
            {
                var providerDao = DaoFactory.ProviderDao;
                if (providerDao == null) return false;

                return SupportBoxInclusion || SupportDropboxInclusion || SupportDocuSignInclusion || SupportGoogleDriveInclusion || SupportOneDriveInclusion || SupportSharePointInclusion || SupportWebDavInclusion || SupportNextcloudInclusion || SupportOwncloudInclusion || SupportYandexInclusion;
            }
        }

        public bool SupportBoxInclusion
        {
            get
            {
                return ThirdPartyProviders.Contains("box") && BoxLoginProvider.Instance.IsEnabled;
            }
        }

        public bool SupportDropboxInclusion
        {
            get
            {
                return ThirdPartyProviders.Contains("dropboxv2") && DropboxLoginProvider.Instance.IsEnabled;
            }
        }

        public bool SupportOneDriveInclusion
        {
            get
            {
                return ThirdPartyProviders.Contains("onedrive") && OneDriveLoginProvider.Instance.IsEnabled;
            }
        }

        public bool SupportSharePointInclusion
        {
            get { return ThirdPartyProviders.Contains("sharepoint"); }
        }

        public bool SupportWebDavInclusion
        {
            get { return ThirdPartyProviders.Contains("webdav"); }
        }

        public bool SupportNextcloudInclusion
        {
            get { return ThirdPartyProviders.Contains("nextcloud"); }
        }

        public bool SupportOwncloudInclusion
        {
            get { return ThirdPartyProviders.Contains("owncloud"); }
        }

        public bool SupportYandexInclusion
        {
            get { return ThirdPartyProviders.Contains("yandex"); }
        }

        public string DropboxAppKey
        {
            get { return DropboxLoginProvider.Instance["dropboxappkey"]; }
        }

        public string DropboxAppSecret
        {
            get { return DropboxLoginProvider.Instance["dropboxappsecret"]; }
        }

        public bool SupportDocuSignInclusion
        {
            get
            {
                return ThirdPartyProviders.Contains("docusign") && DocuSignLoginProvider.Instance.IsEnabled;
            }
        }

        public bool SupportGoogleDriveInclusion
        {
            get
            {
                return ThirdPartyProviders.Contains("google") && GoogleLoginProvider.Instance.IsEnabled;
            }
        }
    }
    public static class ThirdpartyConfigurationExtension
    {
        public static DIHelper AddThirdpartyConfigurationService(this DIHelper services)
        {
            services.TryAddScoped<ThirdpartyConfiguration>();
            return services
                .AddDaoFactoryService()
                .AddDocuSignLoginProviderService()
                .AddBoxLoginProviderService()
                .AddDropboxLoginProviderService()
                .AddOneDriveLoginProviderService()
                .AddGoogleLoginProviderService()
                ;
        }
    }
}