using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Api.Comun;

/// <summary>Traduce las excepciones a respuestas ProblemDetails con el código HTTP adecuado.</summary>
public sealed class ManejadorErrores(ILogger<ManejadorErrores> logger, IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (estado, titulo) = ex switch
        {
            ExcepcionDominio => (StatusCodes.Status400BadRequest, "Datos no válidos"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "Registro duplicado"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }
                => (StatusCodes.Status409Conflict, "El registro está en uso"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (estado == StatusCodes.Status500InternalServerError) logger.LogError(ex, "Error no controlado");

        http.Response.StatusCode = estado;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = estado == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : ex.Message
            }
        });
    }
}
