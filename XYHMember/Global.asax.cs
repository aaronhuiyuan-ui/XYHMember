using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace XYHMember
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        /// <summary>
        /// 给每个 MVC 的 HTML 响应注入 sessionGuard.js，
        /// 实现“会话过期后异步请求统一整页跳登录”的全局拦截。
        /// </summary>
        protected void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context != null && context.CurrentHandler is MvcHandler && context.Response != null)
            {
                context.Response.Filter = SessionGuardHtmlFilter.Create(
                    context.Response.Filter,
                    context.Response.ContentEncoding);
            }
        }
    }
}
