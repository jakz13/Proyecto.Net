using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ValidarEscaneoCarga;

public class ValidarEscaneoCargaCommandHandler : IRequestHandler<ValidarEscaneoCargaCommand, ResultadoVerificacionCargaDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ValidarEscaneoCargaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<ResultadoVerificacionCargaDto> Handle(ValidarEscaneoCargaCommand request, CancellationToken cancellationToken)
    {
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(request.HojaDeRutaId, cancellationToken);

        var codigosEscaneados = (request.CodigosBultosEscaneados ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToHashSet();

        var bultosAsignados = envios
            .SelectMany(e => e.Bultos.Select(b => new { Envio = e, Bulto = b }))
            .ToList();

        var bultosCorrectos = new List<BultoItemDto>();
        var bultosFaltantes = new List<BultoItemDto>();

        foreach (var item in bultosAsignados)
        {
            var dto = new BultoItemDto(
                item.Bulto.Id,
                item.Envio.Id,
                item.Bulto.Identificacion,
                item.Envio.CodigoSeguimientoPublico,
                item.Envio.NombreDestinatario,
                item.Envio.DireccionDestino,
                item.Bulto.Peso
            );

            if (codigosEscaneados.Contains(item.Bulto.Identificacion))
            {
                bultosCorrectos.Add(dto);
            }
            else
            {
                bultosFaltantes.Add(dto);
            }
        }

        var codigosAsignadosSet = bultosAsignados
            .Select(b => b.Bulto.Identificacion)
            .ToHashSet();

        var bultosSobrantes = codigosEscaneados
            .Where(c => !codigosAsignadosSet.Contains(c))
            .Select(c => new BultoSobranteDto(
                c,
                "El bulto no pertenece a ningún envío asignado a esta hoja de ruta."))
            .ToList();

        var tieneDiscrepancias = bultosFaltantes.Any() || bultosSobrantes.Any();
        var puedeDespachar = bultosCorrectos.Any() && !bultosSobrantes.Any();

        return new ResultadoVerificacionCargaDto(
            request.HojaDeRutaId,
            bultosAsignados.Count,
            codigosEscaneados.Count,
            bultosCorrectos.Count,
            bultosFaltantes.Count,
            bultosSobrantes.Count,
            tieneDiscrepancias,
            puedeDespachar,
            bultosCorrectos,
            bultosFaltantes,
            bultosSobrantes
        );
    }
}
