using MediatR;
using UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Comercio.ObtenerConfiguracionAvisos;

public class ObtenerConfiguracionAvisosQueryHandler : IRequestHandler<ObtenerConfiguracionAvisosQuery, DetalleConfiguracionAvisosDto>
{
    private static readonly IReadOnlyList<EventoDisponibleDto> CatalogoCompleto = new List<EventoDisponibleDto>
    {
        new("EnvioAdmitido", "Admisión de Envío", "Emitido cuando el paquete es admitido por API, carga masiva o registro manual.", "Ingreso"),
        new("EnDeposito", "Recepción en Depósito", "Emitido cuando el envío ingresa al depósito central y queda clasificado.", "Operación"),
        new("EnTransito", "Despacho en Ruta", "Emitido cuando el repartidor carga el vehículo e inicia la distribución.", "Distribución"),
        new("Entregado", "Entrega Exitosa", "Emitido cuando el destinatario recibe el bulto y se firma la conformidad.", "Finalización"),
        new("NoEntregado", "Intento Fallido", "Emitido cuando no fue posible concretar la entrega en el domicilio.", "Novedad"),
        new("Reprogramado", "Envío Reprogramado", "Emitido cuando se acuerda una nueva fecha o franja de entrega.", "Gestión"),
        new("Extraviado", "Incidencia / Extravío", "Emitido si se registra una discrepancia crítica o extravío de bulto.", "Alerta")
    };

    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly IAvisoComercioConsultaRepository? _avisoComercioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerConfiguracionAvisosQueryHandler(
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant,
        IAvisoComercioConsultaRepository? avisoComercioRepository = null)
    {
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
        _avisoComercioRepository = avisoComercioRepository;
    }

    public async Task<DetalleConfiguracionAvisosDto> Handle(ObtenerConfiguracionAvisosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para consultar la configuración de avisos.");
        }

        var suscripciones = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);

        if (suscripciones.Count == 0)
        {
            return new DetalleConfiguracionAvisosDto(
                ComercioOperadorId: comercioOperadorId.Value,
                UrlWebhook: string.Empty,
                EventosSuscritos: Array.Empty<string>(),
                ClaveFirma: string.Empty,
                Activa: false,
                EstaConfigurado: false,
                CatalogoEventos: CatalogoCompleto,
                HistorialReciente: Array.Empty<AvisoHistorialItemDto>());
        }

        var primera = suscripciones[0];
        var eventos = suscripciones.Select(s => s.TipoEvento).Distinct().ToList();
        var activa = suscripciones.Any(s => s.Activa);

        IReadOnlyList<AvisoHistorialItemDto> historial = Array.Empty<AvisoHistorialItemDto>();
        if (_avisoComercioRepository != null)
        {
            var avisos = await _avisoComercioRepository.ListarRecientesPorComercioAsync(comercioOperadorId.Value, 10, cancellationToken);
            historial = avisos.Select(a => new AvisoHistorialItemDto(
                a.Id,
                "Aviso automático",
                "ENV-HIST",
                a.Estado,
                a.Estado == "Entregado" ? 200 : 500,
                a.Timestamp,
                a.Intentos)).ToList();
        }

        return new DetalleConfiguracionAvisosDto(
            ComercioOperadorId: comercioOperadorId.Value,
            UrlWebhook: primera.UrlWebhook,
            EventosSuscritos: eventos,
            ClaveFirma: primera.ClaveFirma,
            Activa: activa,
            EstaConfigurado: true,
            CatalogoEventos: CatalogoCompleto,
            HistorialReciente: historial);
    }
}

