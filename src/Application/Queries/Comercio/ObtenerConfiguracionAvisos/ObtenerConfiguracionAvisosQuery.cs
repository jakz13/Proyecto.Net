using MediatR;
using UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

namespace UltimaMilla.Application.Queries.Comercio.ObtenerConfiguracionAvisos;

/// <summary>
/// Consulta la configuración actual de suscripción a avisos automáticos del comercio en sesión (UC-29).
/// </summary>
public record ObtenerConfiguracionAvisosQuery : IRequest<DetalleConfiguracionAvisosDto>;

public record DetalleConfiguracionAvisosDto(
    Guid ComercioOperadorId,
    string UrlWebhook,
    IReadOnlyList<string> EventosSuscritos,
    string ClaveFirma,
    bool Activa,
    bool EstaConfigurado,
    IReadOnlyList<EventoDisponibleDto> CatalogoEventos,
    IReadOnlyList<AvisoHistorialItemDto> HistorialReciente);

public record EventoDisponibleDto(
    string Codigo,
    string Titulo,
    string Descripcion,
    string Categoria);

