using System.Web;
using System.Web.Optimization;

namespace XYHMember
{
    public class BundleConfig
    {
        // 有关捆绑的详细信息，请访问 https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/Scripts/jquery").Include(
                        "~/Scripts/jquery-1.11.3.min.js"));

            bundles.Add(new ScriptBundle("~/Scripts/jquery10").Include(
                        "~/Scripts/jquery-1.10.2.min.js"));

            bundles.Add(new ScriptBundle("~/Scripts/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // 使用要用于开发和学习的 Modernizr 的开发版本。然后，当你做好
            // 生产准备就绪，请使用 https://modernizr.com 上的生成工具仅选择所需的测试。
            bundles.Add(new ScriptBundle("~/Scripts/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/Scripts/layui").Include(
                      "~/Scripts/layui.js"
                      ));

            bundles.Add(new ScriptBundle("~/Scripts/plugs").Include(
          "~/Scripts/plugs.js"
          ));

            bundles.Add(new ScriptBundle("~/Scripts/myjs").Include(
          "~/Scripts/MyScript.js"
          ));

            bundles.Add(new ScriptBundle("~/Scripts/flatpickrjs").Include(
                      "~/Scripts/flatpickr.js",
                      "~/Scripts/flatpickr-locale-zh.js"
                      ));
            bundles.Add(new ScriptBundle("~/Scripts/bootstrap").Include(
          "~/Scripts/bootstrap.js"
          ));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css",
                      "~/Content/style.css",
                      "~/Content/font-awesome.min.css",
                      "~/Content/layui.css"
                      ));

            bundles.Add(new StyleBundle("~/Content/upcss").Include(
                      "~/Content/amazeui.min.css",
                      "~/Content/admin.css",
                      "~/Content/app.css",
                      "~/Content/my.css"
                      ));


            bundles.Add(new StyleBundle("~/Content/flatpickr").Include(
                    "~/Content/flatpickr.min.css"
                    ));

            bundles.Add(new StyleBundle("~/Content/my").Include(
        "~/Content/my.css"
        ));

        }
    }
}
