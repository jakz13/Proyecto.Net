using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Comercio.GenerarEtiquetasEnvios;

public class GenerarEtiquetasEnviosQueryHandler : IRequestHandler<GenerarEtiquetasEnviosQuery, EtiquetasGeneradasResultDto>
{
    private const int MaximoEnviosPorLote = 50;

    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public GenerarEtiquetasEnviosQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<EtiquetasGeneradasResultDto> Handle(GenerarEtiquetasEnviosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioIds is null || request.EnvioIds.Count == 0)
            throw new DomainValidationException("Debe seleccionar al menos un envío para generar etiquetas.");

        var idsUnicos = request.EnvioIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idsUnicos.Count == 0)
            throw new DomainValidationException("Los identificadores de envío indicados no son válidos.");

        if (idsUnicos.Count > MaximoEnviosPorLote)
            throw new DomainValidationException($"No se pueden generar etiquetas para más de {MaximoEnviosPorLote} envíos a la vez.");

        var envios = await _envioRepository.GetByIdsAsync(idsUnicos, cancellationToken);
        if (envios.Count != idsUnicos.Count)
            throw new NotFoundException("Uno o más envíos seleccionados no fueron encontrados.");

        var comercioActualId = _currentTenant.ComercioOperadorId;
        var operadorActualId = _currentTenant.OperadorId;

        var etiquetas = new List<EtiquetaBultoItemDto>();

        foreach (var idSolicitado in idsUnicos)
        {
            var envio = envios.First(e => e.Id == idSolicitado);

            // Protección Anti-IDOR: el envío debe pertenecer al operador actual y, si hay sesión de comercio, a ese comercio
            if (operadorActualId != Guid.Empty && envio.OperadorId != operadorActualId)
                throw new UnauthorizedAccessException("Acceso denegado al envío solicitado.");

            if (comercioActualId.HasValue && comercioActualId.Value != Guid.Empty && envio.ComercioId != comercioActualId.Value)
                throw new UnauthorizedAccessException("Acceso denegado: el envío no pertenece al comercio autenticado.");

            // Precondición de UC-27: el envío debe estar en estado Admitido
            if (envio.EstadoActual != EstadoEnvio.Admitido)
                throw new DomainValidationException(
                    $"El envío '{envio.CodigoSeguimientoPublico}' se encuentra en estado '{envio.EstadoActual}'. Solo se pueden generar etiquetas para envíos en estado Admitido.");

            var bultos = envio.Bultos.ToList();
            if (bultos.Count == 0)
            {
                etiquetas.Add(new EtiquetaBultoItemDto(
                    envio.Id,
                    envio.CodigoSeguimientoPublico,
                    $"{envio.CodigoSeguimientoPublico}-B01",
                    1,
                    1,
                    "Comercio Remitente",
                    "Última Milla Express",
                    envio.NombreDestinatario,
                    envio.DireccionDestino,
                    null,
                    envio.ModalidadServicio,
                    envio.PesoTotal > 0 ? envio.PesoTotal : 1.0m,
                    null,
                    envio.FechaAlta));
            }
            else
            {
                for (var i = 0; i < bultos.Count; i++)
                {
                    var bulto = bultos[i];
                    etiquetas.Add(new EtiquetaBultoItemDto(
                        envio.Id,
                        envio.CodigoSeguimientoPublico,
                        bulto.Identificacion,
                        i + 1,
                        bultos.Count,
                        "Comercio Remitente",
                        "Última Milla Express",
                        envio.NombreDestinatario,
                        envio.DireccionDestino,
                        null,
                        envio.ModalidadServicio,
                        bulto.Peso > 0 ? bulto.Peso : 1.0m,
                        null,
                        envio.FechaAlta));
                }
            }
        }

        return new EtiquetasGeneradasResultDto(
            etiquetas,
            idsUnicos.Count,
            etiquetas.Count);
    }
}

