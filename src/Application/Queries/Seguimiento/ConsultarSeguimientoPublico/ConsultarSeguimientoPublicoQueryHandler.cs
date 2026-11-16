using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;

public class ConsultarSeguimientoPublicoQueryHandler : IRequestHandler<ConsultarSeguimientoPublicoQuery, SeguimientoPublicoDto?>
{
    private const int LargoMaximoCodigo = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IOperadorRepository? _operadorRepository;
    private readonly IDistributedCacheService? _cacheService;
    private readonly IRetiroRepository? _retiroRepository;
    private readonly IFuncionalidadesService? _funcionalidades;

    /// <summary>Prefijo de los códigos de retiro: el comprador usa el mismo enlace público que para los envíos.</summary>
    public const string PrefijoCodigoRetiro = "RET-";

    public ConsultarSeguimientoPublicoQueryHandler(
        IEnvioRepository envioRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IDistributedCacheService? cacheService = null,
        IRetiroRepository? retiroRepository = null,
        IFuncionalidadesService? funcionalidades = null,
        IOperadorRepository? operadorRepository = null)
    {
        _funcionalidades = funcionalidades;
        _envioRepository = envioRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _cacheService = cacheService;
        _retiroRepository = retiroRepository;
        _operadorRepository = operadorRepository;
    }

    /// <summary>
    /// Devuelve null cuando el código es inválido o no existe, sin distinguir ambos casos, para no permitir la enumeración de envíos.
    /// Utiliza el caché distribuido con expiración corta (5 minutos) e invalidación ante eventos del envío.
    /// Incorpora el avance de entrega de bultos sin exponer identificadores internos.
    /// </summary>
    public async Task<SeguimientoPublicoDto?> Handle(ConsultarSeguimientoPublicoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var codigo = request.CodigoSeguimiento?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo) || codigo.Length > LargoMaximoCodigo)
            return null;

        var claveCache = ClavesCache.SeguimientoPorCodigo(codigo);
        if (_cacheService is not null)
        {
            var enCache = await _cacheService.GetAsync<SeguimientoEnCache>(claveCache, cancellationToken);
            if (enCache?.Seguimiento is not null)
                return await AplicarBanderaAsync(enCache, cancellationToken);
        }

        // Un retiro se sigue por el mismo enlace; sin distinguir "no existe" de "código inválido", igual que con los envíos
        if (codigo.StartsWith(PrefijoCodigoRetiro, StringComparison.Ordinal))
            return await ConsultarRetiroAsync(codigo, claveCache, cancellationToken);

        var envio = await _envioRepository.GetByCodigoSeguimientoAsync(codigo, cancellationToken);
        if (envio is null)
            return null;

        var operativos = envio.Bultos
            .Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion)
            .ToList();

        int totalBultos = operativos.Count;
        int bultosEntregados = envio.EstadoActual == EstadoEnvio.Entregado
            ? totalBultos
            : operativos.Count(b => b.EstaEntregado);
        int bultosPendientes = Math.Max(0, totalBultos - bultosEntregados);
        string avanceEntrega = $"{bultosEntregados} de {totalBultos} bultos entregados";

        var hitos = envio.Eventos
            .Where(EsHitoPublico)
            .OrderBy(e => e.Timestamp)
            .Select(e => new HitoSeguimientoDto(e.Timestamp, e.EstadoNuevo, DescribirHito(e, bultosEntregados, totalBultos)))
            .ToList();

        var ultimaActualizacion = envio.Eventos.Count > 0
            ? envio.Eventos.Max(e => e.Timestamp)
            : envio.FechaAlta;

        string? operadorNombre = null;
        string? colorPrimario = null;
        string? colorSecundario = null;
        string? logoUrl = null;

        if (_operadorRepository is not null && envio.OperadorId != Guid.Empty)
        {
            var operador = await _operadorRepository.GetByIdAsync(envio.OperadorId, cancellationToken);
            var identidad = await _operadorRepository.ObtenerIdentidadAsync(envio.OperadorId, cancellationToken);
            operadorNombre = operador?.NombreComercial;
            colorPrimario = identidad?.ColorPrimario;
            colorSecundario = identidad?.ColorSecundario;
            logoUrl = identidad?.LogoUrl;
        }

        // Mismos estados que acepta el caso de uso: el botón no depende de que el envío ya esté reprogramado
        var estadoPermite = envio.EstadoActual is EstadoEnvio.Admitido or EstadoEnvio.EnDeposito or EstadoEnvio.AsignadoARuta or EstadoEnvio.EnTransito or EstadoEnvio.Reprogramado;

        var resultado = new SeguimientoPublicoDto(
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual,
            await ObtenerVentanaAsync(envio, cancellationToken),
            ultimaActualizacion,
            estadoPermite,
            hitos,
            totalBultos,
            bultosEntregados,
            bultosPendientes,
            avanceEntrega,
            OperadorNombre: operadorNombre,
            OperadorColorPrimario: colorPrimario,
            OperadorColorSecundario: colorSecundario,
            OperadorLogoUrl: logoUrl);

        if (_cacheService is not null)
        {
            // Se guarda lo que depende del envío; la bandera de reprogramación se evalúa en cada consulta para que un cambio de la plataforma rija al instante
            var envoltorio = new SeguimientoEnCache(resultado, envio.OperadorId);
            await _cacheService.SetAsync(claveCache, envoltorio, ClavesCache.ExpiracionSeguimientoPublico, cancellationToken);
            await _cacheService.SetAsync(
                ClavesCache.SeguimientoPorOperadorYCodigo(envio.OperadorId, envio.CodigoSeguimientoPublico),
                envoltorio,
                ClavesCache.ExpiracionSeguimientoPublico,
                cancellationToken);
        }

        return await AplicarBanderaAsync(new SeguimientoEnCache(resultado, envio.OperadorId), cancellationToken);
    }

    /// <summary>Lo que se guarda en caché: el seguimiento según el estado del envío y el operador dueño, para evaluar sus banderas en cada consulta.</summary>
    public sealed record SeguimientoEnCache(SeguimientoPublicoDto? Seguimiento, Guid OperadorId);

    private async Task<SeguimientoPublicoDto> AplicarBanderaAsync(SeguimientoEnCache enCache, CancellationToken cancellationToken)
    {
        var dto = enCache.Seguimiento!;
        if (!dto.PermiteReprogramacion || _funcionalidades is null || enCache.OperadorId == Guid.Empty)
            return dto;

        var habilitada = await _funcionalidades.EstaHabilitadaAsync(enCache.OperadorId, Domain.Entities.Operador.Funcionalidad.ReprogramacionPublica, cancellationToken);
        return habilitada ? dto : dto with { PermiteReprogramacion = false };
    }

    /// <summary>
    /// Vista pública de un retiro: estado, ventana, avance y línea de tiempo, sin nombres, domicilios, contactos ni identificadores internos.
    /// </summary>
    private async Task<SeguimientoPublicoDto?> ConsultarRetiroAsync(string codigo, string claveCache, CancellationToken cancellationToken)
    {
        if (_retiroRepository is null)
            return null;

        var retiro = await _retiroRepository.GetByCodigoSeguimientoAsync(codigo, cancellationToken);
        if (retiro is null)
            return null;

        string? ventana = null;
        if (retiro.FranjaHorariaId.HasValue)
        {
            var franja = (await _franjaHorariaRepository.GetByIdsAsync(new[] { retiro.FranjaHorariaId.Value }, cancellationToken)).FirstOrDefault();
            ventana = franja is null ? null : $"{franja.HoraInicio:HH\\:mm}-{franja.HoraFin:HH\\:mm}";
        }

        var total = retiro.Bultos.Count;
        var retirados = retiro.EstadoActual is EstadoRetiro.Retirado or EstadoRetiro.EnDeposito or EstadoRetiro.EntregadoAlComercio ? total : 0;

        var hitos = retiro.Eventos
            .OrderBy(e => e.Timestamp)
            .Select(e => new HitoSeguimientoDto(e.Timestamp, EstadoEnvio.Admitido, DescribirHitoRetiro(e), e.EstadoNuevo.ToString()))
            .ToList();

        string? operadorNombre = null;
        string? colorPrimario = null;
        string? colorSecundario = null;
        string? logoUrl = null;

        if (_operadorRepository is not null && retiro.OperadorId != Guid.Empty)
        {
            var operador = await _operadorRepository.GetByIdAsync(retiro.OperadorId, cancellationToken);
            var identidad = await _operadorRepository.ObtenerIdentidadAsync(retiro.OperadorId, cancellationToken);
            operadorNombre = operador?.NombreComercial;
            colorPrimario = identidad?.ColorPrimario;
            colorSecundario = identidad?.ColorSecundario;
            logoUrl = identidad?.LogoUrl;
        }

        var resultado = new SeguimientoPublicoDto(
            retiro.CodigoSeguimientoPublico,
            EstadoEnvio.Admitido,
            ventana,
            retiro.Eventos.Count > 0 ? retiro.Eventos.Max(e => e.Timestamp) : retiro.FechaSolicitud,
            false,
            hitos,
            total,
            retirados,
            total - retirados,
            $"{retirados} de {total} bultos retirados",
            "Retiro",
            retiro.EstadoActual.ToString(),
            DescribirEstadoRetiro(retiro.EstadoActual),
            OperadorNombre: operadorNombre,
            OperadorColorPrimario: colorPrimario,
            OperadorColorSecundario: colorSecundario,
            OperadorLogoUrl: logoUrl);

        if (_cacheService is not null)
        {
            var envoltorio = new SeguimientoEnCache(resultado, Guid.Empty);
            await _cacheService.SetAsync(claveCache, envoltorio, ClavesCache.ExpiracionSeguimientoPublico, cancellationToken);
            await _cacheService.SetAsync(
                ClavesCache.SeguimientoPorOperadorYCodigo(retiro.OperadorId, retiro.CodigoSeguimientoPublico),
                envoltorio,
                ClavesCache.ExpiracionSeguimientoPublico,
                cancellationToken);
        }

        return resultado;
    }

    private static string DescribirEstadoRetiro(EstadoRetiro estado) => estado switch
    {
        EstadoRetiro.Solicitado => "Retiro solicitado: estamos programando la visita",
        EstadoRetiro.AsignadoARuta => "Retiro programado: un repartidor pasará a retirar los bultos",
        EstadoRetiro.Retirado => "Los bultos ya fueron retirados",
        EstadoRetiro.NoRetirado => "No se pudo concretar el retiro; se coordinará una nueva visita",
        EstadoRetiro.EnDeposito => "Los bultos retirados están en el depósito",
        EstadoRetiro.EntregadoAlComercio => "Los bultos llegaron al comercio",
        EstadoRetiro.Cancelado => "El retiro fue cancelado",
        _ => estado.ToString()
    };

    /// <summary>Texto apto para el comprador: nunca se publica el detalle interno del evento.</summary>
    private static string DescribirHitoRetiro(EventoRetiro evento) => evento.TipoEvento switch
    {
        "SolicitudRetiro" => "Retiro solicitado por el comercio",
        "AsignacionRuta" => "Retiro programado en una ruta de distribución",
        "RetiroRealizado" => "Bultos retirados en el domicilio",
        "RetiroNoRealizado" => "No se pudo concretar el retiro",
        "RetiroReprogramado" => "Retiro reprogramado para una nueva visita",
        "RetiroLiberadoDeRuta" => "Retiro pendiente de una nueva programación",
        "RecepcionEnDeposito" => "Bultos recibidos en el depósito",
        "EntregaAlComercio" => "Bultos entregados al comercio",
        "RetiroCancelado" => "Retiro cancelado",
        _ => DescribirEstadoRetiro(evento.EstadoNuevo)
    };

    /// <summary>Solo los cambios de estado, entregas parciales y geocercas forman parte de la línea de tiempo pública.</summary>
    private static bool EsHitoPublico(EventoEnvio evento) =>
        evento.TipoEvento != Envio.TipoEventoIntermedioReprogramacion
        && (evento.EstadoAnterior is null
        || evento.EstadoAnterior != evento.EstadoNuevo
        || evento.TipoEvento is "LlegadaGeocerca" or "SalidaGeocerca" or "EntregaParcial");

    private async Task<string?> ObtenerVentanaAsync(Envio envio, CancellationToken cancellationToken)
    {
        if (!envio.FranjaHorariaComprometidaId.HasValue)
            return null;

        var franjas = await _franjaHorariaRepository.GetByIdsAsync(new[] { envio.FranjaHorariaComprometidaId.Value }, cancellationToken);
        var franja = franjas.FirstOrDefault();
        return franja is null ? null : $"{franja.HoraInicio:HH\\:mm}-{franja.HoraFin:HH\\:mm}";
    }

    /// <summary>Texto apto para el destinatario: nunca se publica el payload interno ni identificadores internos de bultos.</summary>
    private static string DescribirHito(EventoEnvio evento, int bultosEntregados, int totalBultos) => evento.TipoEvento switch
    {
        "AltaEnvio" => "Envío registrado y admitido",
        "RecepcionEnDeposito" => "Envío recibido en el depósito",
        "AsignacionRuta" => "Envío asignado a una ruta de distribución",
        "BultoFaltanteEnCarga" => "Envío devuelto al depósito para una nueva planificación",
        "DespachoRuta" => "El envío salió a reparto",
        "EntregaExitosa" => "Envío entregado",
        "EntregaParcial" => $"Entrega parcial registrada ({bultosEntregados} de {totalBultos} bultos entregados)",
        "IntentoEntregaFallido" => "No se pudo concretar la entrega",
        "ReprogramadoPorDestinatario" => "Entrega reprogramada a pedido del destinatario",
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
            EstadoEnvio.EntregadoParcialmente => $"Envío entregado parcialmente ({bultosEntregados} de {totalBultos} bultos entregados)",
            EstadoEnvio.Devuelto => "Envío devuelto al comercio",
            EstadoEnvio.Extraviado => "El envío no pudo ser ubicado y se está gestionando",
            _ => $"Actualización del estado del envío: {evento.EstadoNuevo}"
        }
    };
}
