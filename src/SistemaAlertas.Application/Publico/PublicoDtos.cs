using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Application.Publico;

/// <summary>
/// Lo que ve cualquier persona en la web pública. Solo datos del mensaje aprobado: nunca contactos, usuarios
/// ni el aviso oficial interno.
/// </summary>
/// <param name="Vigente">False si la versión fue reemplazada por un Update/Cancel o ya expiró (se sigue mostrando si se abre su enlace).</param>
public sealed record AvisoPublico(Guid Id, int Numero, TipoMensajeCap Tipo, NivelVista Nivel, string Evento, string? Titular,
    string? Descripcion, string? Instruccion, string? DescripcionArea, DateTimeOffset? Desde, DateTimeOffset? Hasta,
    DateTimeOffset? AprobadoEn, bool Vigente, Guid? ReemplazadoPor, JsonElement? Area, string TextoLectura);

/// <summary>Web pública (página de inicio sin sesión): avisos aprobados con texto, nivel, instrucciones y mapa (RF-06).</summary>



