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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreUsuario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    HashContrasena = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    UltimoAcceso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zonas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NivelRiesgo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Area = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zonas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "avisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentificadorCap = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enviado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Formato = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TipoMensaje = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Evento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Urgencia = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severidad = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Certeza = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Titular = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Instruccion = table.Column<string>(type: "text", nullable: true),
                    Vigente = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Expira = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DescripcionArea = table.Column<string>(type: "text", nullable: true),
                    Area = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: true),
                    XmlCap = table.Column<string>(type: "text", nullable: false),
                    RegistradoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_avisos_usuarios_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comunidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Poblacion = table.Column<int>(type: "integer", nullable: true),
                    Ubicacion = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true),
                    ZonaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comunidades_zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contactos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComunidadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contactos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contactos_comunidades_ComunidadId",
                        column: x => x.ComunidadId,
                        principalTable: "comunidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacto_canales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Destino = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Preferido = table.Column<bool>(type: "boolean", nullable: false),
                    ContactoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacto_canales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacto_canales_contactos_ContactoId",
                        column: x => x.ContactoId,
                        principalTable: "contactos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avisos_Area",
                table: "avisos",
                column: "Area")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_Enviado",
                table: "avisos",
                column: "Enviado");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_RegistradoPorId",
                table: "avisos",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_Remitente_IdentificadorCap",
                table: "avisos",
                columns: new[] { "Remitente", "IdentificadorCap" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comunidades_ZonaId_Nombre",
                table: "comunidades",
                columns: new[] { "ZonaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacto_canales_ContactoId",
                table: "contacto_canales",
                column: "ContactoId");

            migrationBuilder.CreateIndex(
                name: "IX_contactos_ComunidadId",
                table: "contactos",
                column: "ComunidadId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_NombreUsuario",
                table: "usuarios",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zonas_Area",
                table: "zonas",
                column: "Area")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_zonas_Nombre",
                table: "zonas",
                column: "Nombre",
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
