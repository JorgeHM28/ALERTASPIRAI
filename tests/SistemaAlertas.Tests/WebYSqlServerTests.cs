using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Infrastructure.Persistence;
namespace SistemaAlertas.Tests;

public sealed class AplicacionPrueba(string? conexion = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Alertas"] = conexion ?? "Server=localhost;Database=NoUsar;Integrated Security=true",
            ["BaseDeDatos:MigrarAlIniciar"] = "false",
            ["Procesos:Activos"] = "false",
            ["Jwt:Clave"] = "clave-exclusiva-para-pruebas-automatizadas-1234567890"
        }));
    }
}
public sealed class WebTests
{
    [Fact]
    public async Task SwaggerYLoginSeSirvenSinReactNiBaseDeDatos()
    {
        await using var app = new AplicacionPrueba(); using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/cuenta/login"); Assert.Contains("__RequestVerificationToken", login); Assert.DoesNotContain("react", login, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/contactos/{contactoId}", out _));
        Assert.True(schema.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        Assert.DoesNotContain(schema.GetProperty("paths").EnumerateObject(), p => p.Name.StartsWith("/gestion"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/comunidades")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/gestion/comunidades")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/cuenta/login", new FormUrlEncodedContent(new Dictionary<string, string> { { "NombreUsuario", "admin" }, { "Contrasena", "prueba" } }))).StatusCode);
    }
}
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALERTAS_TEST_SQLSERVER"))) Skip = "Requiere SQL Server real: configura ALERTAS_TEST_SQLSERVER. No se sustituye por SQLite o memoria."; }
}
public sealed class SqlServerEndToEndTests
{
    [SqlServerFact]
    public async Task CrudErroresGeografiaMvcYPersistenciaTrasReiniciarHost()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALERTAS_TEST_SQLSERVER"));
        var nombre = "AlertasPrueba_" + Guid.NewGuid().ToString("N"); builder.InitialCatalog = nombre; var conexion = builder.ConnectionString;
        Guid comunidadId = default;
        try
        {
            await using (var app = new AplicacionPrueba(conexion))
            {
                using (var scope = app.Services.CreateScope())
                { var db = scope.ServiceProvider.GetRequiredService<AlertasDbContext>(); await db.Database.MigrateAsync(); await db.Database.MigrateAsync(); await scope.ServiceProvider.GetRequiredService<ServicioUsuarios>().SembrarAdministradorAsync("PruebaLocal123", default); }
                using var client = app.CreateClient(new() { AllowAutoRedirect = false }); await Autorizar(client);
                var zona = await client.PostAsJsonAsync("/api/zonas", new { nombre = "Zona de prueba", descripcion = "Prueba", nivelRiesgo = "Alto", geometria = new { type = "Polygon", coordinates = new[] { new[] { new[] { -63.3, -17.9 }, new[] { -63.1, -17.9 }, new[] { -63.1, -17.6 }, new[] { -63.3, -17.6 }, new[] { -63.3, -17.9 } } } }, fuenteInstitucional = "Prueba académica", referenciaDocumento = "DEMO", fechaDocumento = "2026-11-18" });
                Assert.Equal(HttpStatusCode.Created, zona.StatusCode); var zid = (await zona.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
                object Datos(string nombre, int poblacion, Guid z, double lat = -17.75) => new { nombre, municipio = "Santa Cruz", poblacion, latitud = lat, longitud = -63.2, zonaId = z };
                var creada = await client.PostAsJsonAsync("/api/comunidades", Datos("Persistencia", 100, zid)); Assert.Equal(HttpStatusCode.Created, creada.StatusCode); Assert.NotNull(creada.Headers.Location);
                comunidadId = (await creada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(creada.Headers.Location)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/comunidades/{comunidadId}", Datos("Persistencia actualizada", 150, zid))).StatusCode);
                foreach (var datos in new[] { Datos("", 1, zid), Datos("Inválida", -1, zid), Datos("Sin zona", 1, Guid.NewGuid()), Datos("Fuera", 1, zid, 0) }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/comunidades", datos)).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/comunidades/" + Guid.NewGuid())).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/zonas/" + zid)).StatusCode);
                // Sesión MVC real y formularios: mismo servicio, sin invocar HTTP a la propia API.
                using var web = app.CreateClient(new() { AllowAutoRedirect = false }); var html = await web.GetStringAsync("/cuenta/login");
                var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); Assert.NotEmpty(token);
                var login = await web.PostAsync("/cuenta/login", new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", token }, { "NombreUsuario", "admin" }, { "Contrasena", "PruebaLocal123" } })); Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                foreach (var ruta in new[] { "zonas", "comunidades", "avisos", "mensajes", "usuarios", "destinatarios", "difusion", "cuenta", "comunidades?id=" + comunidadId, "zonas?id=" + zid }) Assert.Equal(HttpStatusCode.OK, (await web.GetAsync("/gestion/" + ruta)).StatusCode);
                // Las cookies web no autentican la API.
                Assert.Equal(HttpStatusCode.Unauthorized, (await web.GetAsync("/api/comunidades")).StatusCode);
            }
            // Host y contexto nuevos contra la misma base; sin ejecutar semilla otra vez.
            await using (var app = new AplicacionPrueba(conexion))
            {
                using var client = app.CreateClient(); await Autorizar(client);
                var dato = await client.GetFromJsonAsync<JsonElement>("/api/comunidades/" + comunidadId); Assert.Equal("Persistencia actualizada", dato.GetProperty("nombre").GetString()); Assert.Equal(150, dato.GetProperty("poblacion").GetInt32());
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/comunidades/" + comunidadId)).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/comunidades/" + comunidadId)).StatusCode);
            }
        }
        finally
        {
            // Solo elimina la base cuyo nombre aleatorio creó este test.
            SqlConnection.ClearAllPools(); builder.InitialCatalog = "master"; await using var sql = new SqlConnection(builder.ConnectionString); await sql.OpenAsync();
            await using var cmd = new SqlCommand($"IF DB_ID('{nombre}') IS NOT NULL BEGIN ALTER DATABASE [{nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombre}]; END", sql); await cmd.ExecuteNonQueryAsync();
        }
    }
    private static async Task Autorizar(HttpClient client)
    { var login = await client.PostAsJsonAsync("/api/auth/login", new { nombreUsuario = "admin", contrasena = "PruebaLocal123" }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); var json = await login.Content.ReadFromJsonAsync<JsonElement>(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString()); }
}
