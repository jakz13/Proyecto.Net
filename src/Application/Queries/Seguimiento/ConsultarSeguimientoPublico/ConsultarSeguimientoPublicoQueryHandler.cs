using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;

public class ConsultarSeguimientoPublicoQueryHandler : IRequestHandler<ConsultarSeguimientoPublicoQuery, SeguimientoPublicoDto?>
{
    private const int LargoMaximoCodigo = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;

    public ConsultarSeguimientoPublicoQueryHandler(
        IEnvioRepository envioRepository,
        IFranjaHorariaRepository franjaHorariaRepository)
    {
        _envioRepository = envioRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
    }

    /// <summary>
    /// Devuelve null cuando el código es inválido o no existe, sin distinguir ambos casos, para no permitir la enumeración de envíos.
    /// </summary>
    public async Task<SeguimientoPublicoDto?> Handle(ConsultarSeguimientoPublicoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var codigo = request.CodigoSeguimiento?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo) || codigo.Length > LargoMaximoCodigo)
            return null;

        var envio = await _envioRepository.GetByCodigoSeguimientoAsync(codigo, cancellationToken);
        if (envio is null)
            return null;

        var hitos = envio.Eventos
            .Where(EsHitoPublico)
            .OrderBy(e => e.Timestamp)
            .Select(e => new HitoSeguimientoDto(e.Timestamp, e.EstadoNuevo, DescribirHito(e)))
            .ToList();

        var ultimaActualizacion = envio.Eventos.Count > 0
            ? envio.Eventos.Max(e => e.Timestamp)
            : envio.FechaAlta;

        return new SeguimientoPublicoDto(
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual,
            await ObtenerVentanaAsync(envio, cancellationToken),
            ultimaActualizacion,
            envio.EstadoActual == EstadoEnvio.Reprogramado,
            hitos);
    }

    /// <summary>Solo los cambios de estado (y el alta) forman parte de la línea de tiempo pública.</summary>
    private static bool EsHitoPublico(EventoEnvio evento) =>
        evento.EstadoAnterior is null
        || evento.EstadoAnterior != evento.EstadoNuevo
        || evento.TipoEvento is "LlegadaGeocerca" or "SalidaGeocerca";

    private async Task<string?> ObtenerVentanaAsync(Envio envio, CancellationToken cancellationToken)
    {
        if (!envio.FranjaHorariaComprometidaId.HasValue)
            return null;

        var franjas = await _franjaHorariaRepository.GetByIdsAsync(new[] { envio.FranjaHorariaComprometidaId.Value }, cancellationToken);
        var franja = franjas.FirstOrDefault();
        return franja is null ? null : $"{franja.HoraInicio:HH\\:mm}-{franja.HoraFin:HH\\:mm}";
    }

    /// <summary>Texto apto para el destinatario: nunca se publica el payload interno ni el responsable del evento.</summary>
    private static string DescribirHito(EventoEnvio evento) => evento.TipoEvento switch
    {
        "AltaEnvio" => "Envío registrado y admitido",
        "RecepcionEnDeposito" => "Envío recibido en el depósito",
        "AsignacionRuta" => "Envío asignado a una ruta de distribución",
        "BultoFaltanteEnCarga" => "Envío devuelto al depósito para una nueva planificación",
        "DespachoRuta" => "El envío salió a reparto",
        "EntregaExitosa" => "Envío entregado",
        "IntentoEntregaFallido" => "No se pudo concretar la entrega",
        "ResolucionReglaOperativa_Reprogramado" => "Entrega reprogramada para un nuevo intento",
        "ResolucionReglaOperativa_EnDevolucion" => "El envío será devuelto al comercio",
        "LlegadaGeocerca" => !string.IsNullOrWhiteSpace(evento.Payload)
            ? evento.Payload
            : "Repartidor ingresó a la zona de entrega (Geocerca < 100m)",
        "SalidaGeocerca" => !string.IsNullOrWhiteSpace(evento.Payload)
            ? evento.Payload
            : "Repartidor salió de la zona de entrega",
        _ => evento.EstadoNuevo switch
        {
            EstadoEnvio.EnDeposito => "Envío en el depósito",
            EstadoEnvio.EnTransito => "Envío en tránsito",
            EstadoEnvio.Devuelto => "Envío devuelto al comercio",
            EstadoEnvio.Extraviado => "El envío no pudo ser ubicado y se está gestionando",
            _ => $"Actualización del estado del envío: {evento.EstadoNuevo}"
        }
    };
}
