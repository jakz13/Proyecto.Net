using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperacionEnvioRutaDespacho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CapacidadVolumen",
                table: "Vehiculos",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CapacidadPeso",
                table: "Vehiculos",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

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

            migrationBuilder.AlterColumn<decimal>(
                name: "TarifaCalculada",
                table: "Envios",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "ModalidadServicio",
                table: "Envios",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "OperadorId",
                table: "Envios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaClasificacion",
                table: "Bultos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prioridad",
                table: "Bultos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoClasificacion",
                table: "Bultos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZonaClasificadaId",
                table: "Bultos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CuadrosTarifarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VigenteHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuadrosTarifarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinatarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinatarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Direcciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    Referencia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Direcciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscrepanciaRecepcion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoDiscrepancia = table.Column<string>(type: "text", nullable: false),
                    Detalle = table.Column<string>(type: "text", nullable: false),
                    FechaDeteccion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioDeposito = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscrepanciaRecepcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscrepanciaRecepcion_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "ReglasTarifa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuadroTarifarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModalidadServicio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PesoDesde = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PesoHasta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    VolumenDesde = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    VolumenHasta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PrecioBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Recargo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Bonificacion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasTarifa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglasTarifa_CuadrosTarifarios_CuadroTarifarioId",
                        column: x => x.CuadroTarifarioId,
                        principalTable: "CuadrosTarifarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_OperadorId_Patente",
                table: "Vehiculos",
                columns: new[] { "OperadorId", "Patente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repartidores_OperadorId",
                table: "Repartidores",
                column: "OperadorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasDeRuta_OperadorId_Fecha",
                table: "HojasDeRuta",
                columns: new[] { "OperadorId", "Fecha" });

            // Los envíos creados antes de existir el código público quedaron con el valor vacío: se les asigna uno propio
            // para que el índice único pueda crearse sin descartar datos
            migrationBuilder.Sql(
                "UPDATE \"Envios\" SET \"CodigoSeguimientoPublico\" = 'ENV-LEGACY-' || upper(substr(replace(\"Id\"::text, '-', ''), 1, 12)) " +
                "WHERE \"CodigoSeguimientoPublico\" IS NULL OR \"CodigoSeguimientoPublico\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CodigoSeguimientoPublico",
                table: "Envios",
                column: "CodigoSeguimientoPublico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_OperadorId",
                table: "Envios",
                column: "OperadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesEnvioRuta_EnvioId",
                table: "AsignacionesEnvioRuta",
                column: "EnvioId",
                unique: true,
                filter: "\"Vigente\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CuadrosTarifarios_OperadorId_Version",
                table: "CuadrosTarifarios",
                columns: new[] { "OperadorId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepanciaRecepcion_EnvioId",
                table: "DiscrepanciaRecepcion",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosCargaVehiculo_HojaDeRutaId_BultoId",
                table: "EscaneosCargaVehiculo",
                columns: new[] { "HojaDeRutaId", "BultoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranjasHorarias_ZonaCoberturaId",
                table: "FranjasHorarias",
                column: "ZonaCoberturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasTarifa_CuadroTarifarioId",
                table: "ReglasTarifa",
                column: "CuadroTarifarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Destinatarios");

            migrationBuilder.DropTable(
                name: "Direcciones");

            migrationBuilder.DropTable(
                name: "DiscrepanciaRecepcion");

            migrationBuilder.DropTable(
                name: "EscaneosCargaVehiculo");

            migrationBuilder.DropTable(
                name: "FranjasHorarias");

            migrationBuilder.DropTable(
                name: "ReglasTarifa");

            migrationBuilder.DropTable(
                name: "CuadrosTarifarios");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_OperadorId_Patente",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Repartidores_OperadorId",
                table: "Repartidores");

            migrationBuilder.DropIndex(
                name: "IX_HojasDeRuta_OperadorId_Fecha",
                table: "HojasDeRuta");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CodigoSeguimientoPublico",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_OperadorId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesEnvioRuta_EnvioId",
                table: "AsignacionesEnvioRuta");

            migrationBuilder.DropColumn(
                name: "MaxParadasPorRuta",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "FechaLiberacion",
                table: "HojasDeRuta");

            migrationBuilder.DropColumn(
                name: "OperadorId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "FechaClasificacion",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "Prioridad",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "TipoClasificacion",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "ZonaClasificadaId",
                table: "Bultos");

            migrationBuilder.AlterColumn<decimal>(
                name: "CapacidadVolumen",
                table: "Vehiculos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "CapacidadPeso",
                table: "Vehiculos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "TarifaCalculada",
                table: "Envios",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "ModalidadServicio",
                table: "Envios",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
