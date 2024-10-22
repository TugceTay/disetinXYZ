using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace son.Models
{
    [Table("raster_table")]
    public class RasterTable
    {
        [Key]
        [Column("rid")]
        public int Rid { get; set; }

        [Column("rast")]
        public byte[] Rast { get; set; }
    }
}
