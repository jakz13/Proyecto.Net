using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Avisos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de avisos automáticos y webhooks para comercios (UC-29, UC-30).
/// </summary>
public class MockAvisosComercioApi : IAvisosComercioApi
{
    private static readonly IReadOnlyList<EventoDisponibleDto> CatalogoEventosDisponibles = new List<EventoDisponibleDto>
    {
        new("EnvioAdmitido", "Admisión de Envío", "Emitido cuando el envío queda admitido en el sistema y recibe su código de seguimiento.", "Ingreso"),
        new("EnDeposito", "Recepción en Depósito", "Emitido cuando los bultos ingresan al centro logístico y son clasificados.", "Depósito"),
        new("EnTransito", "Despacho en Ruta", "Emitido cuando el repartidor carga el vehículo e inicia la navegación hacia el destino.", "Distribución"),
        new("Entregado", "Entrega Exitosa", "Emitido cuando el destinatario recibe el paquete y se registra la prueba de entrega.", "Finalización"),
        new("NoEntregado", "Intento Fallido", "Emitido cuando no fue posible concretar la entrega en el domicilio.", "Novedad"),
        new("Reprogramado", "Envío Reprogramado", "Emitido ante un cambio de fecha o turno acordado con el destinatario.", "Gestión"),
        new("Extraviado", "Incidencia / Extravío", "Emitido si se reporta un faltante o siniestro operativo con el paquete.", "Alerta")
    };

    private static string _urlWebhookActual = "https://api.megastore.uy/webhooks/ultimamilla";
    private static List<string> _eventosActuales = new() { "EnvioAdmitido", "EnTransito", "Entregado", "NoEntregado" };
    private static string _claveFirmaActual = "sec_live_9f83a21b47c04e6d8a9e2f1b";
    private static bool _activaActual = true;

    private static readonly List<AvisoHistorialDetalleDto> HistorialDetalleEnMemoria = new()
    {
        new(
            Id: Guid.Parse("11111111-aaaa-bbbb-cccc-000000000001"),
            SuscripcionAvisoId: Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            EnvioId: Guid.Parse("99999999-0000-0000-0000-000000000001"),
            CodigoSeguimiento: "TRK-2026-0001",
            TipoEvento: "Entregado",
            UrlWebhook: "https://api.megastore.uy/webhooks/ultimamilla",
            Payload: "{\n  \"evento\": \"Entregado\",\n  \"codigoSeguimiento\": \"TRK-2026-0001\",\n  \"destinatario\": \"Mariana Pérez\",\n  \"direccion\": \"Av. 18 de Julio 1520\",\n  \"fecha\": \"2026-10-29T18:30:00Z\"\n}",
            FirmaEnviada: "sha256=4f6a9e10283cbe5f781a902834b12c8e",
            Estado: "Entregado",
            Intentos: 1,
            UltimaRespuestaHttp: "HTTP 200 OK - Webhook recibido y procesado por MegaStore API",
            CodigoRespuestaHttp: 200,
            Timestamp: DateTime.UtcNow.AddMinutes(-25),
            ProximoReintento: null),

        new(
            Id: Guid.Parse("22222222-aaaa-bbbb-cccc-000000000002"),
            SuscripcionAvisoId: Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            EnvioId: Guid.Parse("99999999-0000-0000-0000-000000000002"),
            CodigoSeguimiento: "TRK-2026-0002",
            TipoEvento: "EnTransito",
            UrlWebhook: "https://api.megastore.uy/webhooks/ultimamilla",
            Payload: "{\n  \"evento\": \"EnTransito\",\n  \"codigoSeguimiento\": \"TRK-2026-0002\",\n  \"destinatario\": \"Carlos Rodríguez\",\n  \"repartidor\": \"Juan Pérez\",\n  \"vehiculo\": \"Furgón Renault Master\"\n}",
            FirmaEnviada: "sha256=9b201cd8274fbe6182937401928374fa",
            Estado: "Entregado",
            Intentos: 1,
            UltimaRespuestaHttp: "HTTP 200 OK",
            CodigoRespuestaHttp: 200,
            Timestamp: DateTime.UtcNow.AddHours(-1),
            ProximoReintento: null),

        new(
            Id: Guid.Parse("33333333-aaaa-bbbb-cccc-000000000003"),
            SuscripcionAvisoId: Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            EnvioId: Guid.Parse("99999999-0000-0000-0000-000000000006"),
            CodigoSeguimiento: "TRK-2026-0006",
            TipoEvento: "NoEntregado",
            UrlWebhook: "https://api.megastore.uy/webhooks/ultimamilla",
            Payload: "{\n  \"evento\": \"NoEntregado\",\n  \"codigoSeguimiento\": \"TRK-2026-0006\",\n  \"motivo\": \"Destinatario ausente\",\n  \"primerIntento\": true\n}",
            FirmaEnviada: "sha256=a1b2c3d4e5f60718293a4b5c6d7e8f90",
            Estado: "Fallido",
            Intentos: 3,
            UltimaRespuestaHttp: "HTTP 500 Internal Server Error - Timeout conectando con base de datos del comercio",
            CodigoRespuestaHttp: 500,
            Timestamp: DateTime.UtcNow.AddHours(-2),
            ProximoReintento: DateTime.UtcNow.AddMinutes(10)),

        new(
            Id: Guid.Parse("44444444-aaaa-bbbb-cccc-000000000004"),
            SuscripcionAvisoId: Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            EnvioId: Guid.Parse("99999999-0000-0000-0000-000000000005"),
            CodigoSeguimiento: "TRK-2026-0005",
            TipoEvento: "EnvioAdmitido",
            UrlWebhook: "https://api.megastore.uy/webhooks/ultimamilla",
            Payload: "{\n  \"evento\": \"EnvioAdmitido\",\n  \"codigoSeguimiento\": \"TRK-2026-0005\",\n  \"destinatario\": \"Florencia Silva\",\n  \"bultos\": 1\n}",
            FirmaEnviada: "sha256=11223344556677889900aabbccddeeff",
            Estado: "Agotado",
            Intentos: 5,
            UltimaRespuestaHttp: "HTTP 504 Gateway Timeout - Servidor del comercio no respondió tras 5 reintentos automáticos",
            CodigoRespuestaHttp: 504,
            Timestamp: DateTime.UtcNow.AddHours(-6),
            ProximoReintento: null)
    };

    public Task<ConfiguracionAvisosDto> ObtenerConfiguracionAsync(CancellationToken cancelacion = default)
    {
        var historialResumido = HistorialDetalleEnMemoria.Select(h => new AvisoHistorialDto(
            h.Id,
            h.TipoEvento,
            h.CodigoSeguimiento,
            h.Estado,
            h.CodigoRespuestaHttp,
            h.Timestamp,
            h.Intentos)).ToList();

        var config = new ConfiguracionAvisosDto
        {
            ComercioOperadorId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            UrlWebhook = _urlWebhookActual,
            EventosSuscritos = _eventosActuales.AsReadOnly(),
            ClaveFirma = _claveFirmaActual,
            Activa = _activaActual,
            EstaConfigurado = !string.IsNullOrWhiteSpace(_urlWebhookActual),
            CatalogoEventos = CatalogoEventosDisponibles,
            HistorialReciente = historialResumido.AsReadOnly()
        };

        return Task.FromResult(config);
    }

    public Task<ConfiguracionAvisosDto> GuardarConfiguracionAsync(GuardarConfiguracionAvisosDto dto, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.UrlWebhook))
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "La URL del webhook es obligatoria.");

        var url = dto.UrlWebhook.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "La URL ingresada no es una dirección HTTP o HTTPS válida.");

        if (dto.Eventos is null || dto.Eventos.Count == 0)
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "Debe seleccionar al menos un evento para recibir avisos automáticos.");

        _urlWebhookActual = url;
        _eventosActuales = dto.Eventos.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
        _claveFirmaActual = string.IsNullOrWhiteSpace(dto.ClaveFirma)
            ? $"sec_live_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}"
            : dto.ClaveFirma.Trim();
        _activaActual = dto.Activa;

        return ObtenerConfiguracionAsync(cancelacion);
    }

    public Task<ResultadoPruebaWebhookDto> EnviarPruebaWebhookAsync(string? urlOpcional = null, CancellationToken cancelacion = default)
    {
        var urlDestino = string.IsNullOrWhiteSpace(urlOpcional) ? _urlWebhookActual : urlOpcional.Trim();
        if (string.IsNullOrWhiteSpace(urlDestino))
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "Debe configurar o ingresar una URL de webhook para realizar la prueba.");

        if (!Uri.TryCreate(urlDestino, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "La URL ingresada no es una dirección HTTP o HTTPS válida.");

        var ahora = DateTime.UtcNow;
        var datosPrueba = new
        {
            evento = "ping_diagnostico",
            timestamp = ahora.ToString("o"),
            plataforma = "Última Milla - Notificaciones a Comercios (UC-29)",
            comercio = "MegaStore Uruguay",
            envio = new
            {
                codigoSeguimiento = "TRK-2026-0005",
                estado = "Admitido",
                destinatario = "Florencia Silva",
                direccion = "Sarandí 450, Ciudad Vieja, Montevideo",
                fecha = ahora.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        var json = JsonSerializer.Serialize(datosPrueba, new JsonSerializerOptions { WriteIndented = true });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_claveFirmaActual));
        var firma = $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()}";

        // Agrega el intento al historial detallado en memoria
        HistorialDetalleEnMemoria.Insert(0, new(
            Id: Guid.NewGuid(),
            SuscripcionAvisoId: Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            EnvioId: Guid.Parse("99999999-0000-0000-0000-000000000005"),
            CodigoSeguimiento: "TRK-2026-0005",
            TipoEvento: "Prueba Ping",
            UrlWebhook: urlDestino,
            Payload: json,
            FirmaEnviada: firma,
            Estado: "Entregado",
            Intentos: 1,
            UltimaRespuestaHttp: "HTTP 200 OK - Diagnóstico satisfactorio",
            CodigoRespuestaHttp: 200,
            Timestamp: ahora,
            ProximoReintento: null));

        var resultado = new ResultadoPruebaWebhookDto(
            Exitoso: true,
            CodigoHttp: 200,
            Mensaje: $"Notificación de prueba enviada exitosamente a '{urlDestino}'. El servidor receptor aceptó el payload y validó la firma HMAC.",
            PayloadEnviado: json,
            FirmaGenerada: firma,
            Timestamp: ahora);

        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<AvisoHistorialDetalleDto>> ConsultarHistorialAvisosAsync(
        string? estado = null,
        Guid? envioId = null,
        string? codigoSeguimiento = null,
        CancellationToken cancelacion = default)
    {
        IEnumerable<AvisoHistorialDetalleDto> consulta = HistorialDetalleEnMemoria;

        if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("todos", StringComparison.OrdinalIgnoreCase))
        {
            consulta = consulta.Where(h => h.Estado.Equals(estado.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (envioId.HasValue && envioId.Value != Guid.Empty)
        {
            consulta = consulta.Where(h => h.EnvioId == envioId.Value);
        }

        if (!string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            var texto = codigoSeguimiento.Trim();
            consulta = consulta.Where(h => h.CodigoSeguimiento.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                                           h.TipoEvento.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        IReadOnlyList<AvisoHistorialDetalleDto> resultado = consulta
            .OrderByDescending(h => h.Timestamp)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<ResultadoReintentoAvisoDto> ReintentarAvisoAsync(Guid avisoId, CancellationToken cancelacion = default)
    {
        if (avisoId == Guid.Empty)
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "El identificador del aviso es inválido.");

        var indice = HistorialDetalleEnMemoria.FindIndex(h => h.Id == avisoId);
        if (indice < 0)
            throw new ApiExcepcion(System.Net.HttpStatusCode.NotFound, $"No se encontró el aviso con ID '{avisoId}'.");

        var actual = HistorialDetalleEnMemoria[indice];
        if (actual.Estado == "Entregado")
            throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, "El aviso ya fue entregado satisfactoriamente y no requiere reenvío.");

        var ahora = DateTime.UtcNow;
        var actualizado = actual with
        {
            Estado = "Entregado",
            CodigoRespuestaHttp = 200,
            UltimaRespuestaHttp = "HTTP 200 OK - Reenvío manual entregado exitosamente al webhook del comercio",
            Intentos = actual.Intentos + 1,
            Timestamp = ahora,
            ProximoReintento = null
        };

        HistorialDetalleEnMemoria[indice] = actualizado;

        var respuesta = new ResultadoReintentoAvisoDto(
            AvisoId: avisoId,
            Exitoso: true,
            Estado: "Entregado",
            Intentos: actualizado.Intentos,
            Mensaje: $"Aviso para {actualizado.CodigoSeguimiento} reenviado con éxito hacia {actualizado.UrlWebhook}.",
            CodigoHttp: 200,
            Timestamp: ahora);

        return Task.FromResult(respuesta);
    }
}
