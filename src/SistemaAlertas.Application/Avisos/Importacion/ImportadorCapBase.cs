using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Patrón Template Method. <see cref="Importar"/> fija los pasos que comparten todos los formatos CAP:
/// leer el documento, llenar el encabezado &lt;alert&gt;, llenar cada &lt;info&gt; y construir con el Builder,
/// que valida las reglas de CAP 1.2. Cada formato (XML, JSON) solo implementa los pasos que cambian.
/// Así ningún importador puede saltarse la validación del Builder ni armar el mensaje en otro orden.
/// </summary>
/// <typeparam name="TAlerta">Cómo representa el formato el &lt;alert&gt; leído (un XElement, un objeto JSON).</typeparam>
/// <typeparam name="TInfo">Cómo representa cada bloque &lt;info&gt;.</typeparam>
public abstract class ImportadorCapBase<TAlerta, TInfo> : IImportadorAviso
{
    public abstract FormatoAviso Formato { get; }

    /// <summary>Primer carácter con el que empieza un documento de este formato ('&lt;' o '{').</summary>
    protected abstract char PrimerCaracter { get; }

    /// <summary>Gancho (hook): la detección por defecto mira el primer carácter; un formato puede cambiarla.</summary>
    public virtual bool Reconoce(string contenido) => contenido.TrimStart().StartsWith(PrimerCaracter);

    /// <summary>El método plantilla: el orden de los pasos es fijo y no se puede redefinir.</summary>
    public MensajeCap Importar(string contenido)
    {
        var alerta = Leer(contenido);
        var builder = new MensajeCapBuilder();
        LlenarEncabezado(alerta, builder);
        foreach (var info in Infos(alerta))
            builder.AgregarInfo(i => LlenarInfo(info, i));
        return builder.Build();
    }

    /// <summary>Paso 1: interpreta el texto. Traduce los errores de formato a <c>ExcepcionDominio</c>.</summary>
    protected abstract TAlerta Leer(string contenido);

    /// <summary>Paso 2: identifier, sender, sent, status, msgType, scope, note y references.</summary>
    protected abstract void LlenarEncabezado(TAlerta alerta, MensajeCapBuilder builder);

    /// <summary>Paso 3: los bloques &lt;info&gt; del mensaje.</summary>
    protected abstract IEnumerable<TInfo> Infos(TAlerta alerta);

    /// <summary>Paso 4: evento, urgencia, severidad, certeza, textos, fechas, categorías y áreas de un &lt;info&gt;.</summary>
    protected abstract void LlenarInfo(TInfo info, InfoCapBuilder builder);
}
