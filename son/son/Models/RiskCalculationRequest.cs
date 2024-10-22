using NetTopologySuite.Geometries;
using Newtonsoft.Json;

namespace son.Models
{
    public class RiskCalculationRequest
    {
        [JsonProperty("geometry")]
        public Geometry Geometry { get; set; }

        [JsonProperty("properties")]
        public RiskProperties Properties { get; set; }
    }

    public class RiskProperties
    {
        [JsonProperty("zeminSinifi")]
        public int ZeminSinifi { get; set; }

        [JsonProperty("toprakTipi")]
        public int ToprakTipi { get; set; }

        [JsonProperty("yagis")]
        public int Yagis { get; set; }

        [JsonProperty("bitkiOrtusu")]
        public int BitkiOrtusu { get; set; }

        [JsonProperty("litoloji")]
        public int Litoloji { get; set; }
    }
}
