using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmaGestion.Migrations
{
    /// <inheritdoc />
    public partial class AddLoteAndFechaVencimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimiento",
                table: "Medicamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lote",
                table: "Medicamentos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                table: "Medicamentos");

            migrationBuilder.DropColumn(
                name: "Lote",
                table: "Medicamentos");
        }
    }
}
