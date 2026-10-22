using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdministracionDelOperador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comentario",
                table: "ReglasOperativas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAlta",
                table: "MotivosNoEntrega",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<int>(
                name: "CapacidadMaximaEnvios",
                table: "FranjasHorarias",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Comentario",
                table: "CuadrosTarifarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Comercios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NombreFantasia",
                table: "Comercios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SelloModificacion",
                table: "Comercios",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "IdentidadesVisuales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ColorPrimario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ColorSecundario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmailContacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TelefonoContacto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SitioWeb = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MensajeComunicaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentidadesVisuales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MensajesFallidosCola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoMensaje = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadOriginal = table.Column<string>(type: "text", nullable: false),
                    Excepcion = table.Column<string>(type: "text", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    PrimerFallo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoFallo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ColaOrigen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesFallidosCola", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Perfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComercioId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerfilId = table.Column<Guid>(type: "uuid", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZonasCobertura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PoligonoGeografico = table.Column<string>(type: "text", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZonasCobertura", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerfilesPermisos",
                columns: table => new
                {
                    PerfilId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermisoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilesPermisos", x => new { x.PerfilId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_PerfilesPermisos_Perfiles_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "Perfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentidadesVisuales_OperadorId",
                table: "IdentidadesVisuales",
                column: "OperadorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesFallidosCola_Estado",
                table: "MensajesFallidosCola",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Perfiles_Nombre",
                table: "Perfiles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Codigo",
                table: "Permisos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZonasCobertura_OperadorId",
                table: "ZonasCobertura",
                column: "OperadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaCoberturaId",
                table: "FranjasHorarias",
                column: "ZonaCoberturaId",
                principalTable: "ZonasCobertura",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaCoberturaId",
                table: "FranjasHorarias");

            migrationBuilder.DropTable(
                name: "IdentidadesVisuales");

            migrationBuilder.DropTable(
                name: "MensajesFallidosCola");

            migrationBuilder.DropTable(
                name: "PerfilesPermisos");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "ZonasCobertura");

            migrationBuilder.DropTable(
                name: "Perfiles");

            migrationBuilder.DropColumn(
                name: "Comentario",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "FechaAlta",
                table: "MotivosNoEntrega");

            migrationBuilder.DropColumn(
                name: "CapacidadMaximaEnvios",
                table: "FranjasHorarias");

            migrationBuilder.DropColumn(
                name: "Comentario",
                table: "CuadrosTarifarios");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Comercios");

            migrationBuilder.DropColumn(
                name: "NombreFantasia",
                table: "Comercios");

            migrationBuilder.DropColumn(
                name: "SelloModificacion",
                table: "Comercios");
        }
    }
}
