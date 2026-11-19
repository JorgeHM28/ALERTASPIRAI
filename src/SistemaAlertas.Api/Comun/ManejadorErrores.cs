using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
                => (StatusCodes.Status409Conflict, "Registro duplicado"),
            DbUpdateException { InnerException: SqlException { Number: 547 } }
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
                Detail = ex is DbUpdateException ? "La operación entra en conflicto con un registro existente o relacionado." : estado == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : ex.Message
            }
        });
    }
}
