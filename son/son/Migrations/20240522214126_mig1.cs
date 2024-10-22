using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace son.Migrations
{
    public partial class mig1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fKey = table.Column<int>(type: "integer", nullable: true),
                    Blok = table.Column<string>(type: "text", nullable: true),
                    Nitelik = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parcels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParselNo = table.Column<int>(type: "integer", nullable: true),
                    Pafta = table.Column<string>(type: "text", nullable: true),
                    Ada = table.Column<int>(type: "integer", nullable: true),
                    il = table.Column<string>(type: "text", nullable: true),
                    ilce = table.Column<string>(type: "text", nullable: true),
                    mahalle = table.Column<string>(type: "text", nullable: true),
                    nitelik = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Buildings",
                columns: new[] { "Id", "Blok", "Nitelik", "fKey" },
                values: new object[,]
                {
                    { 1, "b1", "n1", 1 },
                    { 2, "b2", "n2", 2 }
                });

            migrationBuilder.InsertData(
                table: "Parcels",
                columns: new[] { "Id", "Ada", "Pafta", "ParselNo", "il", "ilce", "mahalle", "nitelik" },
                values: new object[,]
                {
                    { 1, 1, "P1", 101, "Ankara", "Çankaya", "Kızılay", "Residential" },
                    { 2, 2, "P2", 102, "Ankara", "Çankaya", "Bahçelievler", "Commercial" }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "Parcels");
        }
    }
}
