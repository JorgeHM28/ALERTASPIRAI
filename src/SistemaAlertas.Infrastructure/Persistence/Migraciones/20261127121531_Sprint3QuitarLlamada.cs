using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <summary>
    /// Sprint 3: el sistema no hace llamadas, así que se quita el canal "Llamada" de los contactos y de las listas de destinos.
    /// En contacto_canales y destinos_envio el canal se guarda como texto; en listas_destinos.canales como entero (2 = Llamada).
    /// </summary>
    public partial class Sprint3QuitarLlamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM contacto_canales WHERE tipo = 'Llamada';");
            migrationBuilder.Sql("DELETE FROM destinos_envio WHERE canal = 'Llamada';");
            migrationBuilder.Sql("UPDATE listas_destinos SET canales = array_remove(canales, 2) WHERE 2 = ANY(canales);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los canales de llamada borrados no se recuperan.
        }
    }
}
