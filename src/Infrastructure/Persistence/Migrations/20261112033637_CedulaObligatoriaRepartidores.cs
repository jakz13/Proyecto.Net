using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CedulaObligatoriaRepartidores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los repartidores sin cédula válida (vacía, "S/D" o con menos de 8 dígitos) reciben una provisoria de 8 dígitos;
            // desde Flota se corrige con la real. La cédula pasa a ser obligatoria en el dominio y en la API.
            migrationBuilder.Sql("""
                UPDATE "Repartidores"
                SET "Documento" = (floor(random() * 90000000) + 10000000)::bigint::text
                WHERE length(regexp_replace(coalesce("Documento", ''), '[^0-9]', '', 'g')) < 8;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "IdentidadesVisuales",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "IdentidadesVisuales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
