using System.Web.Mvc;

namespace XYHMember
{
    public class AuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.Session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Home/Login");
            }
            base.OnActionExecuting(filterContext);
        }
    }
}
