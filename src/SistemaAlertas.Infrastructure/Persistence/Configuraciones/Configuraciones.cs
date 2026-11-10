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
        b.Ignore(u => u.Roles);
        b.Ignore(u => u.Permisos);
        b.OwnsMany(u => u.Asignaciones, a =>
        {
            a.ToTable("usuario_roles");
            a.WithOwner().HasForeignKey("UsuarioId");
            a.HasKey("UsuarioId", nameof(AsignacionRol.Rol));
            a.HasOne<Usuario>().WithMany().HasForeignKey(x => x.AsignadoPorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(u => u.Asignaciones).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class RegistroBitacoraUsuarioConfig : IEntityTypeConfiguration<RegistroBitacoraUsuario>
{
    public void Configure(EntityTypeBuilder<RegistroBitacoraUsuario> b)
    {
        b.ToTable("usuario_bitacora");
        b.Property(r => r.Detalle).HasMaxLength(500);
        b.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(r => r.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.UsuarioId, r.CreadoEn });
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
        b.Property(z => z.FuenteInstitucional).HasMaxLength(150);
        b.Property(z => z.ReferenciaDocumento).HasMaxLength(200);
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
        b.Property(c => c.Procedencia).HasMaxLength(150).IsRequired();
        b.Property(c => c.ReferenciaAutorizacion).HasMaxLength(200);
        b.Ignore(c => c.Habilitado);
        b.Ignore(c => c.MotivosNoHabilitado);
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

public class HistorialContactoConfig : IEntityTypeConfiguration<HistorialContacto>
{
    public void Configure(EntityTypeBuilder<HistorialContacto> b)
    {
        b.ToTable("contacto_historial");
        // Sin clave foránea a contactos ni comunidades: el historial sobrevive a la baja del contacto.
        b.Property(h => h.Instantanea).HasColumnType("jsonb").IsRequired();
        b.Property(h => h.Cambios).HasMaxLength(500);
        b.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.ContactoId, h.CreadoEn });
        b.HasIndex(h => h.ComunidadId);
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
        b.Property(a => a.TipoContenidoOriginal).HasMaxLength(64);
        b.Property(a => a.HashOriginal).HasMaxLength(64);
        b.HasIndex(a => a.AptoParaDifusion);
        b.HasIndex(a => new { a.Remitente, a.IdentificadorCap }).IsUnique();
        b.HasIndex(a => a.Enviado);
        b.HasIndex(a => a.Area).HasMethod("gist");
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
