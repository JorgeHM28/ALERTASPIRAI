using SistemaAlertas.Domain.Difusion;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Application.Abstracciones;

public interface IAppDbContext
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<Zona> Zonas { get; }
    DbSet<Comunidad> Comunidades { get; }
    DbSet<Contacto> Contactos { get; }
    DbSet<Aviso> Avisos { get; }
    DbSet<RegistroBitacoraUsuario> BitacoraUsuarios { get; }
    DbSet<HistorialContacto> HistorialContactos { get; }
    DbSet<VersionMensaje> VersionesMensaje { get; }
    DbSet<ListaDestinos> ListasDestinos { get; }
    DbSet<ProtocoloOficial> Protocolos { get; }
    DbSet<CapEmitido> CapsEmitidos { get; }
    DbSet<TrabajoDifusion> TrabajosDifusion { get; }
    DbSet<RegistroEventoAviso> EventosAviso { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda y, si falla, deja de seguir los cambios pendientes: así un canal de difusión que falla no arrastra
    /// su error a los canales siguientes, que comparten el mismo contexto.
    /// </summary>
    async Task GuardarODescartarAsync(CancellationToken ct = default)
    {
        try { await SaveChangesAsync(ct); }
        catch { DescartarCambios(); throw; }
    }

    void DescartarCambios();
}
