using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Operador.GestionComercios;

/// <summary>
/// Manejador para obtener el detalle de un comercio por su identificador.
/// </summary>
public class ObtenerComercioPorIdQueryHandler : IRequestHandler<ObtenerComercioPorIdQuery, ComercioDetalleDto>
{
    private readonly IComercioRepository _comercioRepositorio;
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ObtenerComercioPorIdQueryHandler(
        IComercioRepository comercioRepositorio,
        IComercioOperadorRepository comercioOperadorRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _comercioRepositorio = comercioRepositorio;
        _comercioOperadorRepositorio = comercioOperadorRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<ComercioDetalleDto> Handle(ObtenerComercioPorIdQuery solicitud, CancellationToken cancellationToken)
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

        var vinculo = await _comercioOperadorRepositorio.ObtenerPorComercioYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (vinculo is null)
            throw new NotFoundException("No se encontró el comercio solicitado.");

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

