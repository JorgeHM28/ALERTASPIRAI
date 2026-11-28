using System.Globalization;
using System.Text;

namespace SistemaAlertas.Application.Difusion;

/// <summary>
/// Texto apto para SMS. Una sola tilde hace que el celular use la codificación UCS-2: el mensaje se parte cada 70
/// caracteres y, en la prueba real con SMS Gateway, el chip rechazó la alerta partida (RESULT_ERROR_GENERIC_FAILURE)
/// mientras que el mismo texto sin tildes llegó. Por eso el SMS sale sin tildes, sin ñ y solo con caracteres ASCII
/// imprimibles, que caben en el alfabeto básico de SMS (160 caracteres por mensaje). Los demás canales conservan las tildes.
/// No se corta: si pasa de 160 caracteres se envía en varias partes, pero sin perder la instrucción a la población.
/// </summary>
public static class TextoSms
{
    public const int LargoUnMensaje = 160;

    private static readonly Dictionary<char, string> Reemplazos = new()
    {
        ['ñ'] = "n", ['Ñ'] = "N", ['¿'] = "", ['¡'] = "", ['“'] = "\"", ['”'] = "\"", ['«'] = "\"", ['»'] = "\"",
        ['‘'] = "'", ['’'] = "'", ['–'] = "-", ['—'] = "-", ['…'] = "...", ['°'] = " grados", ['€'] = "EUR"
    };

    public static string ParaSms(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // tilde, diéresis
            if (Reemplazos.TryGetValue(c, out var r)) sb.Append(r);
            else if (c is '\n' or '\r') sb.Append(' ');
            else if (c >= ' ' && c <= '~') sb.Append(c);
            // Cualquier otro carácter (emoji, símbolos) obligaría a usar UCS-2: se omite.
        }
        // ñ ya se normaliza a n + tilde; el diccionario cubre los casos que no se descomponen.
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Cuántos SMS ocupa un texto ya preparado con <see cref="ParaSms"/> (153 caracteres por parte si se divide).</summary>
    public static int Partes(string textoSms) => textoSms.Length <= LargoUnMensaje ? 1 : (int)Math.Ceiling(textoSms.Length / 153.0);
}
