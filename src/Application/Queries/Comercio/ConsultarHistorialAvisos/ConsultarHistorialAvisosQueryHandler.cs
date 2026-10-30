using MediatR;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Comercio.ConsultarHistorialAvisos;

/// <summary>
/// Recupera y mapea el historial detallado de avisos y webhooks para el comercio autenticado (UC-30).
/// </summary>
public class ConsultarHistorialAvisosQueryHandler : IRequestHandler<ConsultarHistorialAvisosQuery, IReadOnlyList<AvisoHistorialDetalleDto>>
{
    private readonly IAvisoComercioConsultaRepository _avisoRepository;
    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;

    public ConsultarHistorialAvisosQueryHandler(
        IAvisoComercioConsultaRepository avisoRepository,
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant)
    {
        _avisoRepository = avisoRepository;
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<AvisoHistorialDetalleDto>> Handle(ConsultarHistorialAvisosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para consultar el historial de avisos.");
        }

        var suscripciones = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);
        var mapaSuscripciones = suscripciones.ToDictionary(s => s.Id);

        var avisos = await _avisoRepository.ListarHistorialPorComercioAsync(
            comercioOperadorId.Value,
            request.Estado,
            request.EnvioId,
            cancellationToken);

        var lista = new List<AvisoHistorialDetalleDto>();

        foreach (var aviso in avisos)
        {
            mapaSuscripciones.TryGetValue(aviso.SuscripcionAvisoId, out var suscripcion);

            var tipoEvento = suscripcion?.TipoEvento ?? "Aviso Automático";
            var urlDestino = suscripcion?.UrlWebhook ?? string.Empty;

            // Extrae código de seguimiento sugerido del payload o fallback
            var codigoSeguimiento = ExtraerCodigoSeguimiento(aviso.Payload, aviso.EnvioId);

            if (!string.IsNullOrWhiteSpace(request.CodigoSeguimiento) &&
                !codigoSeguimiento.Contains(request.CodigoSeguimiento.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var codigoHttp = DeterminarCodigoHttp(aviso.Estado, aviso.UltimaRespuestaHttp);

            lista.Add(new AvisoHistorialDetalleDto(
                Id: aviso.Id,
                SuscripcionAvisoId: aviso.SuscripcionAvisoId,
                EnvioId: aviso.EnvioId,
                CodigoSeguimiento: codigoSeguimiento,
                TipoEvento: tipoEvento,
                UrlWebhook: urlDestino,
                Payload: aviso.Payload,
                FirmaEnviada: aviso.FirmaEnviada,
                Estado: aviso.Estado,
                Intentos: aviso.Intentos,
                UltimaRespuestaHttp: aviso.UltimaRespuestaHttp,
                CodigoRespuestaHttp: codigoHttp,
                Timestamp: aviso.Timestamp,
                ProximoReintento: aviso.ProximoReintento));
        }

        return lista;
    }

    private static string ExtraerCodigoSeguimiento(string payload, Guid envioId)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return $"ENV-{envioId.ToString()[..8].ToUpperInvariant()}";

        const string clave = "\"codigoSeguimiento\"";
        var indiceClave = payload.IndexOf(clave, StringComparison.OrdinalIgnoreCase);
        if (indiceClave >= 0)
        {
            var inicioValor = payload.IndexOf(':', indiceClave);
            if (inicioValor >= 0)
            {
                var inicioComilla = payload.IndexOf('"', inicioValor);
                if (inicioComilla >= 0)
                {
                    var finComilla = payload.IndexOf('"', inicioComilla + 1);
                    if (finComilla > inicioComilla)
                    {
                        return payload.Substring(inicioComilla + 1, finComilla - inicioComilla - 1);
                    }
                }
            }
        }

        return $"TRK-{envioId.ToString()[..8].ToUpperInvariant()}";
    }

    private static int DeterminarCodigoHttp(string estado, string? respuestaHttp)
    {
        if (estado == "Entregado")
            return 200;

        if (!string.IsNullOrWhiteSpace(respuestaHttp))
        {
            if (respuestaHttp.Contains("404")) return 404;
            if (respuestaHttp.Contains("500")) return 500;
            if (respuestaHttp.Contains("502")) return 502;
            if (respuestaHttp.Contains("503")) return 503;
            if (respuestaHttp.Contains("504")) return 504;
        }

        return estado switch
        {
            "Fallido" => 500,
            "Agotado" => 504,
            "Reintentando" => 503,
            _ => 200
        };
    }
}

