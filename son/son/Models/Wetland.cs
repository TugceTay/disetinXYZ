using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace son.Models
{
    [Table("wetlands")]
    public class Wetland
    {
        [Key]
        [Column("id_0")]
        public int Id { get; set; }

        [Column("geom")]
        public Geometry Geom { get; set; }

        [Column("id")]
        public long WetlandId { get; set; }

        [Column("ad")]
        public string Name { get; set; }
    }
}
