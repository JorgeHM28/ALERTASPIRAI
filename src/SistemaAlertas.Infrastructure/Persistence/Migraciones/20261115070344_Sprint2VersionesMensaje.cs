using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint2VersionesMensaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cadena_id",
                table: "avisos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referencias",
                table: "avisos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Avisos ya registrados: cada Alert inicia su propia cadena. Los <references> de los Update/Cancel
            // anteriores se recuperan del XML guardado.
            migrationBuilder.Sql("""
                UPDATE avisos SET referencias = NULLIF(btrim(substring(xml_cap from '<references>([^<]*)</references>')), '');
                UPDATE avisos SET cadena_id = id WHERE tipo_mensaje = 'Alert';
                UPDATE avisos u SET cadena_id = COALESCE(r.cadena_id, r.id)
                  FROM avisos r
                 WHERE u.tipo_mensaje IN ('Update', 'Cancel') AND u.referencias IS NOT NULL
                   AND (' ' || u.referencias || ' ') LIKE ('% ' || r.remitente || ',' || r.identificador_cap || ',%');
                """);

            migrationBuilder.CreateTable(
                name: "versiones_mensaje",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    estado_cap = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    area = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: true),
                    identificador_cap = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enviado_cap = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xml_cap = table.Column<string>(type: "text", nullable: true),
                    referencia_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referencias = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version_anterior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_devolucion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    preparado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enviada_a_revision_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revisado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    difundida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reemplazada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contenido_certeza = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contenido_descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    contenido_descripcion_area = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    contenido_evento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contenido_expira = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contenido_instruccion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    contenido_severidad = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contenido_titular = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    contenido_urgencia = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contenido_vigente = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "listas_destinos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generada_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canales = table.Column<int[]>(type: "integer[]", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "comunidades_pendientes",
                columns: table => new
                {
                    comunidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista_destinos_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunidad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
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
                    canal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    direccion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    lista_destinos_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "ix_avisos_cadena_id",
                table: "avisos",
                column: "cadena_id");

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
                name: "ix_versiones_mensaje_area",
                table: "versiones_mensaje",
                column: "area")
                .Annotation("Npgsql:IndexMethod", "gist");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comunidades_pendientes");

            migrationBuilder.DropTable(
                name: "destinos_envio");

            migrationBuilder.DropTable(
                name: "listas_destinos");

            migrationBuilder.DropTable(
                name: "versiones_mensaje");

            migrationBuilder.DropIndex(
                name: "ix_avisos_cadena_id",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "cadena_id",
                table: "avisos");

            migrationBuilder.DropColumn(
                name: "referencias",
                table: "avisos");
        }
    }
}
