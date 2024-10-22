using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json.Linq;
using son.Models;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace son.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RasterController : ControllerBase
    {
        private readonly RepositoryContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RasterController> _logger;
       

        public RasterController(RepositoryContext context, IConfiguration configuration, ILogger<RasterController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;
        }

        [HttpPost("CalculateSlope")]
        public async Task<IActionResult> CalculateSlope([FromBody] JObject geoJson)
        {
            _logger.LogInformation("Received GeoJSON: {GeoJson}", geoJson.ToString());

            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geometry = reader.Read<Polygon>(geoJson["geometry"].ToString());

            if (geometry == null)
            {
                _logger.LogError("Invalid geometry");
                return BadRequest("Invalid geometry");
            }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("sqlConnection"));
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM calculate_slope(@polygon_geom)", conn);
            cmd.Parameters.AddWithValue("polygon_geom", geometry);

            using var dbReader = await cmd.ExecuteReaderAsync();
            if (dbReader.Read())
            {
                var result = new
                {
                    AvgSlopePercentage = dbReader.GetDouble(0),
                    AvgSlopeDegrees = dbReader.GetDouble(1)
                };

                return Ok(result);
            }

            return BadRequest("Eğim hesaplanamadı.");
        }
        [HttpPost("CalculateHeight")]
        public async Task<IActionResult> CalculateHeight([FromBody] JObject geoJson)
        {
            _logger.LogInformation("Received GeoJSON: {GeoJson}", geoJson.ToString());

            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geometry = reader.Read<Polygon>(geoJson["geometry"].ToString());

            if (geometry == null)
            {
                _logger.LogError("Invalid geometry");
                return BadRequest("Invalid geometry");
            }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("sqlConnection"));
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM calculate_height(@polygon_geom)", conn);
            cmd.Parameters.AddWithValue("polygon_geom", geometry);

            using var dbReader = await cmd.ExecuteReaderAsync();
            if (dbReader.Read())
            {
                var result = new
                {
                    AvgHeight = dbReader.GetDouble(0)
                };

                return Ok(result);
            }

            return BadRequest("Yükseklik hesaplanamadı.");
        }

    }
}
