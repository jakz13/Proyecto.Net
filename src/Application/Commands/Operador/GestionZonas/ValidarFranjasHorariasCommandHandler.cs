using MediatR;
using UltimaMilla.Application.Services;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Manejador que ejecuta la validación de superposiciones y rangos horarios en las franjas.
/// </summary>
public class ValidarFranjasHorariasCommandHandler : IRequestHandler<ValidarFranjasHorariasCommand>
{
    public Task Handle(ValidarFranjasHorariasCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidadorFranjasHorarias.Validar(solicitud.FranjasHorarias);
        return Task.CompletedTask;
    }
}

