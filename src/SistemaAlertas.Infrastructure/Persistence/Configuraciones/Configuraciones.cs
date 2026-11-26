using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Difusion;
using SistemaAlertas.Domain.Mensajes;
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
        // Calculadas: el objeto State sale del estado guardado y EsPrueba del status CAP.
        b.Ignore(a => a.EstadoActual);
        b.Ignore(a => a.EsPrueba);
        b.Property(a => a.IdentificadorCap).HasMaxLength(200).IsRequired();
        b.Property(a => a.Remitente).HasMaxLength(200).IsRequired();
        b.Property(a => a.Evento).HasMaxLength(200).IsRequired();
        b.Property(a => a.Titular).HasMaxLength(300);
        b.Property(a => a.Area).HasColumnType("geometry(Geometry, 4326)");
        b.Property(a => a.XmlCap).IsRequired();
        b.Property(a => a.TipoContenidoOriginal).HasMaxLength(100);
        b.Property(a => a.HashOriginal).HasMaxLength(64);
        b.Property(a => a.Referencias).HasMaxLength(1000);
        b.HasIndex(a => a.CadenaId);
        b.HasIndex(a => a.AptoParaDifusion);
        b.HasIndex(a => new { a.Remitente, a.IdentificadorCap }).IsUnique();
        b.HasIndex(a => a.Enviado);
        b.HasIndex(a => a.Area).HasMethod("gist");
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VersionMensajeConfig : IEntityTypeConfiguration<VersionMensaje>
{
    public void Configure(EntityTypeBuilder<VersionMensaje> b)
    {
        b.ToTable("versiones_mensaje");
        b.Ignore(v => v.EstadoActual);
        b.Ignore(v => v.Nivel);
        b.Ignore(v => v.EsPrueba);
        b.Ignore(v => v.EsVigenteEnCadena);
        b.Ignore(v => v.ReferenciaCap);
        // El contenido (record, Prototype) se guarda en columnas de la misma tabla.
        b.ComplexProperty(v => v.Contenido, c =>
        {
            c.Ignore(x => x.Nivel);
            c.Property(x => x.Evento).HasMaxLength(200).IsRequired();
            c.Property(x => x.Titular).HasMaxLength(300);
            c.Property(x => x.Descripcion).HasMaxLength(4000);
            c.Property(x => x.Instruccion).HasMaxLength(4000);
            c.Property(x => x.DescripcionArea).HasMaxLength(1000);
        });
        b.Property(v => v.Area).HasColumnType("geometry(Geometry, 4326)");
        b.Property(v => v.IdentificadorCap).HasMaxLength(200).IsRequired();
        b.Property(v => v.Remitente).HasMaxLength(200).IsRequired();
        b.Property(v => v.Referencias).HasMaxLength(1000);
        b.Property(v => v.MotivoRechazo).HasMaxLength(1000);
        b.Property(v => v.MotivoDevolucion).HasMaxLength(1000);
        b.HasIndex(v => v.IdentificadorCap).IsUnique();
        b.HasIndex(v => new { v.CadenaId, v.Numero }).IsUnique();
        b.HasIndex(v => v.Estado);
        b.HasIndex(v => v.Area).HasMethod("gist");
        b.HasOne<Aviso>().WithMany().HasForeignKey(v => v.AvisoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Aviso>().WithMany().HasForeignKey(v => v.CadenaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(v => v.ReferenciaVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(v => v.VersionAnteriorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(v => v.ReemplazadaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(v => v.PreparadoPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(v => v.RevisadoPorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(v => v.ProtocoloTitulo).HasMaxLength(200);
        b.Property(v => v.ProtocoloCita).HasMaxLength(400);
        b.HasOne<ProtocoloOficial>().WithMany().HasForeignKey(v => v.ProtocoloId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>RF-05: catálogo de protocolos oficiales. No se borran, se desactivan.</summary>
public class ProtocoloOficialConfig : IEntityTypeConfiguration<ProtocoloOficial>
{
    public void Configure(EntityTypeBuilder<ProtocoloOficial> b)
    {
        b.ToTable("protocolos_oficiales");
        b.Ignore(p => p.Cita);
        b.Property(p => p.Titulo).HasMaxLength(200).IsRequired();
        b.Property(p => p.Instruccion).HasMaxLength(4000).IsRequired();
        b.Property(p => p.Fuente).HasMaxLength(150).IsRequired();
        b.Property(p => p.Referencia).HasMaxLength(200).IsRequired();
        b.HasIndex(p => p.Titulo).IsUnique();
    }
}

public class ListaDestinosConfig : IEntityTypeConfiguration<ListaDestinos>
{
    public void Configure(EntityTypeBuilder<ListaDestinos> b)
    {
        b.ToTable("listas_destinos");
        // Una lista por versión: regenerarla la reemplaza.
        b.HasIndex(l => l.VersionId).IsUnique();
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(l => l.VersionId).OnDelete(DeleteBehavior.Cascade);
        b.PrimitiveCollection(l => l.Canales);
        // RF-07: la clave (lista, canal, dirección) impide duplicar un destino en el mismo envío y versión.
        b.OwnsMany(l => l.Destinos, d =>
        {
            d.ToTable("destinos_envio");
            d.WithOwner().HasForeignKey("ListaDestinosId");
            d.HasKey("ListaDestinosId", nameof(DestinoEnvio.Canal), nameof(DestinoEnvio.Direccion));
            d.Property(x => x.Direccion).HasMaxLength(150).IsRequired();
            d.HasIndex(x => x.ContactoId);
        });
        b.OwnsMany(l => l.Pendientes, p =>
        {
            p.ToTable("comunidades_pendientes");
            p.WithOwner().HasForeignKey("ListaDestinosId");
            p.HasKey("ListaDestinosId", nameof(ComunidadPendiente.ComunidadId));
            p.Property(x => x.Comunidad).HasMaxLength(150).IsRequired();
            p.Property(x => x.Motivo).HasMaxLength(300).IsRequired();
        });
        b.Navigation(l => l.Destinos).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(l => l.Pendientes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

// Sprint 3: difusión (RF-04), seguimiento (RF-08) y bitácora de avisos.

public class CapEmitidoConfig : IEntityTypeConfiguration<CapEmitido>
{
    public void Configure(EntityTypeBuilder<CapEmitido> b)
    {
        b.ToTable("caps_emitidos");
        b.Property(c => c.Identificador).HasMaxLength(200).IsRequired();
        b.Property(c => c.Remitente).HasMaxLength(200).IsRequired();
        b.Property(c => c.Estado).HasMaxLength(16).IsRequired();
        b.Property(c => c.Tipo).HasMaxLength(16).IsRequired();
        b.Property(c => c.Xml).IsRequired();
        b.Property(c => c.Sha256).HasMaxLength(64).IsRequired();
        b.HasIndex(c => c.VersionId).IsUnique();
        b.HasIndex(c => c.Identificador).IsUnique();
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(c => c.VersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Aviso>().WithMany().HasForeignKey(c => c.AvisoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CapEmitido>().WithMany().HasForeignKey(c => c.ReferenciaCapId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Una sola tabla para la jerarquía (TPH): la columna "tipo" distingue el envío SMS de la tarea manual.</summary>
public class TrabajoDifusionConfig : IEntityTypeConfiguration<TrabajoDifusion>
{
    public void Configure(EntityTypeBuilder<TrabajoDifusion> b)
    {
        b.ToTable("trabajos_difusion");
        b.HasDiscriminator<string>("tipo").HasValue<EnvioSms>("EnvioSms").HasValue<TareaManual>("TareaManual");
        b.Property<string>("tipo").HasMaxLength(16);
        b.Property(t => t.Resultado).HasMaxLength(500);
        b.Ignore(t => t.Terminado);
        b.HasIndex(t => t.VersionId);
        b.HasIndex(t => new { t.Estado, t.CreadoEn });
        b.HasOne<VersionMensaje>().WithMany().HasForeignKey(t => t.VersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Comunidad>().WithMany().HasForeignKey(t => t.ComunidadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EnvioSmsConfig : IEntityTypeConfiguration<EnvioSms>
{
    public void Configure(EntityTypeBuilder<EnvioSms> b)
    {
        b.Property(e => e.Destino).HasMaxLength(32);
        b.Property(e => e.Texto).HasMaxLength(1600);
        b.Property(e => e.CodigoProveedor).HasMaxLength(100);
        b.Property(e => e.UltimoError).HasMaxLength(500);
        // La cola busca los pendientes cuyo próximo intento ya llegó.
        b.HasIndex(e => e.ProximoIntento).HasFilter("proximo_intento IS NOT NULL");
        b.HasOne<Contacto>().WithMany().HasForeignKey(e => e.ContactoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TareaManualConfig : IEntityTypeConfiguration<TareaManual>
{
    public void Configure(EntityTypeBuilder<TareaManual> b)
    {
        b.Property(t => t.TextoLegible).HasMaxLength(4000);
        b.Property(t => t.Observacion).HasMaxLength(500);
        b.HasOne<Usuario>().WithMany().HasForeignKey(t => t.ConfirmadoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RegistroEventoAvisoConfig : IEntityTypeConfiguration<RegistroEventoAviso>
{
    public void Configure(EntityTypeBuilder<RegistroEventoAviso> b)
    {
        b.ToTable("eventos_aviso");
        b.Property(r => r.Tipo).HasMaxLength(32).IsRequired();
        b.Property(r => r.Detalle).HasMaxLength(1000).IsRequired();
        b.HasIndex(r => new { r.AvisoId, r.Fecha });
    }
}
