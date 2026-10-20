using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RendicionDeRuta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsInterno",
                table: "MotivosNoEntrega",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRendicion",
                table: "HojasDeRuta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RendicionesDeposito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BultosEntregados = table.Column<int>(type: "integer", nullable: false),
                    BultosDevueltos = table.Column<int>(type: "integer", nullable: false),
                    BultosPendientes = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RendicionesDeposito", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RendicionesDeposito_HojaDeRutaId",
                table: "RendicionesDeposito",
                column: "HojaDeRutaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RendicionesDeposito");

            migrationBuilder.DropColumn(
                name: "EsInterno",
                table: "MotivosNoEntrega");

            migrationBuilder.DropColumn(
                name: "FechaRendicion",
                table: "HojasDeRuta");
        }
    }
}
