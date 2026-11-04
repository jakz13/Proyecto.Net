using MediatR;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Comando para activar o dar de baja lógica a una zona de cobertura (UC-02).
/// </summary>
public record CambiarEstadoZonaCoberturaCommand(
    Guid Id,
    bool Activa
) : IRequest;

