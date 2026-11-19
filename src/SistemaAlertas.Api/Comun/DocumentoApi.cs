using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace SistemaAlertas.Api.Comun;

public sealed class DocumentoApi : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "SAT Río Piraí";
        document.Info.Description = "API de alertas, zonas, comunidades y mensajes. Inicia sesión en /api/auth/login y pega el token en Authorize.";
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
        foreach (var path in document.Paths)
            foreach (var entry in path.Value.Operations ?? [])
            {
                var op = entry.Value;
                if (!path.Key.StartsWith("/api/publico") && path.Key != "/api/auth/login" && path.Key != "/api/salud")
                {
                    op.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
                    op.Responses ??= new();
                    op.Responses.TryAdd("401", new OpenApiResponse { Description = "Sin autenticación válida" });
                    op.Responses.TryAdd("403", new OpenApiResponse { Description = "Permiso insuficiente" });
                }
                op.Responses ??= new();
                op.Responses.TryAdd("400", new OpenApiResponse { Description = "Validación o regla de dominio: ProblemDetails" });
                op.Responses.TryAdd("404", new OpenApiResponse { Description = "Recurso inexistente: ProblemDetails" });
                op.Responses.TryAdd("409", new OpenApiResponse { Description = "Registro duplicado o relacionado: ProblemDetails" });
            bool crea = entry.Key == HttpMethod.Post && (path.Key is "/api/avisos" or "/api/avisos/importar" or "/api/usuarios"
                || path.Key.EndsWith("/versiones") || path.Key.EndsWith("/corregir") || path.Key.EndsWith("/update") || path.Key.EndsWith("/cancel"));
            if (crea && op.Responses.Remove("200", out var ok)) op.Responses["201"] = ok;
            if (op.Responses.TryGetValue("201", out var creada) && creada is OpenApiResponse respuesta)
            {
                respuesta.Description = "Recurso creado";
                respuesta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                respuesta.Headers["Location"] = new OpenApiHeader { Description = "URL GET del recurso creado", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
            foreach (var estado in new[] { "400", "404", "409" })
                if (op.Responses[estado] is OpenApiResponse error)
                    error.Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = new Dictionary<string, IOpenApiSchema> {
                            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String }, ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String } } } } };

                if (path.Key.Contains("importar") || path.Key.EndsWith("validar-cap"))
                {
                    if (path.Key.Contains("avisos"))
                        op.RequestBody = new OpenApiRequestBody
                        {
                            Required = true,
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["application/xml"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                                ["application/json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
                            }
                        };
                }
            }
        return Task.CompletedTask;
    }
}
