using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmaGestion.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Despachos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Total = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despachos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CodigoBarras = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Precio = table.Column<double>(type: "REAL", nullable: false),
                    Existencias = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaUltimaActualizacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetallesDespacho",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DespachoId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioHistorico = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDespacho", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesDespacho_Despachos_DespachoId",
                        column: x => x.DespachoId,
                        principalTable: "Despachos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesDespacho_Medicamentos_MedicamentoId",
                        column: x => x.MedicamentoId,
                        principalTable: "Medicamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDespacho_DespachoId",
                table: "DetallesDespacho",
                column: "DespachoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDespacho_MedicamentoId",
                table: "DetallesDespacho",
                column: "MedicamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Medicamentos_CodigoBarras",
                table: "Medicamentos",
                column: "CodigoBarras",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesDespacho");

            migrationBuilder.DropTable(
                name: "Despachos");

            migrationBuilder.DropTable(
                name: "Medicamentos");
        }
    }
}
