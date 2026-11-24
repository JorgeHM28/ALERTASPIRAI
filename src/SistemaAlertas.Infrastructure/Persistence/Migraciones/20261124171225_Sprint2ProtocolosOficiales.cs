using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint2ProtocolosOficiales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protocolo_cita",
                table: "versiones_mensaje",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "protocolo_id",
                table: "versiones_mensaje",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protocolo_titulo",
                table: "versiones_mensaje",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "protocolos_oficiales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    instruccion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    fuente = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_documento = table.Column<DateOnly>(type: "date", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protocolos_oficiales", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_protocolo_id",
                table: "versiones_mensaje",
                column: "protocolo_id");

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_oficiales_titulo",
                table: "protocolos_oficiales",
                column: "titulo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_versiones_mensaje_protocolos_oficiales_protocolo_id",
                table: "versiones_mensaje",
                column: "protocolo_id",
                principalTable: "protocolos_oficiales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_versiones_mensaje_protocolos_oficiales_protocolo_id",
                table: "versiones_mensaje");

            migrationBuilder.DropTable(
                name: "protocolos_oficiales");

            migrationBuilder.DropIndex(
                name: "ix_versiones_mensaje_protocolo_id",
                table: "versiones_mensaje");

            migrationBuilder.DropColumn(
                name: "protocolo_cita",
                table: "versiones_mensaje");

            migrationBuilder.DropColumn(
                name: "protocolo_id",
                table: "versiones_mensaje");

            migrationBuilder.DropColumn(
                name: "protocolo_titulo",
                table: "versiones_mensaje");
        }
    }
}
