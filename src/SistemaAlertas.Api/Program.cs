using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Infrastructure;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Seguridad;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<DocumentoApi>());
builder.Services.AddAntiforgery();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(builder.Configuration.GetSection(OpcionesCap.Seccion).Get<OpcionesCap>() ?? new OpcionesCap());
builder.Services.AddSingleton(builder.Configuration.GetSection(OpcionesDestinos.Seccion).Get<OpcionesDestinos>() ?? new OpcionesDestinos());

// RF-13: autenticación con JWT y autorización por rol.
var jwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()!;
builder.Services.AddAuthentication(o => { o.DefaultScheme = "Seleccion"; o.DefaultChallengeScheme = "Seleccion"; })
    .AddPolicyScheme("Seleccion", "API o MVC", o => o.ForwardDefaultSelector = c => c.Request.Path.StartsWithSegments("/api") ? JwtBearerDefaults.AuthenticationScheme : "Web")
    .AddCookie("Web", o => { o.LoginPath = "/cuenta/login"; o.AccessDeniedPath = "/cuenta/denegado"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false; o.Events.OnValidatePrincipal = SesionWeb.Validar; })
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
            NameClaimType = NombresClaim.Nombre,
            RoleClaimType = NombresClaim.Rol,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        o.Events = new JwtBearerEvents { OnTokenValidated = ValidacionSesion.AlValidarToken };
    });
builder.Services.AddAuthorization(o =>
{
    foreach (var (politica, roles) in Politicas.Todas())
        o.AddPolicy(politica, p => p.RequireAuthenticatedUser().RequireRole(roles.Select(r => r.ToString())));
});
if (builder.Configuration.GetValue("Procesos:Activos", true))
{
    builder.Services.AddHostedService<RevisionVigenciaAvisos>();
    builder.Services.AddHostedService<ProcesadorColaSms>();
}

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/openapi/v1.json", "SAT Río Piraí · v1"); o.DocumentTitle = "API SAT Río Piraí"; });
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("mvc", "{controller=Inicio}/{action=Index}/{id?}");

app.MapGet("/api/salud", async (AlertasDbContext db) =>
    Results.Ok(new { estado = "ok", baseDeDatos = await db.Database.CanConnectAsync() ? "ok" : "sin conexión", hora = DateTimeOffset.UtcNow }));

// Aplica las migraciones y crea el primer Administrador.
if (app.Configuration.GetValue("BaseDeDatos:MigrarAlIniciar", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AlertasDbContext>();
    await db.Database.MigrateAsync();
    var contrasenaAdmin = app.Configuration["Semilla:ContrasenaAdmin"];
    if (!string.IsNullOrWhiteSpace(contrasenaAdmin))
        await scope.ServiceProvider.GetRequiredService<ServicioUsuarios>().SembrarAdministradorAsync(contrasenaAdmin, default);
}

if (args.Contains("--sembrar") || args.Contains("--sembrar-demo"))
{
    using var scope = app.Services.CreateScope();
    var clave = app.Configuration["Semilla:ContrasenaAdmin"]
        ?? throw new InvalidOperationException("Configura Semilla:ContrasenaAdmin mediante user-secrets o variable de entorno.");
    await scope.ServiceProvider.GetRequiredService<ServicioUsuarios>().SembrarAdministradorAsync(clave, default);
    if (args.Contains("--sembrar-demo"))
        await DatosDemostracion.Cargar(scope.ServiceProvider);
    return;
}
app.Run();

public partial class Program;
