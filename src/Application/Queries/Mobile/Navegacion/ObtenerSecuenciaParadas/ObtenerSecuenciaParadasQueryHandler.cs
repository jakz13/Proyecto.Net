using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Mobile.Navegacion.ObtenerSecuenciaParadas;

public class ObtenerSecuenciaParadasQueryHandler : IRequestHandler<ObtenerSecuenciaParadasQuery, List<ParadaNavegacionDto>>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerSecuenciaParadasQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<List<ParadaNavegacionDto>> Handle(ObtenerSecuenciaParadasQuery request, CancellationToken cancellationToken)
    {
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        // Precondición de navegación de calle: La ruta debe estar iniciada/en tránsito
        if (ruta.Estado != "EnTransito" && ruta.Estado != "Despachada")
            throw new DomainValidationException($"Precondición no cumplida: La hoja de ruta debe estar en curso para iniciar la navegación (Estado actual: '{ruta.Estado}').");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(request.HojaDeRutaId, cancellationToken);
        var enviosDict = envios.ToDictionary(e => e.Id);

        var paradasOrdenadas = ruta.Paradas
            .OrderBy(p => p.Orden)
            .ToList();

        var resultado = new List<ParadaNavegacionDto>();

        foreach (var parada in paradasOrdenadas)
        {
            enviosDict.TryGetValue(parada.EnvioId, out var envio);

            var contacto = new DestinatarioContactoDto(
                envio?.NombreDestinatario ?? "Destinatario no especificado",
                Telefono: null,
                Email: null,
                ReferenciaEntrega: null
            );

            var resumenBultos = new ResumenBultosParadaDto(
                envio?.Bultos.Count ?? 0,
                envio?.Bultos.Select(b => b.Identificacion).ToList() ?? new List<string>(),
                envio?.Bultos.Sum(b => b.Peso) ?? 0m
            );

            bool esParadaActual = parada.Estado == "EnCamino" || parada.Estado == "EnSitio";

            resultado.Add(new ParadaNavegacionDto(
                parada.Id,
                parada.EnvioId,
                parada.Orden,
                parada.Estado,
                envio?.CodigoSeguimientoPublico ?? $"PAR-{parada.Orden:D2}",
                envio?.DireccionDestino ?? "Dirección sin especificar",
                Latitud: null,
                Longitud: null,
                VentanaHoraria: parada.FranjaHorariaId.HasValue ? $"Franja-{parada.FranjaHorariaId.Value.ToString()[..4]}" : "Todo el día",
                contacto,
                resumenBultos,
                esParadaActual
            ));
        }

        return resultado;
    }
}
