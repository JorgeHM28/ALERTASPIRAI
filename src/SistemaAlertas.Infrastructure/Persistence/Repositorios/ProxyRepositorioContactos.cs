using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comunidades;

namespace SistemaAlertas.Infrastructure.Persistence.Repositorios;

/// <summary>
/// Patrón Proxy de protección (RF-12). Tiene la misma interfaz que el repositorio real y controla el acceso a él:
/// <list type="bullet">
/// <item>Con permiso de ver contactos (<see cref="IRepositorioContactos.PermisoVerContactos"/>): datos completos.</item>
/// <item>Con sesión pero sin ese permiso: teléfonos y correos enmascarados (por ejemplo, +591 7*****89).</item>
/// <item>Sin sesión (web pública): ningún dígito ni correo, solo el tipo de canal.</item>
/// </list>
/// Los controladores no repiten esta regla: reciben siempre el proxy al pedir <see cref="IRepositorioContactos"/>.
/// </summary>
public sealed class ProxyRepositorioContactos(IRepositorioContactos real, IUsuarioActual usuario) : IRepositorioContactos
{
    public const string Oculto = "(oculto)";

    private enum Acceso { Completo, Enmascarado, Ninguno }

    private Acceso Nivel => usuario.Tiene(IRepositorioContactos.PermisoVerContactos) ? Acceso.Completo
        : usuario.Autenticado ? Acceso.Enmascarado
        : Acceso.Ninguno;

    public async Task<IReadOnlyList<ContactoVista>> ListarPorComunidadAsync(Guid comunidadId, CancellationToken ct = default) =>
        Proteger(await real.ListarPorComunidadAsync(comunidadId, ct));

    public async Task<IReadOnlyList<ContactoVista>> ListarHabilitadosAsync(IReadOnlyCollection<Guid>? zonaIds, CancellationToken ct = default) =>
        Proteger(await real.ListarHabilitadosAsync(zonaIds, ct));

    public async Task<IReadOnlyList<HistorialContactoVista>> HistorialAsync(Guid contactoId, CancellationToken ct = default)
    {
        var historial = await real.HistorialAsync(contactoId, ct);
        var nivel = Nivel;
        if (nivel == Acceso.Completo) return historial;
        return historial.Select(h => h with
        {
            Instantanea = h.Instantanea with
            {
                Canales = h.Instantanea.Canales.Select(c => OcultarCanalTexto(c, nivel)).ToList(),
                ReferenciaAutorizacion = nivel == Acceso.Ninguno ? null : h.Instantanea.ReferenciaAutorizacion
            }
        }).ToList();
    }

    private IReadOnlyList<ContactoVista> Proteger(IReadOnlyList<ContactoVista> contactos)
    {
        var nivel = Nivel;
        if (nivel == Acceso.Completo) return contactos;
        return contactos.Select(c => c with
        {
            Canales = c.Canales.Select(k => k with { Destino = Ocultar(k.Tipo, k.Destino, nivel) }).ToList(),
            ReferenciaAutorizacion = nivel == Acceso.Ninguno ? null : c.ReferenciaAutorizacion,
            DatosOcultos = true
        }).ToList();
    }

    private static string Ocultar(TipoCanal tipo, string destino, Acceso nivel) =>
        nivel == Acceso.Enmascarado ? EnmascaradorDatos.Destino(tipo, destino)
        : tipo == TipoCanal.Radio ? destino
        : Oculto;

    /// <summary>El historial guarda los canales como texto "Sms:+59171234567 (preferido)".</summary>
    private static string OcultarCanalTexto(string canal, Acceso nivel)
    {
        var separador = canal.IndexOf(':');
        if (separador < 0 || !Enum.TryParse<TipoCanal>(canal[..separador], out var tipo)) return Oculto;
        const string marca = " (preferido)";
        var resto = canal[(separador + 1)..];
        var preferido = resto.EndsWith(marca, StringComparison.Ordinal);
        var destino = preferido ? resto[..^marca.Length] : resto;
        return $"{tipo}:{Ocultar(tipo, destino, nivel)}{(preferido ? marca : "")}";
    }
}
