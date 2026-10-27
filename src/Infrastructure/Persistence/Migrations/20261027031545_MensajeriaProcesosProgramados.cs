using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MensajeriaProcesosProgramados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisosComercio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SuscripcionAvisoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    FirmaEnviada = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    ProximoReintento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimaRespuestaHttp = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosComercio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CierresDiariosOperacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    ResumenJson = table.Column<string>(type: "text", nullable: false),
                    EjecutadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresDiariosOperacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClavesIdempotenciaConsumidor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MensajeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcesadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClavesIdempotenciaConsumidor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndicadoresCumplimiento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaCoberturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Periodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalEnvios = table.Column<int>(type: "integer", nullable: false),
                    EntregadosEnPlazo = table.Column<int>(type: "integer", nullable: false),
                    EntregadosFueraDePlazo = table.Column<int>(type: "integer", nullable: false),
                    NoEntregados = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeCumplimiento = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicadoresCumplimiento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstadoEnvio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuscripcionesAviso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UrlWebhook = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClaveFirma = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesAviso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisosComercio_EnvioId",
                table: "AvisosComercio",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisosComercio_Estado_ProximoReintento",
                table: "AvisosComercio",
                columns: new[] { "Estado", "ProximoReintento" });

            migrationBuilder.CreateIndex(
                name: "IX_AvisosComercio_SuscripcionAvisoId",
                table: "AvisosComercio",
                column: "SuscripcionAvisoId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiariosOperacion_OperadorId_Fecha",
                table: "CierresDiariosOperacion",
                columns: new[] { "OperadorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClavesIdempotenciaConsumidor_MensajeId",
                table: "ClavesIdempotenciaConsumidor",
                column: "MensajeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndicadoresCumplimiento_OperadorId_Periodo",
                table: "IndicadoresCumplimiento",
                columns: new[] { "OperadorId", "Periodo" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_EnvioId",
                table: "Notificaciones",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesAviso_ComercioOperadorId_Activa",
                table: "SuscripcionesAviso",
                columns: new[] { "ComercioOperadorId", "Activa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvisosComercio");

            migrationBuilder.DropTable(
                name: "CierresDiariosOperacion");

            migrationBuilder.DropTable(
                name: "ClavesIdempotenciaConsumidor");

            migrationBuilder.DropTable(
                name: "IndicadoresCumplimiento");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "SuscripcionesAviso");
        }
    }
}
