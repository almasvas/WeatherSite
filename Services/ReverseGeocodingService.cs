using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WeatherSite.Services
{
    public class ReverseGeocodingService
    {
        private const string ReverseUrlTemplate =
            "https://nominatim.openstreetmap.org/reverse?format=jsonv2&accept-language=ru&zoom=10&lat={0}&lon={1}";
        private static readonly MemoryCache GeocodingCache = MemoryCache.Default;
        private static readonly TimeSpan GeocodingCacheTtl = TimeSpan.FromHours(12);
        private static readonly HttpClient GeocodingClient = CreateClient();

        static ReverseGeocodingService()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public async Task<string> ResolveCityNameAsync(double latitude, double longitude)
        {
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                return null;
            }

            var cacheKey = BuildCacheKey(latitude, longitude);
            var cached = GeocodingCache.Get(cacheKey) as string;
            if (!string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            var url = string.Format(
                CultureInfo.InvariantCulture,
                ReverseUrlTemplate,
                latitude,
                longitude);

            try
            {
                var raw = await GeocodingClient.GetStringAsync(url).ConfigureAwait(false);
                var serializer = new JavaScriptSerializer();
                var response = serializer.Deserialize<NominatimResponse>(raw);
                var city = PickBestCityName(response != null ? response.address : null);
                if (!string.IsNullOrWhiteSpace(city))
                {
                    GeocodingCache.Set(
                        cacheKey,
                        city,
                        new CacheItemPolicy
                        {
                            AbsoluteExpiration = DateTimeOffset.UtcNow.Add(GeocodingCacheTtl)
                        });
                }

                return city;
            }
            catch
            {
                return null;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherSite/1.0 (reverse-geocoding)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        private static string BuildCacheKey(double latitude, double longitude)
        {
            var roundedLat = Math.Round(latitude, 3, MidpointRounding.AwayFromZero);
            var roundedLon = Math.Round(longitude, 3, MidpointRounding.AwayFromZero);
            return string.Format(CultureInfo.InvariantCulture, "geo:{0:F3}:{1:F3}", roundedLat, roundedLon);
        }

        private static string PickBestCityName(Dictionary<string, object> address)
        {
            if (address == null)
            {
                return null;
            }

            string[] priorityKeys =
            {
                "city",
                "town",
                "village",
                "municipality",
                "county",
                "state_district",
                "state"
            };

            foreach (var key in priorityKeys)
            {
                object value;
                if (address.TryGetValue(key, out value) && value != null)
                {
                    var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private class NominatimResponse
        {
            public Dictionary<string, object> address { get; set; }
        }
    }
}
