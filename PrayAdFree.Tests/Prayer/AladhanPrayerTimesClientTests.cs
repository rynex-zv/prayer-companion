using System.Net;
using System.Net.Http;
using System.Text;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class AladhanPrayerTimesClientTests {
    [Fact]
    public async Task GetMonthAsync_ParsesResponse() {
        var json = """
        {
          "data": [
            {
              "timings": {
                "Fajr": "05:00 (+03)",
                "Sunrise": "06:10 (+03)",
                "Dhuhr": "12:00 (+03)",
                "Asr": "15:00 (+03)",
                "Maghrib": "18:00 (+03)",
                "Isha": "19:00 (+03)",
                "Imsak": "04:40 (+03)"
              },
              "date": {
                "gregorian": { "date": "01-01-2025" },
                "hijri": { "day": "01", "year": "1446", "month": { "en": "Muharram" } }
              }
            }
          ]
        }
        """;
        var handler = new StubHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.aladhan.com/v1/") };
        var api = new AladhanPrayerTimesClient(client);
        var settings = new AppSettings {
            Location = new LocationSettings { Latitude = 24.0, Longitude = 46.0, TimeZoneId = TimeZoneInfo.Local.Id },
            Method = CalculationMethod.MuslimWorldLeague
        };

        var month = await api.GetMonthAsync(settings, 2025, 1, CancellationToken.None);

        Assert.Single(month.Days);
        Assert.Equal("Muharram", month.Days[0].Hijri.Month);
    }


    [Fact]
    public async Task GetMonthAsync_MapsIshaOffsetToIshaTuneSlot() {
        var json = """
        {
          "data": [
            {
              "timings": {
                "Fajr": "05:00 (+03)",
                "Sunrise": "06:10 (+03)",
                "Dhuhr": "12:00 (+03)",
                "Asr": "15:00 (+03)",
                "Maghrib": "18:00 (+03)",
                "Isha": "19:00 (+03)",
                "Imsak": "04:40 (+03)"
              },
              "date": {
                "gregorian": { "date": "01-01-2025" },
                "hijri": { "day": "01", "year": "1446", "month": { "en": "Muharram" } }
              }
            }
          ]
        }
        """;

        var handler = new StubHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.aladhan.com/v1/") };
        var api = new AladhanPrayerTimesClient(client);
        var settings = new AppSettings {
            Location = new LocationSettings { Latitude = 24.0, Longitude = 46.0, TimeZoneId = TimeZoneInfo.Local.Id },
            Method = CalculationMethod.MuslimWorldLeague,
            Offsets = new PrayerOffsets { Isha = 50 }
        };

        _ = await api.GetMonthAsync(settings, 2025, 1, CancellationToken.None);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("tune=0,0,0,0,0,0,0,50,0", handler.LastRequestUri!.Query, StringComparison.Ordinal);
    }

    private sealed class StubHandler : HttpMessageHandler {
        private readonly string _response;

        public StubHandler(string response) {
            _response = response;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequestUri = request.RequestUri;
            var message = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(message);
        }
    }
}
