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
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Common;
using ASC.Files.Resources;
using ASC.Web.Files.ThirdPartyApp;
using ASC.Web.Studio.Utility;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ASC.Web.Files.HttpHandlers
{
    public class ThirdPartyAppHandler //: IHttpHandler
    {
        public RequestDelegate Next { get; }
        public AuthContext AuthContext { get; }
        public BaseCommonLinkUtility BaseCommonLinkUtility { get; }
        public CommonLinkUtility CommonLinkUtility { get; }
        private ILog Log { get; set; }

        public string HandlerPath { get; set; }

        public ThirdPartyAppHandler(
            RequestDelegate next,
            IOptionsMonitor<ILog> optionsMonitor,
            AuthContext authContext,
            BaseCommonLinkUtility baseCommonLinkUtility,
            CommonLinkUtility commonLinkUtility)
        {
            Next = next;
            AuthContext = authContext;
            BaseCommonLinkUtility = baseCommonLinkUtility;
            CommonLinkUtility = commonLinkUtility;
            Log = optionsMonitor.CurrentValue;
            HandlerPath = baseCommonLinkUtility.ToAbsolute("~/thirdpartyapp");
        }

        public async Task Invoke(HttpContext context)
        {
            Log.Debug("ThirdPartyApp: handler request - " + context.Request.Url());

            var message = string.Empty;

            try
            {
                var app = ThirdPartySelector.GetApp(context.Request.Query[ThirdPartySelector.AppAttr]);
                Log.Debug("ThirdPartyApp: app - " + app);

                if (app.Request(context))
                {
                    await Next.Invoke(context);
                    return;
                }
            }
            catch (ThreadAbortException)
            {
                await Next.Invoke(context);
                //Thats is responce ending
                return;
            }
            catch (Exception e)
            {
                Log.Error("ThirdPartyApp", e);
                message = e.Message;
            }

            if (string.IsNullOrEmpty(message))
            {
                if ((context.Request.Query["error"].FirstOrDefault() ?? "").ToLower() == "access_denied")
                {
                    message = context.Request.Query["error_description"].FirstOrDefault() ?? FilesCommonResource.AppAccessDenied;
                }
            }

            var redirectUrl = CommonLinkUtility.GetDefault();
            if (!string.IsNullOrEmpty(message))
            {
                redirectUrl += AuthContext.IsAuthenticated ? "#error/" : "?m=";
                redirectUrl += HttpUtility.UrlEncode(message);
            }
            context.Response.Redirect(redirectUrl, true);
            await Next.Invoke(context);
        }
    }
}