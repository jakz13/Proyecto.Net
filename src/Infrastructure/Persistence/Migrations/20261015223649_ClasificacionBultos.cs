using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClasificacionBultos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
