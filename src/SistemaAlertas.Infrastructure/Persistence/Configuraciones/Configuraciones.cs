using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Infrastructure.Persistence.Configuraciones;

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuarios");
        b.Property(u => u.NombreUsuario).HasMaxLength(64).IsRequired();
        b.Property(u => u.NombreCompleto).HasMaxLength(150).IsRequired();
        b.Property(u => u.Email).HasMaxLength(150).IsRequired();
        b.Property(u => u.HashContrasena).IsRequired();
        b.HasIndex(u => u.NombreUsuario).IsUnique();
        b.HasIndex(u => u.Email).IsUnique();
    }
}

public class ZonaConfig : IEntityTypeConfiguration<Zona>
{
    public void Configure(EntityTypeBuilder<Zona> b)
    {
        b.ToTable("zonas");
        b.Property(z => z.Nombre).HasMaxLength(150).IsRequired();
        b.Property(z => z.Descripcion).HasMaxLength(1000);
        b.Property(z => z.Area).HasColumnType("geometry(Geometry, 4326)").IsRequired();
        b.HasIndex(z => z.Nombre).IsUnique();
        b.HasIndex(z => z.Area).HasMethod("gist");
    }
}

public class ComunidadConfig : IEntityTypeConfiguration<Comunidad>
{
    public void Configure(EntityTypeBuilder<Comunidad> b)
    {
        b.ToTable("comunidades");
        b.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        b.Property(c => c.Municipio).HasMaxLength(150);
        b.Property(c => c.Ubicacion).HasColumnType("geometry(Point, 4326)");
        b.HasOne(c => c.Zona).WithMany().HasForeignKey(c => c.ZonaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(c => c.Contactos).WithOne().HasForeignKey(c => c.ComunidadId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(c => c.Contactos).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(c => new { c.ZonaId, c.Nombre }).IsUnique();
    }
}

public class ContactoConfig : IEntityTypeConfiguration<Contacto>
{
    public void Configure(EntityTypeBuilder<Contacto> b)
    {
        b.ToTable("contactos");
        b.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        b.Property(c => c.Cargo).HasMaxLength(100);
        b.OwnsMany(c => c.Canales, canal =>
        {
            canal.ToTable("contacto_canales");
            canal.WithOwner().HasForeignKey("ContactoId");
            canal.Property<int>("Id");
            canal.HasKey("Id");
            canal.Property(x => x.Destino).HasMaxLength(150).IsRequired();
        });
        b.Navigation(c => c.Canales).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class AvisoConfig : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> b)
    {
        b.ToTable("avisos");
        b.Property(a => a.IdentificadorCap).HasMaxLength(200).IsRequired();
        b.Property(a => a.Remitente).HasMaxLength(200).IsRequired();
        b.Property(a => a.Evento).HasMaxLength(200).IsRequired();
        b.Property(a => a.Titular).HasMaxLength(300);
        b.Property(a => a.Area).HasColumnType("geometry(Geometry, 4326)");
        b.Property(a => a.XmlCap).IsRequired();
        b.HasIndex(a => new { a.Remitente, a.IdentificadorCap }).IsUnique();
        b.HasIndex(a => a.Enviado);
        b.HasIndex(a => a.Area).HasMethod("gist");
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
