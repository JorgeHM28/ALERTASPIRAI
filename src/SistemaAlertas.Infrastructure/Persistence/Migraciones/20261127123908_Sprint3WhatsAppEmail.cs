using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint3WhatsAppEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "texto",
                table: "trabajos_difusion",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1600)",
                oldMaxLength: 1600,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "destino",
                table: "trabajos_difusion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "asunto",
                table: "trabajos_difusion",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombre_destino",
                table: "trabajos_difusion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "asunto",
                table: "trabajos_difusion");

            migrationBuilder.DropColumn(
                name: "nombre_destino",
                table: "trabajos_difusion");

            migrationBuilder.AlterColumn<string>(
                name: "texto",
                table: "trabajos_difusion",
                type: "character varying(1600)",
                maxLength: 1600,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "destino",
                table: "trabajos_difusion",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
