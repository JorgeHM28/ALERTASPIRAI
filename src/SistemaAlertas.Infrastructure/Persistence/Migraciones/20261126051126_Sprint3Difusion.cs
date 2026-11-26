using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint3Difusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "publicada_web_en",
                table: "versiones_mensaje",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "caps_emitidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identificador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enviado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xml = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    valido = table.Column<bool>(type: "boolean", nullable: false),
                    referencia_cap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caps_emitidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_caps_emitidos_avisos_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "avisos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_caps_emitidos_caps_emitidos_referencia_cap_id",
                        column: x => x.referencia_cap_id,
                        principalTable: "caps_emitidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_caps_emitidos_versiones_mensaje_version_id",
                        column: x => x.version_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_aviso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_aviso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trabajos_difusion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resultado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resultado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    es_prueba = table.Column<bool>(type: "boolean", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    contacto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destino = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    texto = table.Column<string>(type: "character varying(1600)", maxLength: 1600, nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: true),
                    codigo_proveedor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ultimo_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    texto_legible = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    confirmado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajos_difusion", x => x.id);
                    table.ForeignKey(
                        name: "fk_trabajos_difusion_comunidades_comunidad_id",
                        column: x => x.comunidad_id,
                        principalTable: "comunidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabajos_difusion_contactos_contacto_id",
                        column: x => x.contacto_id,
                        principalTable: "contactos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabajos_difusion_usuarios_confirmado_por_id",
                        column: x => x.confirmado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabajos_difusion_versiones_mensaje_version_id",
                        column: x => x.version_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caps_emitidos_aviso_id",
                table: "caps_emitidos",
                column: "aviso_id");

            migrationBuilder.CreateIndex(
                name: "ix_caps_emitidos_identificador",
                table: "caps_emitidos",
                column: "identificador",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caps_emitidos_referencia_cap_id",
                table: "caps_emitidos",
                column: "referencia_cap_id");

            migrationBuilder.CreateIndex(
                name: "ix_caps_emitidos_version_id",
                table: "caps_emitidos",
                column: "version_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_aviso_aviso_id_fecha",
                table: "eventos_aviso",
                columns: new[] { "aviso_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_comunidad_id",
                table: "trabajos_difusion",
                column: "comunidad_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_confirmado_por_id",
                table: "trabajos_difusion",
                column: "confirmado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_contacto_id",
                table: "trabajos_difusion",
                column: "contacto_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_estado_creado_en",
                table: "trabajos_difusion",
                columns: new[] { "estado", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_proximo_intento",
                table: "trabajos_difusion",
                column: "proximo_intento",
                filter: "proximo_intento IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_difusion_version_id",
                table: "trabajos_difusion",
                column: "version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caps_emitidos");

            migrationBuilder.DropTable(
                name: "eventos_aviso");

            migrationBuilder.DropTable(
                name: "trabajos_difusion");

            migrationBuilder.DropColumn(
                name: "publicada_web_en",
                table: "versiones_mensaje");
        }
    }
}
