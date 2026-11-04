using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comunidades;
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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
