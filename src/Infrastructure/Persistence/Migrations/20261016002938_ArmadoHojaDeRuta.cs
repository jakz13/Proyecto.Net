using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArmadoHojaDeRuta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionEnvioRuta_HojasDeRuta_HojaDeRutaId",
                table: "AsignacionEnvioRuta");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AsignacionEnvioRuta",
                table: "AsignacionEnvioRuta");

            migrationBuilder.RenameTable(
                name: "AsignacionEnvioRuta",
                newName: "AsignacionesEnvioRuta");

            migrationBuilder.RenameIndex(
                name: "IX_AsignacionEnvioRuta_HojaDeRutaId",
                table: "AsignacionesEnvioRuta",
                newName: "IX_AsignacionesEnvioRuta_HojaDeRutaId");

            migrationBuilder.AddColumn<int>(
                name: "MaxParadasPorRuta",
                table: "ReglasOperativas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaLiberacion",
                table: "HojasDeRuta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AsignacionesEnvioRuta",
                table: "AsignacionesEnvioRuta",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FranjasHorarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranjasHorarias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repartidores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehiculoAsignadoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repartidores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Patente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CapacidadPeso = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CapacidadVolumen = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HojasDeRuta_OperadorId_Fecha",
                table: "HojasDeRuta",
                columns: new[] { "OperadorId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesEnvioRuta_EnvioId",
                table: "AsignacionesEnvioRuta",
                column: "EnvioId",
                unique: true,
                filter: "\"Vigente\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FranjasHorarias_ZonaCoberturaId",
                table: "FranjasHorarias",
                column: "ZonaCoberturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Repartidores_OperadorId",
                table: "Repartidores",
                column: "OperadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_OperadorId_Patente",
                table: "Vehiculos",
                columns: new[] { "OperadorId", "Patente" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesEnvioRuta_HojasDeRuta_HojaDeRutaId",
                table: "AsignacionesEnvioRuta",
                column: "HojaDeRutaId",
                principalTable: "HojasDeRuta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesEnvioRuta_HojasDeRuta_HojaDeRutaId",
                table: "AsignacionesEnvioRuta");

            migrationBuilder.DropTable(
                name: "FranjasHorarias");

            migrationBuilder.DropTable(
                name: "Repartidores");

            migrationBuilder.DropTable(
                name: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_HojasDeRuta_OperadorId_Fecha",
                table: "HojasDeRuta");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AsignacionesEnvioRuta",
                table: "AsignacionesEnvioRuta");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesEnvioRuta_EnvioId",
                table: "AsignacionesEnvioRuta");

            migrationBuilder.DropColumn(
                name: "MaxParadasPorRuta",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "FechaLiberacion",
                table: "HojasDeRuta");

            migrationBuilder.RenameTable(
                name: "AsignacionesEnvioRuta",
                newName: "AsignacionEnvioRuta");

            migrationBuilder.RenameIndex(
                name: "IX_AsignacionesEnvioRuta_HojaDeRutaId",
                table: "AsignacionEnvioRuta",
                newName: "IX_AsignacionEnvioRuta_HojaDeRutaId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AsignacionEnvioRuta",
                table: "AsignacionEnvioRuta",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionEnvioRuta_HojasDeRuta_HojaDeRutaId",
                table: "AsignacionEnvioRuta",
                column: "HojaDeRutaId",
                principalTable: "HojasDeRuta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
