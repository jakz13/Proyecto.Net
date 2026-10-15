using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AltaEnvioConTarifa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FechaCreacion",
                table: "Envios",
                newName: "FechaAlta");

            migrationBuilder.RenameColumn(
                name: "Estado",
                table: "Envios",
                newName: "ModalidadServicio");

            migrationBuilder.RenameColumn(
                name: "ComercioId",
                table: "Envios",
                newName: "SelloModificacion");

            migrationBuilder.AddColumn<string>(
                name: "CodigoSeguimientoPublico",
                table: "Envios",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ComercioOperadorId",
                table: "Envios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DestinatarioId",
                table: "Envios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DireccionEntregaId",
                table: "Envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoActual",
                table: "Envios",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FranjaHorariaComprometidaId",
                table: "Envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HojaDeRutaId",
                table: "Envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LoteImportacionId",
                table: "Envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReglaTarifaAplicadaId",
                table: "Envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaCalculada",
                table: "Envios",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Bultos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificacion = table.Column<string>(type: "text", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: false),
                    Alto = table.Column<decimal>(type: "numeric", nullable: false),
                    Ancho = table.Column<decimal>(type: "numeric", nullable: false),
                    Profundo = table.Column<decimal>(type: "numeric", nullable: false),
                    EstadoBulto = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bultos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bultos_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "EventosEnvio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    ResponsableUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosEnvio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosEnvio_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HojasDeRuta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehiculoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaDespacho = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SelloModificacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HojasDeRuta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentosEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MotivoNoEntregaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    OrigenDatos = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosEntrega_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MotivosNoEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosNoEntrega", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgregadoOrigen = table.Column<string>(type: "text", nullable: false),
                    AgregadoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Procesado = table.Column<bool>(type: "boolean", nullable: false),
                    ProcesadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReglasOperativas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VigenteHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxIntentosEntrega = table.Column<int>(type: "integer", nullable: false),
                    PlazoEntreIntentos = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TipoPruebaEntregaExigida = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PoliticaDevolucion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PlazoComprometidoEstandar = table.Column<TimeSpan>(type: "interval", nullable: false),
                    PlazoComprometidoUrgente = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasOperativas", x => x.Id);
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

            migrationBuilder.CreateTable(
                name: "AsignacionEnvioRuta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DespachadorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vigente = table.Column<bool>(type: "boolean", nullable: false),
                    SelloModificacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionEnvioRuta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionEnvioRuta_HojasDeRuta_HojaDeRutaId",
                        column: x => x.HojaDeRutaId,
                        principalTable: "HojasDeRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Paradas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    DireccionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paradas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paradas_HojasDeRuta_HojaDeRutaId",
                        column: x => x.HojaDeRutaId,
                        principalTable: "HojasDeRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PruebasDeEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentoEntregaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirmaImagenUrl = table.Column<string>(type: "text", nullable: true),
                    FotoUrl = table.Column<string>(type: "text", nullable: true),
                    NombreReceptor = table.Column<string>(type: "text", nullable: true),
                    DocumentoReceptor = table.Column<string>(type: "text", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    TimestampDispositivo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PruebasDeEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PruebasDeEntrega_IntentosEntrega_IntentoEntregaId",
                        column: x => x.IntentoEntregaId,
                        principalTable: "IntentosEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CodigoSeguimientoPublico",
                table: "Envios",
                column: "CodigoSeguimientoPublico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionEnvioRuta_HojaDeRutaId",
                table: "AsignacionEnvioRuta",
                column: "HojaDeRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_Bultos_EnvioId",
                table: "Bultos",
                column: "EnvioId");

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
                name: "IX_EventosEnvio_EnvioId",
                table: "EventosEnvio",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_EnvioId",
                table: "IntentosEntrega",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_HojaDeRutaId",
                table: "Paradas",
                column: "HojaDeRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_PruebasDeEntrega_IntentoEntregaId",
                table: "PruebasDeEntrega",
                column: "IntentoEntregaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasTarifa_CuadroTarifarioId",
                table: "ReglasTarifa",
                column: "CuadroTarifarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionEnvioRuta");

            migrationBuilder.DropTable(
                name: "Bultos");

            migrationBuilder.DropTable(
                name: "Destinatarios");

            migrationBuilder.DropTable(
                name: "Direcciones");

            migrationBuilder.DropTable(
                name: "DiscrepanciaRecepcion");

            migrationBuilder.DropTable(
                name: "EventosEnvio");

            migrationBuilder.DropTable(
                name: "MotivosNoEntrega");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Paradas");

            migrationBuilder.DropTable(
                name: "PruebasDeEntrega");

            migrationBuilder.DropTable(
                name: "ReglasOperativas");

            migrationBuilder.DropTable(
                name: "ReglasTarifa");

            migrationBuilder.DropTable(
                name: "HojasDeRuta");

            migrationBuilder.DropTable(
                name: "IntentosEntrega");

            migrationBuilder.DropTable(
                name: "CuadrosTarifarios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CodigoSeguimientoPublico",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "CodigoSeguimientoPublico",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "ComercioOperadorId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "DestinatarioId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "DireccionEntregaId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "EstadoActual",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "FranjaHorariaComprometidaId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "HojaDeRutaId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "LoteImportacionId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "ReglaTarifaAplicadaId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "TarifaCalculada",
                table: "Envios");

            migrationBuilder.RenameColumn(
                name: "SelloModificacion",
                table: "Envios",
                newName: "ComercioId");

            migrationBuilder.RenameColumn(
                name: "ModalidadServicio",
                table: "Envios",
                newName: "Estado");

            migrationBuilder.RenameColumn(
                name: "FechaAlta",
                table: "Envios",
                newName: "FechaCreacion");
        }
    }
}
