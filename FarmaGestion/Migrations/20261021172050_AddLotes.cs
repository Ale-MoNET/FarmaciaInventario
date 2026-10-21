using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmaGestion.Migrations
{
    /// <inheritdoc />
    public partial class AddLotes : Migration
    {
        /// <summary>
        /// Versión 1.1: las existencias pasan del producto a sus lotes.
        /// Orden importante: primero se crea la tabla de lotes y se copian los datos; solo
        /// después se quitan las columnas viejas, para no perder información.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MedicamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroLote = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Existencias = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaUltimaActualizacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lotes_Medicamentos_MedicamentoId",
                        column: x => x.MedicamentoId,
                        principalTable: "Medicamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_MedicamentoId_NumeroLote",
                table: "Lotes",
                columns: new[] { "MedicamentoId", "NumeroLote" },
                unique: true);

            // Migración de datos: cada producto con existencias se convierte en un lote con su
            // número de lote y vencimiento actuales (o "INICIAL" si no tenía lote registrado).
            // Los productos en cero no generan lote: el primero llegará por "Ingresar mercadería".
            migrationBuilder.Sql(@"
INSERT INTO Lotes (MedicamentoId, NumeroLote, FechaVencimiento, Existencias, FechaIngreso, FechaUltimaActualizacion)
SELECT Id,
       UPPER(COALESCE(NULLIF(TRIM(Lote), ''), 'INICIAL')),
       FechaVencimiento,
       Existencias,
       FechaUltimaActualizacion,
       FechaUltimaActualizacion
FROM Medicamentos
WHERE Existencias > 0;");

            // Se quitan las columnas viejas con ALTER TABLE nativo (SQLite >= 3.35) en lugar de
            // DropColumn, que reconstruye la tabla fuera de transacción.
            migrationBuilder.Sql("ALTER TABLE Medicamentos DROP COLUMN Existencias;");
            migrationBuilder.Sql("ALTER TABLE Medicamentos DROP COLUMN FechaVencimiento;");
            migrationBuilder.Sql("ALTER TABLE Medicamentos DROP COLUMN Lote;");

            // El detalle de despacho recuerda el lote de salida. La llave foránea se declara en el
            // mismo ADD COLUMN (SQLite lo permite con valor por defecto NULL); con AddForeignKey EF
            // reconstruiría la tabla fuera de transacción. Así toda la migración es atómica: o se
            // aplica completa o no deja rastro, lo que importa en la base compartida en red.
            migrationBuilder.Sql(
                "ALTER TABLE DetallesDespacho ADD COLUMN LoteId INTEGER NULL " +
                "CONSTRAINT FK_DetallesDespacho_Lotes_LoteId REFERENCES Lotes(Id) ON DELETE RESTRICT;");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDespacho_LoteId",
                table: "DetallesDespacho",
                column: "LoteId");
        }

        /// <summary>
        /// Vuelta atrás (mejor esfuerzo): el producto recupera la suma de existencias de sus
        /// lotes y el lote/vencimiento del que vence primero. Se pierde el detalle por lote.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Existencias",
                table: "Medicamentos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.Sql(@"
UPDATE Medicamentos SET
    Existencias = COALESCE((SELECT SUM(l.Existencias) FROM Lotes l WHERE l.MedicamentoId = Medicamentos.Id), 0),
    Lote = (SELECT l.NumeroLote FROM Lotes l WHERE l.MedicamentoId = Medicamentos.Id AND l.Existencias > 0
            ORDER BY l.FechaVencimiento IS NULL, l.FechaVencimiento LIMIT 1),
    FechaVencimiento = (SELECT l.FechaVencimiento FROM Lotes l WHERE l.MedicamentoId = Medicamentos.Id AND l.Existencias > 0
            ORDER BY l.FechaVencimiento IS NULL, l.FechaVencimiento LIMIT 1);");

            // Quitar la columna con su llave foránea requiere reconstruir la tabla (lo hace EF).
            migrationBuilder.DropIndex(
                name: "IX_DetallesDespacho_LoteId",
                table: "DetallesDespacho");

            migrationBuilder.DropColumn(
                name: "LoteId",
                table: "DetallesDespacho");

            migrationBuilder.DropTable(
                name: "Lotes");
        }
    }
}
