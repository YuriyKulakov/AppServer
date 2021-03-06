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
using System.Linq;

using ASC.Common;
using ASC.Common.Security.Authentication;
using ASC.Core.Caching;
using ASC.Core.Security.Authentication;
using ASC.Core.Tenants;
using ASC.Core.Users;

namespace ASC.Core
{
    public class AuthManager
    {
        private readonly IUserService userService;

        public UserManager UserManager { get; }
        public UserFormatter UserFormatter { get; }

        public AuthManager(IUserService service, UserManager userManager, UserFormatter userFormatter)
        {
            userService = service;
            UserManager = userManager;
            UserFormatter = userFormatter;
        }


        public IUserAccount[] GetUserAccounts(Tenant tenant)
        {
            return UserManager.GetUsers(EmployeeStatus.Active).Select(u => ToAccount(tenant.TenantId, u)).ToArray();
        }

        public void SetUserPassword(int tenantId, Guid userID, string password)
        {
            userService.SetUserPassword(tenantId, userID, password);
        }

        public string GetUserPasswordHash(int tenantId, Guid userID)
        {
            return userService.GetUserPassword(tenantId, userID);
        }

        public IAccount GetAccountByID(int tenantId, Guid id)
        {
            var s = ASC.Core.Configuration.Constants.SystemAccounts.FirstOrDefault(a => a.ID == id);
            if (s != null) return s;

            var u = UserManager.GetUsers(id);
            return !Constants.LostUser.Equals(u) && u.Status == EmployeeStatus.Active ? (IAccount)ToAccount(tenantId, u) : ASC.Core.Configuration.Constants.Guest;
        }


        private IUserAccount ToAccount(int tenantId, UserInfo u)
        {
            return new UserAccount(u, tenantId, UserFormatter);
        }
    }
    public static class AuthManagerExtension
    {
        public static DIHelper AddAuthManager(this DIHelper services)
        {
            services.TryAddScoped<AuthManager>();
            return services
                .AddUserService()
                .AddUserFormatter()
                .AddUserManagerService();
        }
    }
}