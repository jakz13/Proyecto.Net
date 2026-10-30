using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Manejador para la actualización de datos de un comercio, con validación de concurrencia optimista y Anti-IDOR.
/// </summary>
public class ActualizarComercioCommandHandler : IRequestHandler<ActualizarComercioCommand, ComercioDetalleDto>
{
    private readonly IComercioRepository _comercioRepositorio;
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public ActualizarComercioCommandHandler(
        IComercioRepository comercioRepositorio,
        IComercioOperadorRepository comercioOperadorRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _comercioRepositorio = comercioRepositorio;
        _comercioOperadorRepositorio = comercioOperadorRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<ComercioDetalleDto> Handle(ActualizarComercioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio es inválido.");

        var comercio = await _comercioRepositorio.ObtenerPorIdAsync(solicitud.Id, cancellationToken);
        if (comercio is null)
            throw new NotFoundException("No se encontró el comercio solicitado.");

        // Anti-IDOR: verificar que el comercio pertenezca al operador actual
        var vinculo = await _comercioOperadorRepositorio.ObtenerPorComercioYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (vinculo is null)
            throw new NotFoundException("No se encontró el comercio solicitado.");

        // Concurrencia optimista: validar que el sello de modificación coincida
        if (!Guid.TryParse(solicitud.SelloModificacion, out var selloGuid) || selloGuid != comercio.SelloModificacion)
        {
            throw new ConflictoDeConcurrenciaException("Otra persona modificó este registro, recargá y reintentá.");
        }

        comercio.ActualizarPerfil(
            solicitud.RazonSocial,
            solicitud.NombreFantasia,
            solicitud.Email,
            solicitud.Telefono,
            solicitud.Direccion);

        if (solicitud.Activo)
        {
            vinculo.Activar();
        }
        else
        {
            vinculo.Suspender();
        }

        await _comercioRepositorio.ActualizarAsync(comercio, cancellationToken);
        await _comercioOperadorRepositorio.ActualizarAsync(vinculo, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        return new ComercioDetalleDto(
            comercio.Id,
            comercio.RazonSocial,
            comercio.NombreFantasia,
            comercio.Documento,
            comercio.Email,
            comercio.Telefono,
            comercio.Direccion,
            vinculo.EstaActivo,
            vinculo.FechaAlta,
            comercio.SelloModificacion.ToString()
        );
    }
}

