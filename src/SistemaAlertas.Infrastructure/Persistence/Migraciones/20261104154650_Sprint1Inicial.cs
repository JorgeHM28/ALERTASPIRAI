using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaAlertas.Infrastructure.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class Sprint1Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_usuario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    hash_contrasena = table.Column<string>(type: "text", nullable: false),
                    rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_acceso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zonas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nivel_riesgo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    area = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zonas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identificador_cap = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enviado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    formato = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tipo_mensaje = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    evento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    urgencia = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    severidad = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    certeza = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    titular = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    instruccion = table.Column<string>(type: "text", nullable: true),
                    vigente = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expira = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    descripcion_area = table.Column<string>(type: "text", nullable: true),
                    area = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: true),
                    xml_cap = table.Column<string>(type: "text", nullable: false),
                    registrado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "comunidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    poblacion = table.Column<int>(type: "integer", nullable: true),
                    ubicacion = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true),
                    zona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "contactos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "contacto_canales",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    destino = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    preferido = table.Column<bool>(type: "boolean", nullable: false),
                    contacto_id = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_avisos_area",
                table: "avisos",
                column: "area")
                .Annotation("Npgsql:IndexMethod", "gist");

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
                name: "ix_contactos_comunidad_id",
                table: "contactos",
                column: "comunidad_id");

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
                name: "ix_zonas_area",
                table: "zonas",
                column: "area")
                .Annotation("Npgsql:IndexMethod", "gist");

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
                name: "avisos");

            migrationBuilder.DropTable(
                name: "contacto_canales");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "contactos");

            migrationBuilder.DropTable(
                name: "comunidades");

            migrationBuilder.DropTable(
                name: "zonas");
        }
    }
}
