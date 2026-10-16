using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para la gestión y reintento de mensajes fallidos en colas asíncronas (UC-42).
/// Simula la Dead Letter Queue (DLQ) interna de la plataforma para eventos, webhooks y sincronizaciones móviles.
/// </summary>
public class MockMensajeriaApi : IMensajeriaApi
{
    private readonly List<MensajeFallidoDto> _colaFallidos;
    private readonly object _bloqueo = new();

    public MockMensajeriaApi()
    {
        var mensaje1Id = Guid.Parse("44444444-4444-4444-4444-444444444401");
        var mensaje2Id = Guid.Parse("44444444-4444-4444-4444-444444444402");
        var mensaje3Id = Guid.Parse("44444444-4444-4444-4444-444444444403");
        var mensaje4Id = Guid.Parse("44444444-4444-4444-4444-444444444404");
        var mensaje5Id = Guid.Parse("44444444-4444-4444-4444-444444444405");
        var mensaje6Id = Guid.Parse("44444444-4444-4444-4444-444444444406");

        _colaFallidos = new List<MensajeFallidoDto>
        {
            new()
            {
                Id = mensaje1Id,
                TipoMensaje = "EventoPaqueteDespachado",
                ColaOrigen = "cola.eventos.envios",
                PayloadOriginal = "{\n  \"evento\": \"PaqueteDespachado\",\n  \"paqueteId\": \"ENV-2026-0891\",\n  \"operadorId\": \"OP-01\",\n  \"repartidorId\": \"33333333-3333-3333-3333-333333333301\",\n  \"hojaRutaId\": \"HR-2026-104\",\n  \"timestamp\": \"2026-10-15T18:22:10Z\"\n}",
                MotivoFallo = "RabbitMQ BrokerUnreachableException: Timeout al confirmar mensaje en el exchange 'amq.topic'.",
                DetalleExcepcion = "RabbitMQ.Client.Exceptions.BrokerUnreachableException: None of the specified endpoints were reachable\n   at RabbitMQ.Client.ConnectionFactory.CreateConnection()\n   at UltimaMilla.Infrastructure.Messaging.RabbitMqEventPublisher.PublishAsync(EventMessage eventMsg) in C:\\src\\Infrastructure\\Messaging\\RabbitMqEventPublisher.cs:line 84",
                Intentos = 3,
                PrimerFallo = DateTime.UtcNow.AddHours(-6),
                UltimoFallo = DateTime.UtcNow.AddMinutes(-45),
                Estado = "Fallido",
                OperadorId = "OP-01"
            },
            new()
            {
                Id = mensaje2Id,
                TipoMensaje = "NotificacionWebhookComercio",
                ColaOrigen = "cola.webhooks.comercios",
                PayloadOriginal = "{\n  \"comercioId\": \"COM-01\",\n  \"endpoint\": \"https://api.tiendacentral.uy/webhooks/envios\",\n  \"evento\": \"ENTREGA_CONCRETADA\",\n  \"codigoSeguimiento\": \"TRK-8841-UY\",\n  \"entregadoA\": \"Juan Pérez\",\n  \"timestamp\": \"2026-10-15T19:05:00Z\"\n}",
                MotivoFallo = "HttpRequestException: 504 Gateway Timeout devuelto por el servidor del comercio.",
                DetalleExcepcion = "System.Net.Http.HttpRequestException: Response status code does not indicate success: 504 (Gateway Timeout).\n   at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()\n   at UltimaMilla.Infrastructure.Webhooks.WebhookDispatcher.DispatchAsync(WebhookPayload payload) in C:\\src\\Infrastructure\\Webhooks\\WebhookDispatcher.cs:line 52",
                Intentos = 4,
                PrimerFallo = DateTime.UtcNow.AddHours(-4),
                UltimoFallo = DateTime.UtcNow.AddMinutes(-30),
                Estado = "Fallido",
                OperadorId = "OP-01"
            },
            new()
            {
                Id = mensaje3Id,
                TipoMensaje = "SincronizacionOfflineRepartidor",
                ColaOrigen = "cola.sincronizacion.movil",
                PayloadOriginal = "{\n  \"repartidorId\": \"33333333-3333-3333-3333-333333333302\",\n  \"operacionId\": \"SYNC-9921\",\n  \"accion\": \"REGISTRAR_INTENTO_FALLIDO\",\n  \"paqueteId\": \"ENV-2026-0412\",\n  \"motivoCodigo\": \"DIRECCION_INCORRECTA\",\n  \"horaLocalDispositivo\": \"2026-10-15T16:40:12Z\"\n}",
                MotivoFallo = "DbUpdateConcurrencyException: Conflicto de versión al asentar el intento fallido del repartidor.",
                DetalleExcepcion = "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: Database operation expected to affect 1 row(s) but actually affected 0 row(s).\n   at Microsoft.EntityFrameworkCore.Update.AffectedDataModificationCommandBatch.ConsumeResultSetWithRowsAffectedOnlyAsync()\n   at UltimaMilla.Infrastructure.Persistence.UnitOfWork.SaveChangesAsync() in C:\\src\\Infrastructure\\Persistence\\UnitOfWork.cs:line 45",
                Intentos = 3,
                PrimerFallo = DateTime.UtcNow.AddHours(-5),
                UltimoFallo = DateTime.UtcNow.AddMinutes(-20),
                Estado = "Fallido",
                OperadorId = "OP-01"
            },
            new()
            {
                Id = mensaje4Id,
                TipoMensaje = "GeneracionEtiquetaZPL",
                ColaOrigen = "cola.etiquetas.impresion",
                PayloadOriginal = "{\n  \"paqueteId\": \"ENV-2026-0902\",\n  \"formato\": \"ZPL_203DPI\",\n  \"impresoraId\": \"IMP-DEPOSITO-01\",\n  \"codigoBarras\": \"TRK-9021-UY\",\n  \"remitente\": \"Distribuidora Sur\",\n  \"destinatario\": \"Carlos Maggi, San José 1120\"\n}",
                MotivoFallo = "TimeoutException: El microservicio de renderizado ZPL no respondió en el plazo de 10 segundos.",
                DetalleExcepcion = "System.TimeoutException: The operation has timed out.\n   at UltimaMilla.Infrastructure.Etiquetas.ZplPrinterClient.GenerateLabelAsync(LabelData data) in C:\\src\\Infrastructure\\Etiquetas\\ZplPrinterClient.cs:line 63",
                Intentos = 5,
                PrimerFallo = DateTime.UtcNow.AddHours(-3),
                UltimoFallo = DateTime.UtcNow.AddMinutes(-15),
                Estado = "Fallido",
                OperadorId = "OP-01"
            },
            new()
            {
                Id = mensaje5Id,
                TipoMensaje = "ActualizacionRutaRepartidor",
                ColaOrigen = "cola.rutas.optimizacion",
                PayloadOriginal = "{\n  \"hojaRutaId\": \"HR-2026-104\",\n  \"paradasPendientes\": 8,\n  \"posicionGpsActual\": {\"lat\": -34.9011, \"lon\": -56.1645},\n  \"timestamp\": \"2026-10-15T19:30:00Z\"\n}",
                MotivoFallo = "SocketException: Conexión rechazada por el motor OSRM al calcular matriz de distancias.",
                DetalleExcepcion = "System.Net.Sockets.SocketException (10061): No connection could be made because the target machine actively refused it 127.0.0.1:5000\n   at System.Net.Sockets.Socket.AwaitableSocketAsyncEventArgs.ThrowException(SocketError error)\n   at UltimaMilla.Infrastructure.Routing.OsrmRoutingService.CalculateMatrixAsync(Coordinates[] points)",
                Intentos = 3,
                PrimerFallo = DateTime.UtcNow.AddHours(-2),
                UltimoFallo = DateTime.UtcNow.AddMinutes(-10),
                Estado = "Fallido",
                OperadorId = "OP-01"
            },
            new()
            {
                Id = mensaje6Id,
                TipoMensaje = "FacturacionEnvioComercio",
                ColaOrigen = "cola.facturacion",
                PayloadOriginal = "{\n  \"comercioId\": \"COM-02\",\n  \"periodo\": \"2026-10\",\n  \"totalEnvios\": 142,\n  \"montoTotal\": 35500.00,\n  \"moneda\": \"UYU\"\n}",
                MotivoFallo = "InvalidOperationException: Certificado de firma digital de facturación revocado o vencido.",
                DetalleExcepcion = "System.InvalidOperationException: Digital signature validation failed for issuer 'DGI Uruguay'.\n   at UltimaMilla.Infrastructure.Billing.InvoiceSigner.SignDocumentAsync(InvoiceModel invoice) in C:\\src\\Infrastructure\\Billing\\InvoiceSigner.cs:line 38",
                Intentos = 3,
                PrimerFallo = DateTime.UtcNow.AddHours(-8),
                UltimoFallo = DateTime.UtcNow.AddHours(-1),
                Estado = "Fallido",
                OperadorId = "OP-01"
            }
        };
    }

    public Task<IReadOnlyList<MensajeFallidoDto>> ListarMensajesFallidosAsync(
        string? tipo = null,
        string? estado = null,
        string? busqueda = null,
        CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var consulta = _colaFallidos.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(tipo) && tipo != "todos")
            {
                consulta = consulta.Where(m => m.TipoMensaje.Equals(tipo.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(estado) && estado != "todos")
            {
                consulta = consulta.Where(m => m.Estado.Equals(estado.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var termino = busqueda.Trim().ToLowerInvariant();
                consulta = consulta.Where(m =>
                    m.Id.ToString().ToLowerInvariant().Contains(termino) ||
                    m.TipoMensaje.ToLowerInvariant().Contains(termino) ||
                    m.ColaOrigen.ToLowerInvariant().Contains(termino) ||
                    m.MotivoFallo.ToLowerInvariant().Contains(termino) ||
                    m.PayloadOriginal.ToLowerInvariant().Contains(termino));
            }

            var resultado = consulta
                .OrderByDescending(m => m.UltimoFallo)
                .Select(m => m with { })
                .ToList();

            return Task.FromResult<IReadOnlyList<MensajeFallidoDto>>(resultado);
        }
    }

    public Task<MensajeFallidoDto?> ObtenerMensajePorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var mensaje = _colaFallidos.FirstOrDefault(m => m.Id == id);
            return Task.FromResult(mensaje != null ? mensaje with { } : null);
        }
    }

    public Task<ResultadoReintentoDto> ReintentarMensajesAsync(
        IEnumerable<Guid> ids,
        bool forzarFallo = false,
        CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var listaIds = ids.ToList();
            if (listaIds.Count == 0)
            {
                throw new ApiExcepcion("Debes seleccionar al menos un mensaje de la cola para reintentar.");
            }

            var exitosos = 0;
            var fallidos = 0;
            var detalles = new List<string>();

            foreach (var id in listaIds)
            {
                var indice = _colaFallidos.FindIndex(m => m.Id == id);
                if (indice < 0)
                {
                    detalles.Add($"[Ignorado] El mensaje con ID '{id}' no se encontró en la cola de fallidos.");
                    continue;
                }

                var actual = _colaFallidos[indice];

                // Flujo alternativo: Si se fuerza fallo o si el mensaje simula error persistente
                if (forzarFallo)
                {
                    var nuevaExcepcion = $"Reintento manual fallido en {DateTime.UtcNow:HH:mm:ss} UTC. El servicio de destino rechazó la solicitud.";
                    var actualizadoFallo = actual with
                    {
                        Intentos = actual.Intentos + 1,
                        UltimoFallo = DateTime.UtcNow,
                        MotivoFallo = nuevaExcepcion,
                        Estado = "Fallido"
                    };
                    _colaFallidos[indice] = actualizadoFallo;
                    fallidos++;
                    detalles.Add($"[Fallo] '{actual.TipoMensaje}' ({actual.Id.ToString()[..8]}): Reintento no completado. Permanece en la cola con {actualizadoFallo.Intentos} intentos.");
                }
                else
                {
                    // Flujo principal: Se reencola exitosamente y se marca como Resuelto
                    var actualizadoExito = actual with
                    {
                        UltimoFallo = DateTime.UtcNow,
                        Estado = "Resuelto"
                    };
                    _colaFallidos[indice] = actualizadoExito;
                    exitosos++;
                    detalles.Add($"[Éxito] '{actual.TipoMensaje}' ({actual.Id.ToString()[..8]}): Reencolado y procesado satisfactoriamente.");
                }
            }

            var balance = forzarFallo
                ? $"Se ejecutaron {fallidos} reintento(s) y el sistema registró nueva falla (permanecen en la cola)."
                : $"Se reencolaron exitosamente {exitosos} mensaje(s) para su procesamiento.";

            var resultado = new ResultadoReintentoDto
            {
                TotalSeleccionados = listaIds.Count,
                Exitosos = exitosos,
                Fallidos = fallidos,
                MensajeResumen = balance,
                Detalles = detalles
            };

            return Task.FromResult(resultado);
        }
    }

    public Task<bool> DescartarMensajeAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indice = _colaFallidos.FindIndex(m => m.Id == id);
            if (indice < 0)
            {
                return Task.FromResult(false);
            }

            _colaFallidos.RemoveAt(indice);
            return Task.FromResult(true);
        }
    }

    public Task<int> LimpiarMensajesResueltosAsync(CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var eliminados = _colaFallidos.RemoveAll(m => m.Estado == "Resuelto");
            return Task.FromResult(eliminados);
        }
    }
}
