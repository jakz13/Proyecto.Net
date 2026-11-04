using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Comando para actualizar los datos, polígono y franjas horarias de una zona de cobertura (UC-02).
/// </summary>
public record ActualizarZonaCoberturaCommand(
    Guid Id,
    string Nombre,
    string PoligonoGeografico,
    string ColorHex,
    bool Activa,
    IReadOnlyList<CrearFranjaHorariaDto>? FranjasHorarias
) : IRequest<ZonaCoberturaDetalleDto>;

