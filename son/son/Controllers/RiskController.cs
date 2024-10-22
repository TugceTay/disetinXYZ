using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using System.Text;

namespace son.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RiskController : ControllerBase
    {
        private readonly RepositoryContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RiskController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Hesaplanan parametrelerin hafızada tutulması için iki dictionary
        private static Dictionary<string, double> _calculatedParameters = new Dictionary<string, double>();
        private static Dictionary<string, string> _calculatedStringParameters = new Dictionary<string, string>();

        public RiskController(RepositoryContext context, IConfiguration configuration, ILogger<RiskController> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        // Yeni geometri çizildiğinde hafızayı temizleyen API
        [HttpPost("ClearMemory")]
        public IActionResult ClearMemory()
        {
            _calculatedParameters.Clear();
            _calculatedStringParameters.Clear();
            return Ok(new { message = "Memory cleared" });
        }

       
        
        
        [HttpPost("CalculateRisk")]
        public async Task<IActionResult> CalculateRisk([FromBody] JObject request)
        {
            _logger.LogInformation("Received CalculateRisk request: {Request}", request.ToString());

            var properties = request["properties"];
            _logger.LogInformation("Received Properties: {Properties}", properties.ToString());
            _logger.LogInformation("Received distanceToWetlandsValue: {Value}", properties["distanceToWetlandsValue"]);

            var geometryJson = request["geometry"];
            Geometry geometry = null;

            try
            {
                if (geometryJson != null && _calculatedParameters.Count == 0)
                {
                    var geoJsonReader = new GeoJsonReader();
                    geometry = geoJsonReader.Read<Geometry>(geometryJson.ToString());
                    _logger.LogInformation("Parsed Geometry: {Geometry}", geometry.ToString());
                }
                else if (_calculatedParameters.Count == 0)
                {
                    _logger.LogWarning("Geometri eksik, ilk hesaplama yapılamaz.");
                    return BadRequest("Geometri eksik, veritabanından sorgu yapılamaz.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Geometri işleme hatası.");
                return BadRequest("Geçersiz GeoJSON geometrisi");
            }

           
            // Parametreleri güvenli bir şekilde ayır
            var slopePercentage = ParseDouble(properties["slopeDegreeValue"]);
            var height = ParseDouble(properties["elevationValue"]);
            var faultDistance = ParseDouble(properties["distanceToFaultValue"]);
            var wetlandDistance = ParseDouble(properties["wetlandDistance"]);
            if (wetlandDistance == null)
            {
                _logger.LogWarning("Received null or invalid wetlandDistance");
            }
            else
            {
                _logger.LogInformation("Successfully parsed wetlandDistance: {Value}", wetlandDistance);
            }



            _logger.LogInformation("Parsed Parameters: Slope = {Slope}, Height = {Height}, Fault Distance = {FaultDistance}, Wetland Distance = {WetlandDistance}",
    slopePercentage, height, faultDistance, wetlandDistance);

            var zeminSinifi = properties["zeminSinifi"]?.ToString();
            var toprakTipi = properties["toprakTipi"]?.ToString();
            var yagis = properties["yagis"]?.ToString();
            var bitkiOrtusu = properties["bitkiOrtusu"]?.ToString();
            var litoloji = properties["litoloji"]?.ToString();

            _logger.LogInformation("Parsed Parameters: Slope = {Slope}, Height = {Height}, Fault Distance = {FaultDistance}, Wetland Distance = {WetlandDistance}, Zemin Sınıfı = {ZeminSinifi}, Toprak Tipi = {ToprakTipi}, Yağış = {Yagis}, Bitki Örtüsü = {BitkiOrtusu}, Litoloji = {Litoloji}",
                slopePercentage, height, faultDistance, wetlandDistance, zeminSinifi, toprakTipi, yagis, bitkiOrtusu, litoloji);

            // Frontend'den gelen değerleri bellekte güncelle
            UpdateMemoryParameters(slopePercentage, height, faultDistance, wetlandDistance, zeminSinifi, toprakTipi, yagis, bitkiOrtusu, litoloji);

            // Bellek dolu değilse, tüm parametreleri veritabanından al
            if (_calculatedParameters.Count == 0 && geometry != null)
            {
                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("sqlConnection"));
                await conn.OpenAsync();

                slopePercentage = await GetSlopeFromDatabase(geometry, conn);
                height = await GetHeightFromDatabase(geometry, conn);
                faultDistance = await GetFaultDistanceFromDatabase(geometry, conn);
                wetlandDistance = await GetWetlandDistanceFromGeoJson(request);

                // Bellekte sakla
                UpdateMemoryParameters(slopePercentage, height, faultDistance, wetlandDistance, zeminSinifi, toprakTipi, yagis, bitkiOrtusu, litoloji);
            }

            _logger.LogInformation("Updated Memory Parameters: Slope = {Slope}, Height = {Height}, Fault Distance = {FaultDistance}, Wetland Distance = {WetlandDistance}",
                _calculatedParameters["slope"], _calculatedParameters["height"], _calculatedParameters["faultDistance"], _calculatedParameters["wetlandDistance"]);

            // Parametrelerin normalize edilmesi ve puanlanması
            int scaledFaultDistance = NormalizeFaultDistance(_calculatedParameters["faultDistance"]);
            int scaledWetlandDistance = NormalizeWetlandDistance(_calculatedParameters["wetlandDistance"]);
            int scaledRainfall = NormalizeRainfall(GetYagisPuani(_calculatedStringParameters["yagis"]));

            // Toplam deprem tehlikesi hesaplama
            double earthquakeRisk = (scaledFaultDistance * 0.40)
                                  + (GetZeminPuani(_calculatedStringParameters["zeminSinifi"]) * 0.35)
                                  + (GetToprakPuani(_calculatedStringParameters["toprakTipi"]) * 0.25);

            double floodRisk = (GetToprakPuani(_calculatedStringParameters["toprakTipi"]) * 0.20)
                             + (scaledRainfall * 0.30)
                             + (FloodRiskSlope(_calculatedParameters["slope"]) * 0.10)
                             + (scaledWetlandDistance * 0.20)
                             + (GetBitkiOrtusuPuani(_calculatedStringParameters["bitkiOrtusu"]) * 0.10)
                             + (FloodRiskHeight(_calculatedParameters["height"]) * 0.10);

            double landslideRisk = (LandslideRiskSlope(_calculatedParameters["slope"]) * 0.35)
                                 + (GetLitolojiPuani(_calculatedStringParameters["litoloji"]) * 0.25)
                                 + (GetToprakPuani(_calculatedStringParameters["toprakTipi"]) * 0.15)
                                 + (scaledRainfall * 0.15)
                                 + (GetBitkiOrtusuPuani(_calculatedStringParameters["bitkiOrtusu"]) * 0.10);

            return Ok(new
            {
                earthquakeRisk = $"{earthquakeRisk:F2}%", 
                floodRisk = $"{floodRisk:F2}%",
                landslideRisk = $"{landslideRisk:F2}%",
                wetlandDistance = $"{_calculatedParameters["wetlandDistance"]:F2} meters",
                slopePercentage = _calculatedParameters["slope"],
                height = _calculatedParameters["height"],
                faultDistance = _calculatedParameters["faultDistance"],
                zeminSinifi = _calculatedStringParameters["zeminSinifi"],
                toprakTipi = _calculatedStringParameters["toprakTipi"],
                yagis = _calculatedStringParameters["yagis"],
                bitkiOrtusu = _calculatedStringParameters["bitkiOrtusu"],
                litoloji = _calculatedStringParameters["litoloji"]
            });


        }

        // Hafıza parametrelerini güncelleyen yöntem
        private void UpdateMemoryParameters(double? slope, double? height, double? faultDistance, double? wetlandDistance, 
                                            string zeminSinifi, string toprakTipi, string yagis, string bitkiOrtusu, string litoloji)
        {
            if (slope.HasValue) _calculatedParameters["slope"] = slope.Value;
            if (height.HasValue) _calculatedParameters["height"] = height.Value;
            if (faultDistance.HasValue) _calculatedParameters["faultDistance"] = faultDistance.Value;
            if (wetlandDistance.HasValue)
            {
                _logger.LogInformation("Updating Wetland Distance in memory: {Value}", wetlandDistance.Value);
                _calculatedParameters["wetlandDistance"] = wetlandDistance.Value;
            }





            if (!string.IsNullOrEmpty(zeminSinifi)) _calculatedStringParameters["zeminSinifi"] = zeminSinifi;
            if (!string.IsNullOrEmpty(toprakTipi)) _calculatedStringParameters["toprakTipi"] = toprakTipi;
            if (!string.IsNullOrEmpty(yagis)) _calculatedStringParameters["yagis"] = yagis;
            if (!string.IsNullOrEmpty(bitkiOrtusu)) _calculatedStringParameters["bitkiOrtusu"] = bitkiOrtusu;
            if (!string.IsNullOrEmpty(litoloji)) _calculatedStringParameters["litoloji"] = litoloji;
        }

        // Güvenli parse işlemi
        private double? ParseDouble(JToken token)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.ToString()))
            {
                _logger.LogWarning("Null or empty token received for parsing.");
                return null; // Burada varsayılan bir değer döndürmeyi düşünebilirsiniz
            }

            _logger.LogInformation("Parsing double value: {Value}", token.ToString());

            // Sadece sayısal değeri almak için regex kullanıyoruz
            var numberString = System.Text.RegularExpressions.Regex.Match(token.ToString(), @"\d+(\.\d+)?").Value;

            if (double.TryParse(numberString, out double value))
            {
                _logger.LogInformation("Parsed double value successfully: {ParsedValue}", value);
                return value;
            }

            _logger.LogWarning("Invalid double format: {Value}", token);
            return null; // Burada da varsayılan bir değer döndürmeyi düşünebilirsiniz
        }








        // Veritabanından eğim hesaplama
        private async Task<double> GetSlopeFromDatabase(Geometry geometry, NpgsqlConnection conn)
        {
            using (var slopeCmd = new NpgsqlCommand("SELECT * FROM calculate_slope(@polygon_geom)", conn))
            {
                slopeCmd.Parameters.AddWithValue("polygon_geom", geometry);
                using var slopeReader = await slopeCmd.ExecuteReaderAsync();
                if (slopeReader.Read())
                {
                    return slopeReader.GetDouble(0);
                }
            }
            return 0;
        }

        // Veritabanından yükseklik hesaplama
        private async Task<double> GetHeightFromDatabase(Geometry geometry, NpgsqlConnection conn)
        {
            using (var heightCmd = new NpgsqlCommand("SELECT * FROM calculate_height(@polygon_geom)", conn))
            {
                heightCmd.Parameters.AddWithValue("polygon_geom", geometry);
                using var heightReader = await heightCmd.ExecuteReaderAsync();
                if (heightReader.Read())
                {
                    return heightReader.GetDouble(0);
                }
            }
            return 0;
        }

        // Veritabanından fay hattı mesafesi hesaplama
        private async Task<double> GetFaultDistanceFromDatabase(Geometry geometry, NpgsqlConnection conn)
        {
            using (var faultCmd = new NpgsqlCommand("SELECT * FROM calculate_nearest_fault(@polygon_geom)", conn))
            {
                faultCmd.Parameters.AddWithValue("polygon_geom", geometry);
                using var faultReader = await faultCmd.ExecuteReaderAsync();
                if (faultReader.Read())
                {
                    return faultReader.GetDouble(0);
                }
            }
            return 0;
        }

        // Sulak alan mesafesi hesaplama
        private async Task<double> GetWetlandDistanceFromGeoJson(JObject geoJson)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://disetinapi.azurewebsites.net");

            try
            {
                _logger.LogInformation("Sending request to Wetland API with GeoJSON: {GeoJson}", geoJson.ToString());

                var response = await client.PostAsync("api/wetland/CalculateNearestWaterbody", new StringContent(geoJson.ToString(), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var wetlandResult = JObject.Parse(responseString);
                    _logger.LogInformation("Parsed Response from Wetland API: {WetlandResult}", wetlandResult);

                    return wetlandResult["distanceInMeters"]?.ToObject<double>() ?? 0;
                }

                var errorResponse = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error response from WetlandController: {Response}", errorResponse);
                throw new InvalidOperationException("Sulak alan mesafesi hesaplanamadı.");
            }
            catch (HttpRequestException httpRequestException)
            {
                _logger.LogError(httpRequestException, "HTTP request to WetlandController failed.");
                throw new InvalidOperationException("Sulak alan mesafesi hesaplanamadı.", httpRequestException);
            }
        }


        // Zemin sınıfı puanlama
        private int GetZeminPuani(string zeminSinifi)
        {
            switch (zeminSinifi)
            {
                case "ZA": return 20;
                case "ZB": return 40;
                case "ZC": return 60;
                case "ZD": return 80;
                case "ZE": return 90;
                case "ZF": return 100;
                default: return 0;
            }
        }

        // Toprak tipi puanlama
        private int GetToprakPuani(string toprakTipi)
        {
            switch (toprakTipi)
            {
                case "Organik Topraklar": return 100;
                case "Killi Topraklar": return 80;
                case "Siltli Topraklar": return 60;
                case "Kumlu Topraklar": return 40;
                case "Çakıllı Topraklar": return 20;
                default: return 0;
            }
        }

        // Yağış puanlama
        private int GetYagisPuani(string yagis)
        {
            switch (yagis)
            {
                case "Çok Düşük Yağış": return 20;
                case "Düşük Yağış": return 40;
                case "Orta Yağış": return 60;
                case "Yüksek Yağış": return 80;
                case "Çok Yüksek Yağış": return 100;
                default: return 0;
            }
        }

        // Bitki örtüsü puanlama
        private int GetBitkiOrtusuPuani(string bitkiOrtusu)
        {
            switch (bitkiOrtusu)
            {
                case "Kentsel Alanlar": return 100;
                case "Tarım Alanları": return 80;
                case "Otlaklar/Çayırlar": return 60;
                case "Çalılık Alanlar": return 40;
                case "Ormanlık Alanlar": return 20;
                default: return 0;
            }
        }

        // Litoloji puanlama
        private int GetLitolojiPuani(string litoloji)
        {
            switch (litoloji)
            {
                case "Alüvyon": return 70;
                case "Kiltaşı": return 60;
                case "Çakıltaşı": return 50;
                case "Kumtaşı": return 40;
                case "Kireçtaşı": return 30;
                case "Bazalt": return 20;
                case "Granit": return 10;
                default: return 0;
            }
        }

        // Fay hattı mesafesi normalizasyonu
        private int NormalizeFaultDistance(double faultDistance)
        {
            if (faultDistance <= 50) return 100;
            if (faultDistance <= 100) return 90;
            if (faultDistance <= 200) return 80;
            if (faultDistance <= 300) return 70;
            if (faultDistance <= 400) return 60;
            if (faultDistance <= 500) return 50;
            if (faultDistance <= 600) return 40;
            if (faultDistance <= 700) return 30;
            if (faultDistance <= 800) return 20;
            return 10;
        }

        // Sulak alan mesafesi normalizasyonu
        private int NormalizeWetlandDistance(double wetlandDistance)
        {
            if (wetlandDistance <= 100) return 100;
            if (wetlandDistance <= 200) return 90;
            if (wetlandDistance <= 300) return 80;
            if (wetlandDistance <= 400) return 70;
            if (wetlandDistance <= 500) return 60;
            if (wetlandDistance <= 600) return 50;
            if (wetlandDistance <= 700) return 40;
            if (wetlandDistance <= 800) return 30;
            if (wetlandDistance <= 900) return 20;
            return 10;
        }

        // Yağış normalizasyonu
        private int NormalizeRainfall(double rainfall)
        {
            if (rainfall <= 200) return 10;
            if (rainfall <= 400) return 20;
            if (rainfall <= 600) return 30;
            if (rainfall <= 800) return 40;
            if (rainfall <= 1000) return 50;
            if (rainfall <= 1200) return 60;
            if (rainfall <= 1400) return 70;
            if (rainfall <= 1600) return 80;
            if (rainfall <= 1800) return 90;
            return 100;
        }

        // Sel tehlikesi için eğim puanlama
        private int FloodRiskSlope(double slopePercentage)
        {
            if (slopePercentage <= 5) return 100;
            if (slopePercentage <= 10) return 90;
            if (slopePercentage <= 15) return 80;
            if (slopePercentage <= 20) return 70;
            if (slopePercentage <= 25) return 60;
            if (slopePercentage <= 30) return 50;
            if (slopePercentage <= 35) return 40;
            if (slopePercentage <= 40) return 30;
            if (slopePercentage <= 45) return 20;
            return 10;
        }

        // Heyelan tehlikesi için eğim puanlama
        private int LandslideRiskSlope(double slopePercentage)
        {
            if (slopePercentage <= 5) return 10;
            if (slopePercentage <= 10) return 20;
            if (slopePercentage <= 15) return 30;
            if (slopePercentage <= 20) return 40;
            if (slopePercentage <= 25) return 50;
            if (slopePercentage <= 30) return 60;
            if (slopePercentage <= 35) return 80;
            if (slopePercentage <= 40) return 90;
            if (slopePercentage <= 45) return 100;
            return 100;
        }

        // Sel tehlikesi için yükseklik puanlama
        private int FloodRiskHeight(double height)
        {
            if (height <= 50) return 100;
            if (height <= 100) return 90;
            if (height <= 200) return 80;
            if (height <= 300) return 70;
            if (height <= 400) return 60;
            if (height <= 500) return 50;
            if (height <= 600) return 40;
            if (height <= 700) return 30;
            if (height <= 800) return 20;
            return 10;
        }

   
    }
}
