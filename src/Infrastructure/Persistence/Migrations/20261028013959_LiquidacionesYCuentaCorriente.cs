using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiquidacionesYCuentaCorriente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasCorrientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SelloModificacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasCorrientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Liquidaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodoDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Liquidaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCuentaCorriente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCorrienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiquidacionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCuentaCorriente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCuentaCorriente_CuentasCorrientes_CuentaCorrient~",
                        column: x => x.CuentaCorrienteId,
                        principalTable: "CuentasCorrientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasCorrientes_ComercioOperadorId",
                table: "CuentasCorrientes",
                column: "ComercioOperadorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Liquidaciones_ComercioOperadorId_PeriodoDesde",
                table: "Liquidaciones",
                columns: new[] { "ComercioOperadorId", "PeriodoDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_CuentaCorrienteId_Fecha",
                table: "MovimientosCuentaCorriente",
                columns: new[] { "CuentaCorrienteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_EnvioId",
                table: "MovimientosCuentaCorriente",
                column: "EnvioId",
                unique: true,
                filter: "\"EnvioId\" IS NOT NULL AND \"LiquidacionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Liquidaciones");

            migrationBuilder.DropTable(
                name: "MovimientosCuentaCorriente");

            migrationBuilder.DropTable(
                name: "CuentasCorrientes");
        }
    }
}
