using System.ComponentModel.DataAnnotations;
using SistemaAlertas.Domain.Comun;
namespace SistemaAlertas.Application.Abstracciones;
/// <summary>Valida las anotaciones de los parámetros de records también fuera del model binder HTTP.</summary>
public static class ValidacionDto
{
    public static void Validar<T>(T dto) where T : notnull
    {
        var tipo = typeof(T);
        var constructor = tipo.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        foreach (var parametro in constructor.GetParameters())
        {
            var valor = tipo.GetProperty(parametro.Name!)?.GetValue(dto);
            foreach (var regla in parametro.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>())
                if (!regla.IsValid(valor)) throw new ExcepcionDominio(regla.FormatErrorMessage(parametro.Name!));
            var tipoValor = Nullable.GetUnderlyingType(parametro.ParameterType) ?? parametro.ParameterType;
            if (valor is not null && tipoValor.IsEnum && !Enum.IsDefined(tipoValor, valor)) throw new ExcepcionDominio($"{parametro.Name}: valor no permitido.");
        }
    }
}
