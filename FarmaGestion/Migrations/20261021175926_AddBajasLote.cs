using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmaGestion.Migrations
{
    /// <inheritdoc />
    public partial class AddBajasLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BajasLote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BajasLote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BajasLote_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BajasLote_Medicamentos_MedicamentoId",
                        column: x => x.MedicamentoId,
                        principalTable: "Medicamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BajasLote_Fecha",
                table: "BajasLote",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_BajasLote_LoteId",
                table: "BajasLote",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_BajasLote_MedicamentoId",
                table: "BajasLote",
                column: "MedicamentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BajasLote");
        }
    }
}
