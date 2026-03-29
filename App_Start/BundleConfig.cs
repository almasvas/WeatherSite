using System.Web.Optimization;

namespace WeatherSite
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/weather").Include(
                "~/Scripts/weather.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/site.css"));

            BundleTable.EnableOptimizations = false;
        }
    }
}
