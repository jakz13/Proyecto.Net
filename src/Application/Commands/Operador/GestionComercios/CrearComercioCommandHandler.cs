using System.Security.Cryptography;
using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Exceptions;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Manejador para el registro de un nuevo comercio cliente asociado al operador autenticado.
/// </summary>
public class CrearComercioCommandHandler : IRequestHandler<CrearComercioCommand, ComercioDetalleDto>
{
    private readonly IComercioRepository _comercioRepositorio;
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public CrearComercioCommandHandler(
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

    public async Task<ComercioDetalleDto> Handle(CrearComercioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var documentoNormalizado = (solicitud.Rut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(documentoNormalizado))
            throw new DomainValidationException("El RUT del comercio no puede ser vacío.");

        // Buscar si ya existe la entidad de comercio por su RUT/documento
        var comercioExistente = await _comercioRepositorio.ObtenerPorDocumentoAsync(documentoNormalizado, cancellationToken);
        ComercioEntidad comercioAAsociar;

        if (comercioExistente is not null)
        {
            var yaVinculado = await _comercioOperadorRepositorio.ExisteRelacionAsync(comercioExistente.Id, operadorId, cancellationToken);
            if (yaVinculado)
            {
                throw new DomainValidationException("Ya existe un comercio registrado con ese RUT.");
            }

            comercioAAsociar = comercioExistente;
        }
        else
        {
            comercioAAsociar = new ComercioEntidad(
                Guid.NewGuid(),
                solicitud.RazonSocial,
                documentoNormalizado,
                solicitud.Email,
                solicitud.Telefono,
                solicitud.NombreFantasia,
                solicitud.Direccion);

            await _comercioRepositorio.AgregarAsync(comercioAAsociar, cancellationToken);
        }

        // Se genera la relación con el operador y una huella de clave de API para integraciones
        var claveAleatoria = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var huellaClave = ComercioOperador.CalcularHuellaClaveApi(claveAleatoria);

        var vinculo = new ComercioOperador(
            Guid.NewGuid(),
            comercioAAsociar.Id,
            operadorId,
            huellaClave,
            "Activo");

        await _comercioOperadorRepositorio.AgregarAsync(vinculo, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        return new ComercioDetalleDto(
            comercioAAsociar.Id,
            comercioAAsociar.RazonSocial,
            comercioAAsociar.NombreFantasia,
            comercioAAsociar.Documento,
            comercioAAsociar.Email,
            comercioAAsociar.Telefono,
            comercioAAsociar.Direccion,
            vinculo.EstaActivo,
            vinculo.FechaAlta,
            comercioAAsociar.SelloModificacion.ToString()
        );
    }
}
