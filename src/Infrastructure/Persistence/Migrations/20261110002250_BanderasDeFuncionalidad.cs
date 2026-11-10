using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BanderasDeFuncionalidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BanderasFuncionalidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Clave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    ModificadaPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadaPorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModificadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BanderasFuncionalidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistorialBanderas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Clave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<bool>(type: "boolean", nullable: false),
                    ValorNuevo = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialBanderas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BanderasFuncionalidad_OperadorId_Clave",
                table: "BanderasFuncionalidad",
                columns: new[] { "OperadorId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialBanderas_OperadorId_Fecha",
                table: "HistorialBanderas",
                columns: new[] { "OperadorId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BanderasFuncionalidad");

            migrationBuilder.DropTable(
                name: "HistorialBanderas");
        }
    }
}
