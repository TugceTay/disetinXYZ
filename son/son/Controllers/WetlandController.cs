using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace son.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WetlandController : ControllerBase
    {
        private readonly RepositoryContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WetlandController> _logger;

        public WetlandController(RepositoryContext context, IConfiguration configuration, ILogger<WetlandController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }


        [HttpGet("GetWetlandDistance")]
        public async Task<IActionResult> GetWetlandDistance(double latitude, double longitude)
        {
            var centroid = new Point(latitude, longitude) { SRID = 4326 };
            var nearestWaterbody = await GetNearestWaterbodyFromOSM(centroid);

            if (nearestWaterbody == null)
            {
                return BadRequest("En yakın su kaynağı bulunamadı.");
            }

            var distance = await CalculateDistanceInMeters(centroid, nearestWaterbody);
            return Ok(new { DistanceInMeters = distance });
        }




        [HttpPost("CalculateNearestWaterbody")]
        public async Task<IActionResult> CalculateNearestWaterbody([FromBody] JObject geoJson)
        {
            _logger.LogInformation("Received GeoJSON: {GeoJson}", geoJson.ToString());

            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geometry = reader.Read<Polygon>(geoJson["geometry"].ToString());

            if (geometry == null)
            {
                _logger.LogError("Invalid geometry");
                return BadRequest("Invalid geometry");
            }

            var centroid = geometry.Centroid;
            _logger.LogInformation("Polygon Centroid: {Centroid}", centroid.ToString());

            var nearestWaterbody = await GetNearestWaterbodyFromOSM(centroid);

            if (nearestWaterbody == null)
            {
                return BadRequest("En yakın su kaynağı bulunamadı.");
            }

            var distance = await CalculateDistanceInMeters(centroid, nearestWaterbody);

            return Ok(new
            {
                NearestWaterbody = new { nearestWaterbody.X, nearestWaterbody.Y },
                DistanceInMeters = distance
            });
        }


        private async Task<Point> GetNearestWaterbodyFromOSM(Point centroid)
        {
            using var httpClient = new HttpClient();
            int radius = 1000; // Start radius
            const int maxRadius = 50000; // Maximum radius
            const int increment = 1000; // Increment amount
            Point nearestWaterbody = null;
            double minDistance = double.MaxValue;

            while (radius <= maxRadius)
            {
                string osmApiUrl = $"https://overpass-api.de/api/interpreter?data=[out:json];(way(around:{radius},{centroid.Y},{centroid.X})[natural=coastline];way(around:{radius},{centroid.Y},{centroid.X})[waterway=river];way(around:{radius},{centroid.Y},{centroid.X})[waterway=stream];);out geom;";

                _logger.LogInformation($"OSM API URL: {osmApiUrl}");

                var response = await httpClient.GetStringAsync(osmApiUrl);
                var osmData = JObject.Parse(response);

                _logger.LogInformation($"OSM API Response: {osmData}");

                if (osmData["elements"]?.HasValues ?? false)
                {
                    foreach (var element in osmData["elements"])
                    {
                        if (element["geometry"] != null)
                        {
                            foreach (var coord in element["geometry"])
                            {
                                var waterbodyPoint = new Point((double)coord["lon"], (double)coord["lat"]);
                                var distance = CalculateDistance(centroid, waterbodyPoint);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestWaterbody = waterbodyPoint;
                                }
                            }
                        }
                    }
                    if (nearestWaterbody != null) break; // Break loop if waterbody is found
                }

                radius += increment;
            }

            if (nearestWaterbody == null)
            {
                _logger.LogWarning("No nearest waterbody found.");
            }

            return nearestWaterbody;
        }


        private double CalculateDistance(Point point1, Point point2)
        {
            var earthRadiusKm = 6371.0;

            var dLat = DegreesToRadians(point2.Y - point1.Y);
            var dLon = DegreesToRadians(point2.X - point1.X);

            var lat1 = DegreesToRadians(point1.Y);
            var lat2 = DegreesToRadians(point2.Y);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c * 1000; // Mesafeyi metre cinsinden döndür
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private async Task<double> CalculateDistanceInMeters(Point centroid, Point nearestWaterbody)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("sqlConnection"));
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                SELECT ST_Distance(
                    ST_Transform(ST_SetSRID(ST_MakePoint(@x1, @y1), 4326), 3857),
                    ST_Transform(ST_SetSRID(ST_MakePoint(@x2, @y2), 4326), 3857)
                )", conn);
            cmd.Parameters.AddWithValue("x1", centroid.X);
            cmd.Parameters.AddWithValue("y1", centroid.Y);
            cmd.Parameters.AddWithValue("x2", nearestWaterbody.X);
            cmd.Parameters.AddWithValue("y2", nearestWaterbody.Y);

            return (double)await cmd.ExecuteScalarAsync();
        }
    }
}
