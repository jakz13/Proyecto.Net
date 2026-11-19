using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrazabilidadReglaEnIntentoEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReglaOperativaId",
                table: "IntentosEntrega",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_ReglaOperativaId",
                table: "IntentosEntrega",
                column: "ReglaOperativaId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_ReglasOperativas_ReglaOperativaId",
                table: "IntentosEntrega",
                column: "ReglaOperativaId",
                principalTable: "ReglasOperativas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_ReglasOperativas_ReglaOperativaId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_ReglaOperativaId",
                table: "IntentosEntrega");

            migrationBuilder.DropColumn(
                name: "ReglaOperativaId",
                table: "IntentosEntrega");
        }
    }
}
