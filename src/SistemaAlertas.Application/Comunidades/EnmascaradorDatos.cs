namespace SistemaAlertas.Application.Comunidades;

/// <summary>Oculta datos de contacto para quien no tiene permiso de verlos y para los registros (logs).</summary>
public static class EnmascaradorDatos
{
    /// <summary>
    /// "+59171234589" → "+591 7******89": se ve el código de país, el primer dígito y los dos últimos.
    /// Un número sin código de país ("71234589") queda "7******89".
    /// </summary>
    public static string Telefono(string? telefono)
    {
        var digitos = new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length < 4) return new string('*', digitos.Length);
        var local = digitos.Length > 8 ? digitos[^8..] : digitos;
        var pais = digitos.Length > 8 ? "+" + digitos[..^8] + " " : string.Empty;
        return pais + local[0] + new string('*', local.Length - 3) + local[^2..];
    }

    /// <summary>"juana.perez@porongo.bo" → "j***@porongo.bo".</summary>
    public static string Email(string? email)
    {
        var texto = email ?? string.Empty;
        var arroba = texto.IndexOf('@');
        return arroba <= 0 ? "***" : texto[0] + "***" + texto[arroba..];
    }

    /// <summary>Destino de un canal según su tipo: teléfono o correo enmascarado; la radio no es un dato personal.</summary>
    public static string Destino(Domain.Comunidades.TipoCanal tipo, string destino) => tipo switch
    {
        Domain.Comunidades.TipoCanal.Email => Email(destino),
        Domain.Comunidades.TipoCanal.Radio => destino,
        _ => Telefono(destino)
    };
}
