using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Infrastructure.Persistence.Repositorios;

namespace SistemaAlertas.Tests;

internal sealed class UsuarioFalso(bool autenticado, params Rol[] roles) : IUsuarioActual
{
    public bool Autenticado => autenticado;
    public bool Tiene(Permiso permiso) => autenticado && MatrizPermisos.De(roles).Contains(permiso);
}

/// <summary>Proxy de protección: enmascara los contactos de RF-12 según el permiso del usuario.</summary>
public class ProxyRepositorioContactosTests
{
    private static readonly Guid ComunidadId = Guid.NewGuid();

    private static ContactoVista Contacto() => new(Guid.NewGuid(), ComunidadId, "Juana Pérez", "Corregidora", true,
        [new CanalVista(TipoCanal.Sms, "+59171234589", true), new CanalVista(TipoCanal.Email, "juana.perez@porongo.bo", false),
         new CanalVista(TipoCanal.Radio, "Radio Porongo 98.5 FM", false)],
        "Asamblea comunal", true, "Acta 15/2026", new DateOnly(2026, 10, 1), true, []);

    private sealed class RepositorioFalso : IRepositorioContactos
    {
        public int Llamadas { get; private set; }
        public Task<IReadOnlyList<ContactoVista>> ListarPorComunidadAsync(Guid comunidadId, CancellationToken ct = default)
        { Llamadas++; return Task.FromResult<IReadOnlyList<ContactoVista>>([Contacto()]); }
        public Task<IReadOnlyList<ContactoVista>> ListarHabilitadosAsync(IReadOnlyCollection<Guid>? zonaIds, CancellationToken ct = default)
            => ListarPorComunidadAsync(ComunidadId, ct);
        public Task<IReadOnlyList<HistorialContactoVista>> HistorialAsync(Guid contactoId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<HistorialContactoVista>>([new(Guid.NewGuid(), DateTimeOffset.UtcNow, AccionContacto.Alta,
                Guid.NewGuid(), null, new InstantaneaContacto("Juana Pérez", null, true, "Asamblea", true, "Acta 15/2026", null,
                    ["Sms:+59171234589 (preferido)", "Email:juana.perez@porongo.bo", "Radio:Radio Porongo 98.5 FM"]))]);
    }

    private static async Task<ContactoVista> VerComo(IUsuarioActual usuario) =>
        Assert.Single(await new ProxyRepositorioContactos(new RepositorioFalso(), usuario).ListarPorComunidadAsync(ComunidadId));

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Administrador)]
    public async Task Quien_mantiene_contactos_ve_los_numeros_completos(Rol rol)
    {
        var c = await VerComo(new UsuarioFalso(true, rol));
        Assert.Equal("+59171234589", c.Canales[0].Destino);
        Assert.False(c.DatosOcultos);
    }

    [Theory]
    [InlineData(Rol.Consulta)]
    [InlineData(Rol.Aprobador)]
    public async Task Sin_permiso_los_numeros_y_correos_salen_enmascarados(Rol rol)
    {
        var c = await VerComo(new UsuarioFalso(true, rol));

        Assert.True(c.DatosOcultos);
        Assert.Equal("+591 7*****89", c.Canales.Single(k => k.Tipo == TipoCanal.Sms).Destino);
        Assert.Equal("j***@porongo.bo", c.Canales.Single(k => k.Tipo == TipoCanal.Email).Destino);
        Assert.Equal("Radio Porongo 98.5 FM", c.Canales.Single(k => k.Tipo == TipoCanal.Radio).Destino);
        Assert.Equal("Juana Pérez", c.Nombre);
    }

    [Fact]
    public async Task La_web_publica_nunca_recibe_numeros()
    {
        var c = await VerComo(new UsuarioFalso(false));

        Assert.True(c.DatosOcultos);
        Assert.Null(c.ReferenciaAutorizacion);
        foreach (var canal in c.Canales.Where(k => k.Tipo != TipoCanal.Radio))
        {
            Assert.Equal(ProxyRepositorioContactos.Oculto, canal.Destino);
            Assert.DoesNotContain(canal.Destino, char.IsDigit);
        }
    }

    [Fact]
    public async Task El_historial_tambien_se_enmascara()
    {
        var proxy = new ProxyRepositorioContactos(new RepositorioFalso(), new UsuarioFalso(true, Rol.Consulta));

        var canales = Assert.Single(await proxy.HistorialAsync(Guid.NewGuid())).Instantanea.Canales;

        Assert.Equal(["Sms:+591 7*****89 (preferido)", "Email:j***@porongo.bo", "Radio:Radio Porongo 98.5 FM"], canales);
    }

    [Fact]
    public async Task El_proxy_delega_en_el_repositorio_real()
    {
        var real = new RepositorioFalso();
        await new ProxyRepositorioContactos(real, new UsuarioFalso(true, Rol.Consulta)).ListarPorComunidadAsync(ComunidadId);
        Assert.Equal(1, real.Llamadas);
    }

    [Theory]
    [InlineData("+59171234589", "+591 7*****89")]
    [InlineData("71234589", "7*****89")]
    [InlineData("+14155550123", "+141 5*****23")]
    [InlineData("12", "**")]
    public void Enmascara_telefonos(string telefono, string esperado)
    {
        Assert.Equal(esperado, EnmascaradorDatos.Telefono(telefono));
    }
}
