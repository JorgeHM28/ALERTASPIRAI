using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class SqlServerInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre_usuario = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    nombre_completo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    hash_contrasena = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    ultimo_acceso = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zonas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    nivel_riesgo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    area = table.Column<Geometry>(type: "geometry", nullable: false),
                    fuente_institucional = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    referencia_documento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    fecha_documento = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zonas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    identificador_cap = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    remitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    enviado = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    formato = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    tipo_mensaje = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    estado_cap = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    evento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    urgencia = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    severidad = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    certeza = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    titular = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    instruccion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    vigente = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    expira = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    referencias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    cadena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    descripcion_area = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    area = table.Column<Geometry>(type: "geometry", nullable: true),
                    xml_cap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    registrado_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contenido_original = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tipo_contenido_original = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    hash_original = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    incompleto = table.Column<bool>(type: "bit", nullable: false),
                    vencido = table.Column<bool>(type: "bit", nullable: false),
                    apto_para_difusion = table.Column<bool>(type: "bit", nullable: false),
                    motivos_no_apto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    aptitud_evaluada_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos", x => x.id);
                    table.ForeignKey(
                        name: "fk_avisos_usuarios_registrado_por_id",
                        column: x => x.registrado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contacto_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    accion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    instantanea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    cambios = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacto_historial", x => x.id);
                    table.CheckConstraint("CK_contacto_historial_json", "ISJSON([instantanea]) = 1");
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
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    accion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    rol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                    rol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asignado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "comunidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    municipio = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    poblacion = table.Column<int>(type: "int", nullable: true),
                    ubicacion = table.Column<Point>(type: "geometry", nullable: true),
                    zona_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comunidades", x => x.id);
                    table.ForeignKey(
                        name: "fk_comunidades_zonas_zona_id",
                        column: x => x.zona_id,
                        principalTable: "zonas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versiones_mensaje",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cadena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    estado_cap = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    area = table.Column<Geometry>(type: "geometry", nullable: true),
                    identificador_cap = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    remitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    enviado_cap = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    xml_cap = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    referencia_version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    referencias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    version_anterior_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_devolucion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    preparado_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    enviada_a_revision_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    revisado_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    revisado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    difundida_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reemplazada_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    contenido_certeza = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    contenido_descripcion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    contenido_descripcion_area = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    contenido_evento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    contenido_expira = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    contenido_instruccion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    contenido_severidad = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    contenido_titular = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    contenido_urgencia = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    contenido_vigente = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versiones_mensaje", x => x.id);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_avisos_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "avisos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_avisos_cadena_id",
                        column: x => x.cadena_id,
                        principalTable: "avisos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_usuarios_preparado_por_id",
                        column: x => x.preparado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_usuarios_revisado_por_id",
                        column: x => x.revisado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_versiones_mensaje_reemplazada_por_id",
                        column: x => x.reemplazada_por_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_versiones_mensaje_referencia_version_id",
                        column: x => x.referencia_version_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versiones_mensaje_versiones_mensaje_version_anterior_id",
                        column: x => x.version_anterior_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contactos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    procedencia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    autorizado = table.Column<bool>(type: "bit", nullable: false),
                    referencia_autorizacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    fecha_autorizacion = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contactos", x => x.id);
                    table.ForeignKey(
                        name: "fk_contactos_comunidades_comunidad_id",
                        column: x => x.comunidad_id,
                        principalTable: "comunidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listas_destinos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    generada_por_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    canales = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listas_destinos", x => x.id);
                    table.ForeignKey(
                        name: "fk_listas_destinos_versiones_mensaje_version_id",
                        column: x => x.version_id,
                        principalTable: "versiones_mensaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacto_canales",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    destino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    preferido = table.Column<bool>(type: "bit", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacto_canales", x => x.id);
                    table.ForeignKey(
                        name: "fk_contacto_canales_contactos_contacto_id",
                        column: x => x.contacto_id,
                        principalTable: "contactos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comunidades_pendientes",
                columns: table => new
                {
                    comunidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    lista_destinos_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comunidad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comunidades_pendientes", x => new { x.lista_destinos_id, x.comunidad_id });
                    table.ForeignKey(
                        name: "fk_comunidades_pendientes_listas_destinos_lista_destinos_id",
                        column: x => x.lista_destinos_id,
                        principalTable: "listas_destinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "destinos_envio",
                columns: table => new
                {
                    canal = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    direccion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    lista_destinos_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinos_envio", x => new { x.lista_destinos_id, x.canal, x.direccion });
                    table.ForeignKey(
                        name: "fk_destinos_envio_listas_destinos_lista_destinos_id",
                        column: x => x.lista_destinos_id,
                        principalTable: "listas_destinos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_avisos_apto_para_difusion",
                table: "avisos",
                column: "apto_para_difusion");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_cadena_id",
                table: "avisos",
                column: "cadena_id");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_enviado",
                table: "avisos",
                column: "enviado");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_registrado_por_id",
                table: "avisos",
                column: "registrado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_remitente_identificador_cap",
                table: "avisos",
                columns: new[] { "remitente", "identificador_cap" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comunidades_zona_id_nombre",
                table: "comunidades",
                columns: new[] { "zona_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contacto_canales_contacto_id",
                table: "contacto_canales",
                column: "contacto_id");

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
                name: "ix_contactos_comunidad_id",
                table: "contactos",
                column: "comunidad_id");

            migrationBuilder.CreateIndex(
                name: "ix_destinos_envio_contacto_id",
                table: "destinos_envio",
                column: "contacto_id");

            migrationBuilder.CreateIndex(
                name: "ix_listas_destinos_version_id",
                table: "listas_destinos",
                column: "version_id",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_nombre_usuario",
                table: "usuarios",
                column: "nombre_usuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_aviso_id",
                table: "versiones_mensaje",
                column: "aviso_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_cadena_id_numero",
                table: "versiones_mensaje",
                columns: new[] { "cadena_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_estado",
                table: "versiones_mensaje",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_identificador_cap",
                table: "versiones_mensaje",
                column: "identificador_cap",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_preparado_por_id",
                table: "versiones_mensaje",
                column: "preparado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_reemplazada_por_id",
                table: "versiones_mensaje",
                column: "reemplazada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_referencia_version_id",
                table: "versiones_mensaje",
                column: "referencia_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_revisado_por_id",
                table: "versiones_mensaje",
                column: "revisado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_mensaje_version_anterior_id",
                table: "versiones_mensaje",
                column: "version_anterior_id");

            migrationBuilder.CreateIndex(
                name: "ix_zonas_nombre",
                table: "zonas",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comunidades_pendientes");

            migrationBuilder.DropTable(
                name: "contacto_canales");

            migrationBuilder.DropTable(
                name: "contacto_historial");

            migrationBuilder.DropTable(
                name: "destinos_envio");

            migrationBuilder.DropTable(
                name: "usuario_bitacora");

            migrationBuilder.DropTable(
                name: "usuario_roles");

            migrationBuilder.DropTable(
                name: "contactos");

            migrationBuilder.DropTable(
                name: "listas_destinos");

            migrationBuilder.DropTable(
                name: "comunidades");

            migrationBuilder.DropTable(
                name: "versiones_mensaje");

            migrationBuilder.DropTable(
                name: "zonas");

            migrationBuilder.DropTable(
                name: "avisos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
