using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Dos operadores no pueden llamarse igual (sin distinguir mayúsculas ni espacios de los bordes): en el selector y en la pantalla de funcionalidades
    /// de la plataforma no se distinguirían. Las bases ya cargadas tenían la semilla del segundo operador con el mismo nombre que la del primero.
    /// </summary>
    public partial class NombreComercialDeOperadorUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El operador de la semilla "Express" se llama igual que el de la semilla principal: recibe un nombre propio
            migrationBuilder.Sql(@"
                UPDATE ""Operadores""
                SET ""NombreComercial"" = 'TransLogística Express Norte'
                WHERE ""Id"" = 'b0000000-0000-0000-0000-000000000001'
                  AND EXISTS (SELECT 1 FROM ""Operadores"" o2 WHERE o2.""Id"" <> ""Operadores"".""Id""
                              AND lower(btrim(o2.""NombreComercial"")) = lower(btrim(""Operadores"".""NombreComercial"")));");

            // Cualquier otro nombre repetido se distingue (el más antiguo conserva el suyo) para que el índice único pueda crearse
            migrationBuilder.Sql(@"
                UPDATE ""Operadores"" o
                SET ""NombreComercial"" = btrim(o.""NombreComercial"") || ' (' || d.posicion || ')'
                FROM (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY lower(btrim(""NombreComercial"")) ORDER BY ""FechaAlta"", ""Id"") AS posicion
                    FROM ""Operadores""
                ) d
                WHERE o.""Id"" = d.""Id"" AND d.posicion > 1;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Operadores_NombreComercial"";");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_Operadores_NombreComercial_Unico"" ON ""Operadores"" (lower(btrim(""NombreComercial"")));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Operadores_NombreComercial_Unico"";");
        }
    }
}
