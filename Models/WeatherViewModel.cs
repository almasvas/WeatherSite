using System.Collections.Generic;

namespace WeatherSite.Models
{
    public class WeatherViewModel
    {
        public string City { get; set; }
        public CurrentWeatherViewModel Current { get; set; }
        public List<HourlyForecastViewModel> Hourly { get; set; }
        public List<DailyForecastViewModel> Daily { get; set; }
    }

    public class CurrentWeatherViewModel
    {
        public string Condition { get; set; }
        public string IconUrl { get; set; }
        public decimal TempC { get; set; }
        public decimal FeelsLikeC { get; set; }
        public int Humidity { get; set; }
        public decimal WindKph { get; set; }
        public string LastUpdated { get; set; }
    }

    public class HourlyForecastViewModel
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public decimal TempC { get; set; }
        public string Condition { get; set; }
        public string IconUrl { get; set; }
        public int ChanceOfRain { get; set; }
    }

    public class DailyForecastViewModel
    {
        public string Date { get; set; }
        public decimal MinTempC { get; set; }
        public decimal MaxTempC { get; set; }
        public string Condition { get; set; }
        public string IconUrl { get; set; }
        public decimal MaxWindKph { get; set; }
        public decimal TotalPrecipMm { get; set; }
    }
}
