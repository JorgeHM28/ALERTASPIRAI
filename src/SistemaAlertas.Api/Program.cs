using Microsoft.OpenApi;
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

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options => { options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    };
    return Task.CompletedTask;
});
options.AddOperationTransformer((operation, context, _) =>
{
    var metadata = context.Description.ActionDescriptor.EndpointMetadata;
    if (metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
        && !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
    {
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        }];
    }
    return Task.CompletedTask;
});
});
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
builder.Services.AddHostedService<RevisionVigenciaAvisos>();
builder.Services.AddHostedService<ProcesadorColaSms>();

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/openapi/v1.json", "Alertas Pirai"));
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/salud", async (AlertasDbContext db) =>
    Results.Ok(new { estado = "ok", baseDeDatos = await db.Database.CanConnectAsync() ? "ok" : "sin conexión", hora = DateTimeOffset.UtcNow }));

// Aplica las migraciones y crea el primer Administrador.
if (app.Configuration.GetValue("BaseDeDatos:MigrarAlIniciar", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AlertasDbContext>();
    await db.Database.MigrateAsync();
    var contrasenaAdmin = app.Configuration["Semilla:ContrasenaAdmin"];
    if (!string.IsNullOrWhiteSpace(contrasenaAdmin))
        await scope.ServiceProvider.GetRequiredService<ServicioUsuarios>().SembrarAdministradorAsync(contrasenaAdmin, default);
    var contrasenaAprobador = app.Configuration["Semilla:ContrasenaAprobador"];
    if (!string.IsNullOrWhiteSpace(contrasenaAprobador))
        await scope.ServiceProvider.GetRequiredService<ServicioUsuarios>().SembrarAprobadorDemoAsync(contrasenaAprobador, default);
}

app.Run();

public partial class Program;
