using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DespachoYCargaEscaneada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EscaneosCargaVehiculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscaneosCargaVehiculo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscaneosCargaVehiculo_HojasDeRuta_HojaDeRutaId",
                        column: x => x.HojaDeRutaId,
                        principalTable: "HojasDeRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosCargaVehiculo_HojaDeRutaId_BultoId",
                table: "EscaneosCargaVehiculo",
                columns: new[] { "HojaDeRutaId", "BultoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscaneosCargaVehiculo");
        }
    }
}
