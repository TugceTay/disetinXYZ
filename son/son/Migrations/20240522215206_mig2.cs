using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace son.Migrations
{
    public partial class mig2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<MultiPolygon>(
                name: "geom",
                table: "Parcels",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<MultiPolygon>(
                name: "geom",
                table: "Buildings",
                type: "geometry",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "geom",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "geom",
                table: "Buildings");
        }
    }
}
