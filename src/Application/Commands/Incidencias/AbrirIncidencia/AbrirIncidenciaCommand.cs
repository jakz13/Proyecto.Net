using MediatR;

namespace UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;

/// <summary>El despachador registra una incidencia; el envío y la hoja de ruta afectados son opcionales.</summary>
public record AbrirIncidenciaCommand(
    string Tipo,
    string Descripcion,
    string? Severidad = null,
    Guid? EnvioId = null,
    Guid? HojaDeRutaId = null
) : IRequest<IncidenciaResultDto>;

public record IncidenciaResultDto(
    Guid Id,
    string Tipo,
    string Severidad,
    string Estado,
    Guid? EnvioId,
    Guid? HojaDeRutaId,
    DateTime FechaApertura);
