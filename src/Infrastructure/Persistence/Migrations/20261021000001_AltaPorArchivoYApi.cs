using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AltaPorArchivoYApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaExterna",
                table: "Envios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LotesImportacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaProcesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CantidadRegistros = table.Column<int>(type: "integer", nullable: false),
                    CantidadExitosos = table.Column<int>(type: "integer", nullable: false),
                    CantidadConError = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesImportacion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ComercioOperadorId_ReferenciaExterna",
                table: "Envios",
                columns: new[] { "ComercioOperadorId", "ReferenciaExterna" },
                unique: true,
                filter: "\"ReferenciaExterna\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LotesImportacion_ComercioOperadorId_ClaveIdempotencia",
                table: "LotesImportacion",
                columns: new[] { "ComercioOperadorId", "ClaveIdempotencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotesImportacion");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ComercioOperadorId_ReferenciaExterna",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "ReferenciaExterna",
                table: "Envios");
        }
    }
}
