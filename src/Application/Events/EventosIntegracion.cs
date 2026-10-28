using System.Text.Json;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Events;

/// <summary>
/// Contrato único de los eventos de negocio que la API deja en la outbox para el worker (avisos a comercios,
/// notificaciones al destinatario, tablero e indicadores). Todos comparten el mismo sobre: quien los consume
/// siempre encuentra versión, operador, instante y, según el agregado, los identificadores del envío o de la ruta.
/// El tipo del evento viaja en <see cref="OutboxMessage.TipoEvento"/> y el detalle propio de cada uno en <c>Detalle</c>.
/// </summary>
public static class EventosIntegracion
{
    /// <summary>Versión del sobre; se incrementa ante cualquier cambio incompatible de los campos comunes.</summary>
    public const int VersionContrato = 1;

    public static class Tipos
    {
        // Envío
        public const string EnvioAdmitido = "EnvioAdmitidoIntegrationEvent";
        public const string EnvioRecibidoEnDeposito = "EnvioRecibidoEnDepositoIntegrationEvent";
        public const string EnvioClasificado = "EnvioClasificadoIntegrationEvent";
        public const string EnvioAsignadoARuta = "EnvioAsignadoARutaIntegrationEvent";

        // Hoja de ruta
        public const string RutaArmada = "RutaArmadaIntegrationEvent";
        public const string CargaVehiculoRegistrada = "CargaVehiculoRegistradaIntegrationEvent";

        // Incidencias
        public const string IncidenciaAbierta = "IncidenciaAbiertaIntegrationEvent";
        public const string IncidenciaConSeguimiento = "IncidenciaConSeguimientoIntegrationEvent";
        public const string IncidenciaCerrada = "IncidenciaCerradaIntegrationEvent";
    }

    public static OutboxMessage DeEnvio(string tipoEvento, Envio envio, object? detalle = null)
    {
        ArgumentNullException.ThrowIfNull(envio);

        return Crear(nameof(Envio), envio.Id, tipoEvento, new
        {
            Version = VersionContrato,
            OperadorId = envio.OperadorId,
            ComercioId = envio.ComercioId,
            EnvioId = envio.Id,
            CodigoSeguimiento = envio.CodigoSeguimientoPublico,
            Estado = envio.EstadoActual.ToString(),
            OcurridoEn = DateTime.UtcNow,
            Detalle = detalle
        });
    }

    public static OutboxMessage DeRuta(string tipoEvento, HojaDeRuta ruta, object? detalle = null)
    {
        ArgumentNullException.ThrowIfNull(ruta);

        return Crear(nameof(HojaDeRuta), ruta.Id, tipoEvento, new
        {
            Version = VersionContrato,
            OperadorId = ruta.OperadorId,
            HojaDeRutaId = ruta.Id,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            Estado = ruta.Estado,
            OcurridoEn = DateTime.UtcNow,
            Detalle = detalle
        });
    }

    public static OutboxMessage DeIncidencia(string tipoEvento, Incidencia incidencia, object? detalle = null)
    {
        ArgumentNullException.ThrowIfNull(incidencia);

        return Crear(nameof(Incidencia), incidencia.Id, tipoEvento, new
        {
            Version = VersionContrato,
            OperadorId = incidencia.OperadorId,
            IncidenciaId = incidencia.Id,
            EnvioId = incidencia.EnvioId,
            HojaDeRutaId = incidencia.HojaDeRutaId,
            Tipo = incidencia.Tipo,
            Severidad = incidencia.Severidad,
            Estado = incidencia.Estado,
            OcurridoEn = DateTime.UtcNow,
            Detalle = detalle
        });
    }

    private static OutboxMessage Crear(string agregado, Guid agregadoId, string tipoEvento, object payload) =>
        new(Guid.NewGuid(), agregado, agregadoId, tipoEvento, JsonSerializer.Serialize(payload));
}
