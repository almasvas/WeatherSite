using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WeatherSite.Contracts
{
    public class CurrentApiResponseDto
    {
        [JsonProperty("location")]
        public LocationDto Location { get; set; }

        [JsonProperty("current")]
        public CurrentWeatherDto Current { get; set; }
    }

    public class ForecastApiResponseDto
    {
        [JsonProperty("location")]
        public LocationDto Location { get; set; }

        [JsonProperty("forecast")]
        public ForecastDto Forecast { get; set; }
    }

    public class LocationDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("localtime_epoch")]
        public long LocaltimeEpoch { get; set; }
    }

    public class CurrentWeatherDto
    {
        [JsonProperty("temp_c")]
        public decimal TempC { get; set; }

        [JsonProperty("feelslike_c")]
        public decimal FeelslikeC { get; set; }

        [JsonProperty("humidity")]
        public int Humidity { get; set; }

        [JsonProperty("wind_kph")]
        public decimal WindKph { get; set; }

        [JsonProperty("last_updated")]
        public string LastUpdated { get; set; }

        [JsonProperty("condition")]
        public ConditionDto Condition { get; set; }
    }

    public class ForecastDto
    {
        [JsonProperty("forecastday")]
        public List<ForecastDayDto> Forecastday { get; set; }
    }

    public class ForecastDayDto
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("day")]
        public DaySummaryDto Day { get; set; }

        [JsonProperty("hour")]
        public List<HourForecastDto> Hour { get; set; }
    }

    public class DaySummaryDto
    {
        [JsonProperty("maxtemp_c")]
        public decimal MaxtempC { get; set; }

        [JsonProperty("mintemp_c")]
        public decimal MintempC { get; set; }

        [JsonProperty("maxwind_kph")]
        public decimal MaxwindKph { get; set; }

        [JsonProperty("totalprecip_mm")]
        public decimal TotalprecipMm { get; set; }

        [JsonProperty("condition")]
        public ConditionDto Condition { get; set; }
    }

    public class HourForecastDto
    {
        [JsonProperty("time")]
        public DateTime Time { get; set; }

        [JsonProperty("temp_c")]
        public decimal TempC { get; set; }

        [JsonProperty("chance_of_rain")]
        public int ChanceOfRain { get; set; }

        [JsonProperty("condition")]
        public ConditionDto Condition { get; set; }
    }

    public class ConditionDto
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }
}
