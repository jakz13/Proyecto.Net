using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Ports;

public interface IOutboxRepository
{
    /// <summary>Mensajes aún no publicados, del más antiguo al más nuevo.</summary>
    Task<List<OutboxMessage>> ListarPendientesAsync(int maximo, CancellationToken cancellationToken = default);
}

public interface IIdempotenciaConsumidorRepository
{
    Task<bool> ExisteAsync(string mensajeId, CancellationToken cancellationToken = default);
    Task AddAsync(ClaveIdempotenciaConsumidor clave, CancellationToken cancellationToken = default);
}

public interface IMensajeFallidoRepository
{
    Task<MensajeFallidoCola?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MensajeFallidoCola mensaje, CancellationToken cancellationToken = default);
}

public interface INotificacionRepository
{
    Task AddAsync(Notificacion notificacion, CancellationToken cancellationToken = default);
}

public interface IAvisoComercioRepository
{
    /// <summary>Suscripciones activas del comercio (el alcance del comercio es siempre explícito).</summary>
    Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default);
    Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default);
    Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default);

    /// <summary>Avisos pendientes o en reintento cuyo próximo intento ya venció.</summary>
    Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default);
    Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default);
    Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default);
    Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default);
    Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default);
    Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default);
}

public interface IIndicadorCumplimientoRepository
{
    Task<List<IndicadorCumplimiento>> ListarPorPeriodoAsync(Guid operadorId, string periodo, CancellationToken cancellationToken = default);
    Task AddAsync(IndicadorCumplimiento indicador, CancellationToken cancellationToken = default);
}

public interface ICierreDiarioRepository
{
    Task<CierreDiarioOperacion?> GetAsync(Guid operadorId, DateOnly fecha, CancellationToken cancellationToken = default);
    Task AddAsync(CierreDiarioOperacion cierre, CancellationToken cancellationToken = default);
}

/// <summary>Consultas de solo lectura que alimentan los procesos programados (UC-37 y UC-38). Todas acotadas a un operador.</summary>
public interface IProcesosProgramadosRepository
{
    Task<List<Guid>> ListarOperadoresActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Envíos del operador con un resultado de entrega (entregado o no entregado) registrado dentro del intervalo.</summary>
    Task<List<Envio>> ListarEnviosConResultadoAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default);

    Task<int> ContarEnviosAdmitidosAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default);

    /// <summary>Incidencias abiertas en el intervalo y las que siguen sin cerrarse al final del mismo.</summary>
    Task<(int Abiertas, int SinCerrar)> ContarIncidenciasAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default);
}
