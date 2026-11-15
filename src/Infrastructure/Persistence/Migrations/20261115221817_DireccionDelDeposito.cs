using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DireccionDelDeposito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DepositoLatitud",
                table: "IdentidadesVisuales",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DepositoLongitud",
                table: "IdentidadesVisuales",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionDeposito",
                table: "IdentidadesVisuales",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositoLatitud",
                table: "IdentidadesVisuales");

            migrationBuilder.DropColumn(
                name: "DepositoLongitud",
                table: "IdentidadesVisuales");

            migrationBuilder.DropColumn(
                name: "DireccionDeposito",
                table: "IdentidadesVisuales");
        }
    }
}
