using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetiroEnDomicilio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "EnvioId",
                table: "Paradas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "RetiroId",
                table: "Paradas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Paradas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Entrega");

            migrationBuilder.CreateTable(
                name: "Retiros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioOperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoSeguimientoPublico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NombreContacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TelefonoContacto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmailContacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DireccionRetiroId = table.Column<Guid>(type: "uuid", nullable: false),
                    DireccionRetiro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaSolicitada = table.Column<DateOnly>(type: "date", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EstadoActual = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TarifaCalculada = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReglaTarifaAplicadaId = table.Column<Guid>(type: "uuid", nullable: true),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaRetiro = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NombreEntrega = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentoEntrega = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FotoEstadoBase64 = table.Column<string>(type: "text", nullable: true),
                    FirmaBase64 = table.Column<string>(type: "text", nullable: true),
                    LatitudRetiro = table.Column<double>(type: "double precision", nullable: true),
                    LongitudRetiro = table.Column<double>(type: "double precision", nullable: true),
                    MotivoNoRetiroCodigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SelloModificacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retiros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BultosRetiro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RetiroId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: false),
                    Alto = table.Column<decimal>(type: "numeric", nullable: false),
                    Ancho = table.Column<decimal>(type: "numeric", nullable: false),
                    Profundo = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BultosRetiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BultosRetiro_Retiros_RetiroId",
                        column: x => x.RetiroId,
                        principalTable: "Retiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventosRetiro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RetiroId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstadoAnterior = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResponsableUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosRetiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosRetiro_Retiros_RetiroId",
                        column: x => x.RetiroId,
                        principalTable: "Retiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_RetiroId",
                table: "Paradas",
                column: "RetiroId");

            migrationBuilder.CreateIndex(
                name: "IX_BultosRetiro_Identificacion",
                table: "BultosRetiro",
                column: "Identificacion");

            migrationBuilder.CreateIndex(
                name: "IX_BultosRetiro_RetiroId",
                table: "BultosRetiro",
                column: "RetiroId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosRetiro_RetiroId",
                table: "EventosRetiro",
                column: "RetiroId");

            migrationBuilder.CreateIndex(
                name: "IX_Retiros_CodigoSeguimientoPublico",
                table: "Retiros",
                column: "CodigoSeguimientoPublico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Retiros_ComercioOperadorId_ReferenciaExterna",
                table: "Retiros",
                columns: new[] { "ComercioOperadorId", "ReferenciaExterna" },
                unique: true,
                filter: "\"ReferenciaExterna\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Retiros_OperadorId_EstadoActual",
                table: "Retiros",
                columns: new[] { "OperadorId", "EstadoActual" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BultosRetiro");

            migrationBuilder.DropTable(
                name: "EventosRetiro");

            migrationBuilder.DropTable(
                name: "Retiros");

            migrationBuilder.DropIndex(
                name: "IX_Paradas_RetiroId",
                table: "Paradas");

            migrationBuilder.DropColumn(
                name: "RetiroId",
                table: "Paradas");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Paradas");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnvioId",
                table: "Paradas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
