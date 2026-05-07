using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace XYHMember
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
           name: "getUserByName",
           url: "User/GetUserByName",
           defaults: new { controller = "User", action = "GetUserByName" }
       );
//            routes.MapRoute(
//name: "GetMemberByMZHMPay",
//url: "Member/GetMemberByMZHMPay",
//defaults: new { controller = "Member", action = "GetMemberByMZHMPay" }
//);

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Login", id = UrlParameter.Optional }
            );
        }
    }
}
