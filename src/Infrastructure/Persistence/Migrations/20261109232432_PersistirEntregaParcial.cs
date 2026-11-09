using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistirEntregaParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VersionContrato",
                table: "SuscripcionesAviso",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "v1");

            migrationBuilder.AddColumn<bool>(
                name: "AdmiteEntregaParcial",
                table: "ReglasOperativas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PoliticaEntregaParcial",
                table: "ReglasOperativas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NuevaVisitaSinRecargo");

            migrationBuilder.AddColumn<string>(
                name: "CodigosBultosEntregados",
                table: "PruebasDeEntrega",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotivoNoEntregaCodigo",
                table: "Bultos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PruebaDeEntregaId",
                table: "Bultos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConflictosSincronizacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntidadAfectada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntidadId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoServidor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EstadoDispositivo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoliticaAplicada = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resolucion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictosSincronizacion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictosSincronizacion_EntidadAfectada_EntidadId",
                table: "ConflictosSincronizacion",
                columns: new[] { "EntidadAfectada", "EntidadId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConflictosSincronizacion");

            migrationBuilder.DropColumn(
                name: "VersionContrato",
                table: "SuscripcionesAviso");

            migrationBuilder.DropColumn(
                name: "AdmiteEntregaParcial",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "PoliticaEntregaParcial",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "CodigosBultosEntregados",
                table: "PruebasDeEntrega");

            migrationBuilder.DropColumn(
                name: "MotivoNoEntregaCodigo",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "PruebaDeEntregaId",
                table: "Bultos");
        }
    }
}
