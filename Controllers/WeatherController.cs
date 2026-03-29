using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using WeatherSite.Services;

namespace WeatherSite.Controllers
{
    public class WeatherController : Controller
    {
        private readonly WeatherApiService _weatherApiService;
        private readonly ReverseGeocodingService _reverseGeocodingService;

        public WeatherController()
        {
            _weatherApiService = new WeatherApiService();
            _reverseGeocodingService = new ReverseGeocodingService();
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<ActionResult> Data(double? lat = null, double? lon = null)
        {
            try
            {
                var result = await _weatherApiService.GetWeatherAsync(lat, lon);
                if (lat.HasValue && lon.HasValue)
                {
                    var localizedName = await _reverseGeocodingService.ResolveCityNameAsync(lat.Value, lon.Value);
                    if (!string.IsNullOrWhiteSpace(localizedName))
                    {
                        result.City = localizedName;
                    }
                }

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 502;
                return Json(new { message = "Не удалось получить данные погоды.", details = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
