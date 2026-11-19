using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint1CierreRequisitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_documento",
                table: "zonas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuente_institucional",
                table: "zonas",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referencia_documento",
                table: "zonas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "autorizado",
                table: "contactos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_autorizacion",
                table: "contactos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "procedencia",
                table: "contactos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "Sin registrar");

            migrationBuilder.AddColumn<string>(
                name: "referencia_autorizacion",
                table: "contactos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "aptitud_evaluada_en",
                table: "avisos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: Domain.Avisos.Aviso.SinEvaluar);

            migrationBuilder.AddColumn<bool>(
                name: "apto_para_difusion",
                table: "avisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "contenido_original",
                table: "avisos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_cap",
                table: "avisos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Actual");

            migrationBuilder.AddColumn<string>(
                name: "hash_original",
                table: "avisos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "incompleto",
                table: "avisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "motivos_no_apto",
                table: "avisos",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "tipo_contenido_original",
                table: "avisos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "vencido",
                table: "avisos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "contacto_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instantanea = table.Column<string>(type: "jsonb", nullable: false),
                    cambios = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacto_historial", x => x.id);
                    table.ForeignKey(
                        name: "fk_contacto_historial_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_bitacora",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_bitacora", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_bitacora_usuarios_administrador_id",
                        column: x => x.administrador_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_bitacora_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_roles",
                columns: table => new
                {
                    rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    asignado_por_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_roles", x => new { x.usuario_id, x.rol });
                    table.ForeignKey(
                        name: "fk_usuario_roles_usuarios_asignado_por_id",
                        column: x => x.asignado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_roles_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Datos existentes. RF-13: el rol único pasa a ser la primera asignación de cada usuario, y queda en la bitácora.
            migrationBuilder.Sql("""
                INSERT INTO usuario_roles (usuario_id, rol, asignado_en, asignado_por_id)
                SELECT id, rol, creado_en, NULL FROM usuarios;
                INSERT INTO usuario_bitacora (id, usuario_id, administrador_id, accion, rol, detalle, creado_en)
                SELECT gen_random_uuid(), id, NULL, 'RolAsignado', rol, 'Rol traspasado del esquema de un solo rol.', now() FROM usuarios;
                """);
            migrationBuilder.DropColumn(
                name: "rol",
                table: "usuarios");

            // RF-01: los avisos XML ya guardaban el documento tal cual llegó; se registra como original con su hash.
            // La aptitud se calcula al arrancar la API (aptitud_evaluada_en = SinEvaluar).
            migrationBuilder.Sql("""
                UPDATE avisos
                SET contenido_original = xml_cap,
                    tipo_contenido_original = 'application/xml',
                    hash_original = encode(sha256(convert_to(xml_cap, 'UTF8')), 'hex')
                WHERE formato = 'CapXml';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_avisos_apto_para_difusion",
                table: "avisos",
                column: "apto_para_difusion");

            migrationBuilder.CreateIndex(
                name: "ix_contacto_historial_comunidad_id",
                table: "contacto_historial",
                column: "comunidad_id");

            migrationBuilder.CreateIndex(
                name: "ix_contacto_historial_contacto_id_creado_en",
                table: "contacto_historial",
                columns: new[] { "contacto_id", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_contacto_historial_usuario_id",
                table: "contacto_historial",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_bitacora_administrador_id",
                table: "usuario_bitacora",
                column: "administrador_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_bitacora_usuario_id_creado_en",
                table: "usuario_bitacora",
                columns: new[] { "usuario_id", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_roles_asignado_por_id",
                table: "usuario_roles",
                column: "asignado_por_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rol",
                table: "usuarios",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Consulta");
            migrationBuilder.Sql("""
                UPDATE usuarios u SET rol = r.rol
                FROM (SELECT DISTINCT ON (usuario_id) usuario_id, rol FROM usuario_roles
                      ORDER BY usuario_id, CASE rol WHEN 'Administrador' THEN 0 WHEN 'Operador' THEN 1 WHEN 'Aprobador' THEN 2 ELSE 3 END) r
                WHERE u.id = r.usuario_id;
                """);

            migrationBuilder.DropTable(
                name: "contacto_historial");

            migrationBuilder.DropTable(
                name: "usuario_bitacora");

            migrationBuilder.DropTable(
                name: "usuario_roles");

            migrationBuilder.DropIndex(
                name: "ix_avisos_apto_para_difusion",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "fecha_documento",
                table: "zonas");

            migrationBuilder.DropColumn(
                name: "fuente_institucional",
                table: "zonas");

            migrationBuilder.DropColumn(
                name: "referencia_documento",
                table: "zonas");

            migrationBuilder.DropColumn(
                name: "autorizado",
                table: "contactos");

            migrationBuilder.DropColumn(
                name: "fecha_autorizacion",
                table: "contactos");

            migrationBuilder.DropColumn(
                name: "procedencia",
                table: "contactos");

            migrationBuilder.DropColumn(
                name: "referencia_autorizacion",
                table: "contactos");

            migrationBuilder.DropColumn(
                name: "aptitud_evaluada_en",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "apto_para_difusion",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "contenido_original",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "estado_cap",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "hash_original",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "incompleto",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "motivos_no_apto",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "tipo_contenido_original",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "vencido",
                table: "avisos");

        }
    }
}
