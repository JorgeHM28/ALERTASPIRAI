using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// Nivel de alerta que ve la población (RF-06). Sale de la severidad oficial del CAP y siempre se muestra con texto,
/// no solo con color: el SMS, la radio y el altavoz no tienen color.
/// </summary>
public sealed record NivelAlerta(string Codigo, string Texto, int Orden, string Color)
{
    public static readonly NivelAlerta Roja = new("roja", "Alerta roja", 4, "#C62828");
    public static readonly NivelAlerta Naranja = new("naranja", "Alerta naranja", 3, "#EF6C00");
    public static readonly NivelAlerta Amarilla = new("amarilla", "Alerta amarilla", 2, "#F9A825");
    public static readonly NivelAlerta Verde = new("verde", "Alerta verde", 1, "#2E7D32");
    public static readonly NivelAlerta SinNivel = new("sin-nivel", "Sin nivel definido", 0, "#616161");

    public static NivelAlerta De(SeveridadCap severidad) => severidad switch
    {
        SeveridadCap.Extreme => Roja,
        SeveridadCap.Severe => Naranja,
        SeveridadCap.Moderate => Amarilla,
        SeveridadCap.Minor => Verde,
        _ => SinNivel
    };
}
