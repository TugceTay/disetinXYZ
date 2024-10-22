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
    public class FaultLineController : ControllerBase
    {
        private readonly RepositoryContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FaultLineController> _logger;

        public FaultLineController(RepositoryContext context, IConfiguration configuration, ILogger<FaultLineController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("CalculateNearestFault")]
        public async Task<IActionResult> CalculateNearestFault([FromBody] JObject geoJson)
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

            using var cmd = new NpgsqlCommand("SELECT * FROM calculate_nearest_fault(@polygon_geom)", conn);
            cmd.Parameters.AddWithValue("polygon_geom", geometry);

            using var dbReader = await cmd.ExecuteReaderAsync();
            if (dbReader.Read())
            {
                var result = new
                {
                    FaultDistance = dbReader.GetDouble(0)
                };

                return Ok(result);
            }

            return BadRequest("Fay hattı mesafesi hesaplanamadı.");
        }
    }
}
