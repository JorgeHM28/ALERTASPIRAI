using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Application.Mensajes;

/// <summary>
/// RF-06: cómo se lee una versión fuera de la web. El nivel va siempre en palabras ("ALERTA NARANJA"),
/// porque el SMS, la radio y el altavoz no tienen color.
/// </summary>
public static class TextosMensaje
{
    public const int LargoMaximoSms = 320;

    /// <summary>Texto corto (SMS, altavoz) y texto completo para lectura (radio, web).</summary>
    public static (string Sms, string Completo) De(VersionMensaje v)
    {
        var c = v.Contenido;
        var titular = c.Titular ?? c.Evento;
        var prefijo = v.Tipo switch
        {
            TipoMensajeCap.Update => "ACTUALIZACIÓN. ",
            TipoMensajeCap.Cancel => "AVISO CANCELADO. ",
            _ => string.Empty
        };
        var nivel = v.Tipo == TipoMensajeCap.Cancel ? string.Empty : c.Nivel.Texto.ToUpperInvariant() + ": ";
        var sms = $"{prefijo}{nivel}{titular}. {c.Instruccion}".Trim();
        if (sms.Length > LargoMaximoSms) sms = sms[..(LargoMaximoSms - 1)] + "…";

        var partes = new List<string?>
        {
            $"{prefijo}{c.Nivel.Texto}. {titular}.",
            c.Descripcion,
            string.IsNullOrWhiteSpace(c.Instruccion) ? null : "Qué hacer: " + c.Instruccion,
            string.IsNullOrWhiteSpace(c.DescripcionArea) ? null : "Zona: " + c.DescripcionArea,
            c.Expira is { } e ? $"Vigente hasta: {e.ToOffset(TimeSpan.FromHours(-4)):dd/MM/yyyy HH:mm} (hora de Bolivia)." : null
        };
        return (sms, string.Join("\n\n", partes.Where(p => !string.IsNullOrWhiteSpace(p))));
    }
}
