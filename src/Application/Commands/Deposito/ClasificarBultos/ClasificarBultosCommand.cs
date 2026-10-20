using MediatR;

namespace UltimaMilla.Application.Commands.Deposito.ClasificarBultos;

/// <summary>Clasificación de bultos de un envío ya recepcionado en depósito.</summary>
public record ClasificarBultosCommand(
    Guid EnvioId,
    IReadOnlyList<ClasificacionBultoDto> Bultos
) : IRequest<ClasificacionResultDto>;

/// <summary>Prioridad admitida: Baja, Normal, Alta o Urgente.</summary>
public record ClasificacionBultoDto(
    string CodigoBulto,
    string Prioridad,
    Guid? ZonaCoberturaId = null,
    string? Tipo = null);

public record ClasificacionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Estado,
    int TotalBultosRecibidos,
    int TotalClasificados,
    IReadOnlyList<string> BultosPendientes,
    bool ListoParaHojaDeRuta);
