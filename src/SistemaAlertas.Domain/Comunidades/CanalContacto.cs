using System.Text.RegularExpressions;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Comunidades;

/// <summary>Canal por el que se puede alcanzar a un contacto (objeto de valor).</summary>
public partial class CanalContacto
{
    public TipoCanal Tipo { get; private set; }
    public string Destino { get; private set; } = null!;
    public bool Preferido { get; private set; }

    private CanalContacto() { }

    public CanalContacto(TipoCanal tipo, string destino, bool preferido)
    {
        destino = (destino ?? string.Empty).Trim();
        switch (tipo)
        {
            case TipoCanal.Sms or TipoCanal.WhatsApp or TipoCanal.Llamada:
                destino = destino.Replace(" ", "").Replace("-", "");
                if (!Telefono().IsMatch(destino))
                    throw new ExcepcionDominio($"'{destino}' no es un teléfono válido. Usa formato internacional, por ejemplo +59171234567.");
                break;
            case TipoCanal.Email:
                if (!Correo().IsMatch(destino)) throw new ExcepcionDominio($"'{destino}' no es un email válido.");
                destino = destino.ToLowerInvariant();
                break;
            case TipoCanal.Radio:
                if (destino.Length == 0) throw new ExcepcionDominio("Indica la emisora o frecuencia de radio.");
                break;
        }
        Tipo = tipo;
        Destino = destino;
        Preferido = preferido;
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex Telefono();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Correo();
}
