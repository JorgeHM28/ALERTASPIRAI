using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Infrastructure.Persistence;

public class AlertasDbContext(DbContextOptions<AlertasDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Comunidad> Comunidades => Set<Comunidad>();
    public DbSet<Contacto> Contactos => Set<Contacto>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<RegistroBitacoraUsuario> BitacoraUsuarios => Set<RegistroBitacoraUsuario>();
    public DbSet<HistorialContacto> HistorialContactos => Set<HistorialContacto>();
    public DbSet<VersionMensaje> VersionesMensaje => Set<VersionMensaje>();
    public DbSet<ListaDestinos> ListasDestinos => Set<ListaDestinos>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CS_AS");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertasDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Los enums se guardan como texto: la base es legible y no se rompe si se reordena un enum.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
