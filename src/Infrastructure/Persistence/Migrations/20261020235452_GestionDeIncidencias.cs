using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GestionDeIncidencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: true),
                    HojaDeRutaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResponsableUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadaPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaApertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Resolucion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidencias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotasIncidencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidenciaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasIncidencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasIncidencia_Incidencias_IncidenciaId",
                        column: x => x.IncidenciaId,
                        principalTable: "Incidencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_OperadorId_Estado",
                table: "Incidencias",
                columns: new[] { "OperadorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasIncidencia_IncidenciaId",
                table: "NotasIncidencia",
                column: "IncidenciaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasIncidencia");

            migrationBuilder.DropTable(
                name: "Incidencias");
        }
    }
}
