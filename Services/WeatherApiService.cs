using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WeatherSite.Contracts;
using WeatherSite.Models;

namespace WeatherSite.Services
{
    public class WeatherApiService
    {
        private const string BaseUrl = "https://api.weatherapi.com/v1";
        private const string CurrentFields = "temp_c,feelslike_c,humidity,wind_kph,last_updated,condition";
        private const string DayFields = "maxtemp_c,mintemp_c,maxwind_kph,totalprecip_mm,condition";
        private const string HourFields = "time,temp_c,chance_of_rain,condition";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan FreshTtl = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan StaleTtl = TimeSpan.FromMinutes(20);
        private const int MaxAttempts = 3;
        private static readonly MemoryCache WeatherCache = MemoryCache.Default;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly string _apiKey;
        private readonly string _defaultQuery;

        static WeatherApiService()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
        }

        public WeatherApiService()
        {
            _apiKey = ConfigurationManager.AppSettings["WeatherApiKey"];
            var latitude = ConfigurationManager.AppSettings["MoscowLat"] ?? "55.7558";
            var longitude = ConfigurationManager.AppSettings["MoscowLon"] ?? "37.6176";
            _defaultQuery = string.Format(CultureInfo.InvariantCulture, "{0},{1}", latitude, longitude);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("WeatherApiKey is missing in Web.config.");
            }
        }

        public Task<WeatherViewModel> GetWeatherAsync()
        {
            return GetWeatherAsync(null, null);
        }

        public async Task<WeatherViewModel> GetWeatherAsync(double? latitude, double? longitude)
        {
            var query = BuildQuery(latitude, longitude);
            var cacheKey = BuildCacheKey(latitude, longitude);
            CachedWeatherEntry cachedEntry;
            var now = DateTimeOffset.UtcNow;

            if (TryGetCacheEntry(cacheKey, out cachedEntry) && cachedEntry.FreshUntilUtc > now)
            {
                return CloneWeather(cachedEntry.Weather);
            }

            var keyLock = KeyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync().ConfigureAwait(false);

            try
            {
                now = DateTimeOffset.UtcNow;
                if (TryGetCacheEntry(cacheKey, out cachedEntry) && cachedEntry.FreshUntilUtc > now)
                {
                    return CloneWeather(cachedEntry.Weather);
                }

                try
                {
                    var fetchedWeather = await FetchFromApiAsync(query).ConfigureAwait(false);
                    var newEntry = new CachedWeatherEntry
                    {
                        Weather = fetchedWeather,
                        FreshUntilUtc = now.Add(FreshTtl),
                        StaleUntilUtc = now.Add(StaleTtl)
                    };

                    WeatherCache.Set(
                        cacheKey,
                        newEntry,
                        new CacheItemPolicy
                        {
                            AbsoluteExpiration = newEntry.StaleUntilUtc
                        });

                    return CloneWeather(fetchedWeather);
                }
                catch
                {
                    if (TryGetCacheEntry(cacheKey, out cachedEntry) && cachedEntry.StaleUntilUtc > now)
                    {
                        return CloneWeather(cachedEntry.Weather);
                    }

                    throw;
                }
            }
            finally
            {
                keyLock.Release();
            }
        }

        private async Task<WeatherViewModel> FetchFromApiAsync(string query)
        {
            using (var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseProxy = false,
                Proxy = null,
                UseCookies = false
            })
            using (var client = new HttpClient(handler))
            {
                client.Timeout = RequestTimeout;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.ConnectionClose = true;

                var currentUrl = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/current.json?key={1}&q={2}&lang=ru&current_fields={3}",
                    BaseUrl,
                    Uri.EscapeDataString(_apiKey),
                    Uri.EscapeDataString(query),
                    CurrentFields);

                var forecastUrl = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/forecast.json?key={1}&q={2}&days=3&lang=ru&day_fields={3}&hour_fields={4}",
                    BaseUrl,
                    Uri.EscapeDataString(_apiKey),
                    Uri.EscapeDataString(query),
                    DayFields,
                    HourFields);

                var currentJson = await GetJsonAsync(client, currentUrl, "current").ConfigureAwait(false);
                var forecastJson = await GetJsonAsync(client, forecastUrl, "forecast").ConfigureAwait(false);

                var currentResponse = JsonConvert.DeserializeObject<CurrentApiResponseDto>(currentJson);
                var forecastResponse = JsonConvert.DeserializeObject<ForecastApiResponseDto>(forecastJson);

                if (currentResponse == null || forecastResponse == null || forecastResponse.Forecast == null)
                {
                    throw new InvalidOperationException("Weather API returned an invalid response.");
                }

                return BuildViewModel(currentResponse, forecastResponse);
            }
        }

        private static bool TryGetCacheEntry(string cacheKey, out CachedWeatherEntry entry)
        {
            entry = WeatherCache.Get(cacheKey) as CachedWeatherEntry;
            return entry != null && entry.Weather != null;
        }

        private static string BuildCacheKey(double? latitude, double? longitude)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                return "weather:default:moscow";
            }

            var normalizedLat = Math.Round(latitude.Value, 2, MidpointRounding.AwayFromZero);
            var normalizedLon = Math.Round(longitude.Value, 2, MidpointRounding.AwayFromZero);
            return string.Format(CultureInfo.InvariantCulture, "weather:{0:F2}:{1:F2}", normalizedLat, normalizedLon);
        }

        private string BuildQuery(double? latitude, double? longitude)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                return _defaultQuery;
            }

            if (latitude.Value < -90 || latitude.Value > 90 || longitude.Value < -180 || longitude.Value > 180)
            {
                throw new ArgumentOutOfRangeException("latitude/longitude", "Координаты вне допустимого диапазона.");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1}",
                latitude.Value,
                longitude.Value);
        }

        private static async Task<string> GetJsonAsync(HttpClient client, string url, string endpointName)
        {
            Exception lastException = null;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(RequestTimeout))
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await client.SendAsync(
                               request,
                               HttpCompletionOption.ResponseHeadersRead,
                               cts.Token).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Weather API {0} failed with status {1}. Body: {2}",
                                    endpointName,
                                    (int)response.StatusCode,
                                    body));
                        }

                        return body;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                }

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt)).ConfigureAwait(false);
                }
            }

            throw new TimeoutException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Weather API {0} request timed out or failed after {1} attempts (timeout {2} sec each).",
                    endpointName,
                    MaxAttempts,
                    RequestTimeout.TotalSeconds),
                lastException);
        }

        private static WeatherViewModel BuildViewModel(CurrentApiResponseDto currentResponse, ForecastApiResponseDto forecastResponse)
        {
            var localNow = DateTimeOffset.FromUnixTimeSeconds(currentResponse.Location.LocaltimeEpoch).DateTime;
            var today = localNow.Date;
            var tomorrow = today.AddDays(1);

            var hourly = forecastResponse.Forecast.Forecastday
                .Where(day => day.Date == today || day.Date == tomorrow)
                .SelectMany(day =>
                {
                    if (day.Date == today)
                    {
                        return day.Hour.Where(h => h.Time.Hour >= localNow.Hour);
                    }

                    return day.Hour;
                })
                .OrderBy(h => h.Time)
                .Select(h => new HourlyForecastViewModel
                {
                    Date = h.Time.ToString("dd.MM", CultureInfo.InvariantCulture),
                    Time = h.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
                    TempC = h.TempC,
                    Condition = h.Condition != null ? h.Condition.Text : string.Empty,
                    IconUrl = NormalizeIconUrl(h.Condition != null ? h.Condition.Icon : string.Empty),
                    ChanceOfRain = h.ChanceOfRain
                })
                .ToList();

            var daily = forecastResponse.Forecast.Forecastday
                .OrderBy(day => day.Date)
                .Take(3)
                .Select(day => new DailyForecastViewModel
                {
                    Date = day.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    MinTempC = day.Day.MintempC,
                    MaxTempC = day.Day.MaxtempC,
                    Condition = day.Day.Condition != null ? day.Day.Condition.Text : string.Empty,
                    IconUrl = NormalizeIconUrl(day.Day.Condition != null ? day.Day.Condition.Icon : string.Empty),
                    MaxWindKph = day.Day.MaxwindKph,
                    TotalPrecipMm = day.Day.TotalprecipMm
                })
                .ToList();

            return new WeatherViewModel
            {
                City = currentResponse.Location != null ? currentResponse.Location.Name : "Moscow",
                Current = new CurrentWeatherViewModel
                {
                    Condition = currentResponse.Current.Condition != null ? currentResponse.Current.Condition.Text : string.Empty,
                    IconUrl = NormalizeIconUrl(currentResponse.Current.Condition != null ? currentResponse.Current.Condition.Icon : string.Empty),
                    TempC = currentResponse.Current.TempC,
                    FeelsLikeC = currentResponse.Current.FeelslikeC,
                    Humidity = currentResponse.Current.Humidity,
                    WindKph = currentResponse.Current.WindKph,
                    LastUpdated = currentResponse.Current.LastUpdated
                },
                Hourly = hourly,
                Daily = daily
            };
        }

        private static WeatherViewModel CloneWeather(WeatherViewModel source)
        {
            if (source == null)
            {
                return null;
            }

            return new WeatherViewModel
            {
                City = source.City,
                Current = source.Current == null
                    ? null
                    : new CurrentWeatherViewModel
                    {
                        Condition = source.Current.Condition,
                        IconUrl = source.Current.IconUrl,
                        TempC = source.Current.TempC,
                        FeelsLikeC = source.Current.FeelsLikeC,
                        Humidity = source.Current.Humidity,
                        WindKph = source.Current.WindKph,
                        LastUpdated = source.Current.LastUpdated
                    },
                Hourly = source.Hourly == null
                    ? new List<HourlyForecastViewModel>()
                    : source.Hourly.Select(h => new HourlyForecastViewModel
                    {
                        Date = h.Date,
                        Time = h.Time,
                        TempC = h.TempC,
                        Condition = h.Condition,
                        IconUrl = h.IconUrl,
                        ChanceOfRain = h.ChanceOfRain
                    }).ToList(),
                Daily = source.Daily == null
                    ? new List<DailyForecastViewModel>()
                    : source.Daily.Select(d => new DailyForecastViewModel
                    {
                        Date = d.Date,
                        MinTempC = d.MinTempC,
                        MaxTempC = d.MaxTempC,
                        Condition = d.Condition,
                        IconUrl = d.IconUrl,
                        MaxWindKph = d.MaxWindKph,
                        TotalPrecipMm = d.TotalPrecipMm
                    }).ToList()
            };
        }

        private static string NormalizeIconUrl(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
            {
                return string.Empty;
            }

            return icon.StartsWith("//", StringComparison.Ordinal) ? "https:" + icon : icon;
        }

        private class CachedWeatherEntry
        {
            public WeatherViewModel Weather { get; set; }
            public DateTimeOffset FreshUntilUtc { get; set; }
            public DateTimeOffset StaleUntilUtc { get; set; }
        }
    }
}
