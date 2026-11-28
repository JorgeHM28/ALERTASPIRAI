using SistemaAlertas.Application.Difusion;

namespace SistemaAlertas.Tests;

/// <summary>El SMS sale sin tildes ni ñ para caber en el alfabeto básico de SMS (160 caracteres por mensaje).</summary>
public class TextoSmsTests
{
    [Fact]
    public void La_alerta_de_la_prueba_real_queda_sin_tildes_y_en_un_solo_mensaje()
    {
        const string original = "ALERTA NARANJA: Alerta naranja por crecida del río Piraí en El Torno. " +
            "Diríjase a la zona alta o al punto de encuentro de su comunidad. No cruce el río.";

        var sms = TextoSms.ParaSms(original);

        Assert.Equal("ALERTA NARANJA: Alerta naranja por crecida del rio Pirai en El Torno. " +
            "Dirijase a la zona alta o al punto de encuentro de su comunidad. No cruce el rio.", sms);
        Assert.All(sms, c => Assert.InRange(c, ' ', '~'));
        Assert.StartsWith("ALERTA NARANJA", sms); // RF-06: el nivel en palabras
        Assert.Equal(1, TextoSms.Partes(sms));
    }

    [Theory]
    [InlineData("Señal de evacuación", "Senal de evacuacion")]
    [InlineData("¿Qué hacer? ¡Salga ya!", "Que hacer? Salga ya!")]
    [InlineData("Pingüino «zona» – 10°", "Pinguino \"zona\" - 10 grados")]
    [InlineData("AÑO\nNUEVO", "ANO NUEVO")]
    [InlineData("Alerta 🔴 roja", "Alerta roja")]
    public void Reemplaza_lo_que_obligaria_a_usar_ucs2(string original, string esperado) =>
        Assert.Equal(esperado, TextoSms.ParaSms(original));

    [Fact]
    public void Un_texto_largo_no_se_corta_se_envia_en_varias_partes()
    {
        var largo = string.Join(' ', Enumerable.Repeat("Diríjase a la zona alta.", 12));
        var sms = TextoSms.ParaSms(largo);
        Assert.EndsWith("Dirijase a la zona alta.", sms);
        Assert.True(TextoSms.Partes(sms) > 1);
    }
}
