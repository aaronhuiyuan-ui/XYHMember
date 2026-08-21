using System.Web.Mvc;

namespace XYHMember
{
    public class AuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.Session["UserId"] == null)
            {
                // 异步请求（fetch / jQuery ajax）返回 401，由页面注入的 sessionGuard.js 统一整页跳登录；
                // 普通页面跳转仍走 302 到登录页（登录页脚本负责跳出 iframe）。
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new HttpStatusCodeResult(401, "登录已过期");
                }
                else
                {
                    filterContext.Result = new RedirectResult("~/Home/Login");
                }
            }
            base.OnActionExecuting(filterContext);
        }
    }
}
