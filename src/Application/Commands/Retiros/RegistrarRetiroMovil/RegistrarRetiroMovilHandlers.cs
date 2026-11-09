using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Retiros.RegistrarRetiroMovil;

/// <summary>Contexto común de los casos de uso móviles de retiro: ruta propia del repartidor, parada y retiro coherentes entre sí.</summary>
public abstract class RetiroMovilHandlerBase
{
    protected readonly IRutaRepository Rutas;
    protected readonly IRetiroRepository Retiros;
    protected readonly IEnvioRepository Envios;
    protected readonly IRepartidorRepository Repartidores;
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ICurrentTenant Tenant;
    protected readonly IClock Clock;
    private readonly ITableroRealtimeNotifier? _tablero;

    protected RetiroMovilHandlerBase(
        IRutaRepository rutas,
        IRetiroRepository retiros,
        IEnvioRepository envios,
        IRepartidorRepository repartidores,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        IClock clock,
        ITableroRealtimeNotifier? tablero)
    {
        Rutas = rutas;
        Retiros = retiros;
        Envios = envios;
        Repartidores = repartidores;
        UnitOfWork = unitOfWork;
        Tenant = tenant;
        Clock = clock;
        _tablero = tablero;
    }

    protected async Task<(HojaDeRuta Ruta, Parada Parada, Retiro Retiro)> CargarAsync(
        Guid hojaDeRutaId, Guid paradaId, Guid retiroId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (paradaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        if (retiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro es inválido.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        var ruta = await Rutas.GetByIdWithParadasYAsignacionesAsync(hojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{hojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != Tenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        // Un repartidor solo opera sobre sus propias rutas
        var usuarioId = Tenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al repartidor.");
        var repartidor = await Repartidores.GetByUsuarioIdAsync(usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario autenticado no es un repartidor.");
        if (ruta.RepartidorId != repartidor.Id)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta pertenece a otro repartidor.");

        var parada = ruta.Paradas.FirstOrDefault(p => p.Id == paradaId)
            ?? throw new NotFoundException($"La parada '{paradaId}' no pertenece a la hoja de ruta '{hojaDeRutaId}'.");

        if (!parada.EsRetiro || parada.RetiroId != retiroId)
            throw new DomainValidationException($"La parada '{paradaId}' no corresponde al retiro '{retiroId}'.");

        var retiro = await Retiros.GetByIdAsync(retiroId, cancellationToken)
            ?? throw new NotFoundException($"Retiro '{retiroId}' no encontrado.");

        if (retiro.OperadorId != Tenant.OperadorId)
            throw new NotFoundException($"Retiro '{retiroId}' no encontrado.");

        return (ruta, parada, retiro);
    }

    /// <summary>Sin conflicto, la ruta debe estar en la calle. Con conflicto (el servidor la cambió mientras no había conexión) se acepta.</summary>
    protected static void ValidarRutaEnLaCalle(HojaDeRuta ruta, bool huboConflicto)
    {
        if (ruta.Estado is not ("Despachada" or "EnTransito") && !huboConflicto)
        {
            throw new DomainValidationException(
                $"No se puede registrar el retiro porque la hoja de ruta no se encuentra en tránsito (estado actual: '{ruta.Estado}').");
        }
    }

    /// <summary>
    /// El estado del servidor no es el esperado: el despachador sacó el retiro de la ruta o el comercio lo canceló mientras el dispositivo
    /// estaba sin conexión. Política explícita: prevalece lo ocurrido físicamente en el domicilio y el conflicto queda asentado.
    /// </summary>
    protected static bool HayConflicto(Retiro retiro, Parada parada) =>
        retiro.EstadoActual != EstadoRetiro.AsignadoARuta || parada.Estado == "Cancelada";

    protected async Task AsentarConflictoAsync(Retiro retiro, EstadoRetiro estadoDispositivo, CancellationToken cancellationToken)
    {
        await Envios.RegistrarConflictoAsync(
            new ConflictoSincronizacion(
                Guid.NewGuid(),
                nameof(Retiro),
                retiro.Id,
                estadoServidor: retiro.EstadoActual.ToString(),
                estadoDispositivo: estadoDispositivo.ToString(),
                politicaAplicada: "PrevalecenLosHechosFisicosEnElDomicilio",
                resolucion: $"El repartidor registró sin conexión el resultado del retiro ({estadoDispositivo}) mientras el servidor lo tenía en estado " +
                            $"'{retiro.EstadoActual}'. Prevalece lo ocurrido en el domicilio, con su evidencia."),
            cancellationToken);
    }

    protected async Task GuardarYNotificarAsync(HojaDeRuta ruta, Parada parada, Retiro retiro, CancellationToken cancellationToken)
    {
        await Rutas.UpdateAsync(ruta, cancellationToken);
        await Retiros.UpdateAsync(retiro, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        if (_tablero is not null)
        {
            try
            {
                await _tablero.NotificarHitoParadaAsync(ruta.OperadorId, ruta.Id, parada.Id, parada.Estado, cancellationToken);
                if (ruta.Estado == "Finalizada")
                    await _tablero.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida el retiro ya persistido
            }
        }
    }
}

public class RegistrarRetiroRealizadoCommandHandler : RetiroMovilHandlerBase, IRequestHandler<RegistrarRetiroRealizadoCommand, RetiroMovilResultDto>
{
    public RegistrarRetiroRealizadoCommandHandler(
        IRutaRepository rutas,
        IRetiroRepository retiros,
        IEnvioRepository envios,
        IRepartidorRepository repartidores,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        IClock clock,
        ITableroRealtimeNotifier? tablero = null)
        : base(rutas, retiros, envios, repartidores, unitOfWork, tenant, clock, tablero) { }

    public async Task<RetiroMovilResultDto> Handle(RegistrarRetiroRealizadoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.NombreEntrega))
            throw new DomainValidationException("El nombre de quien entrega los bultos es requerido.");

        var (ruta, parada, retiro) = await CargarAsync(request.HojaDeRutaId, request.ParadaId, request.RetiroId, request.IdempotencyKey, cancellationToken);

        // Idempotencia: reenviar un retiro ya registrado desde esta misma ruta no repite el cambio ni sus avisos
        if (retiro.HojaDeRutaId == ruta.Id
            && retiro.EstadoActual is EstadoRetiro.Retirado or EstadoRetiro.EnDeposito or EstadoRetiro.EntregadoAlComercio)
        {
            return Resultado(retiro, ruta, parada, idempotente: true, conflicto: false,
                "El retiro ya se encontraba registrado previamente (operación idempotente).");
        }

        var huboConflicto = HayConflicto(retiro, parada);
        ValidarRutaEnLaCalle(ruta, huboConflicto);

        var fechaDispositivo = request.FechaHoraDispositivoUtc ?? Clock.UtcNow;

        if (huboConflicto)
            await AsentarConflictoAsync(retiro, EstadoRetiro.Retirado, cancellationToken);

        retiro.RegistrarRetiro(
            request.CodigosBultosEscaneados,
            request.FotoEstadoBase64,
            request.FirmaBase64,
            request.NombreEntrega,
            request.DocumentoEntrega,
            request.Latitud,
            request.Longitud,
            fechaDispositivo,
            Tenant.UsuarioId,
            resolverConflicto: huboConflicto,
            hojaDeRutaId: ruta.Id);

        ruta.RegistrarRetiroRealizadoEnParada(parada.Id, resolverConflicto: huboConflicto);
        await GuardarYNotificarAsync(ruta, parada, retiro, cancellationToken);

        return Resultado(retiro, ruta, parada, idempotente: false, conflicto: huboConflicto,
            huboConflicto
                ? "Retiro registrado resolviendo un conflicto: prevaleció lo ocurrido en el domicilio sobre el cambio previo en el servidor."
                : "Retiro registrado correctamente.");
    }

    private static RetiroMovilResultDto Resultado(Retiro retiro, HojaDeRuta ruta, Parada parada, bool idempotente, bool conflicto, string mensaje) =>
        new(retiro.Id, ruta.Id, parada.Id, retiro.CodigoSeguimientoPublico, retiro.EstadoActual.ToString(), parada.Estado, idempotente, conflicto, mensaje);
}

public class RegistrarRetiroNoRealizadoCommandHandler : RetiroMovilHandlerBase, IRequestHandler<RegistrarRetiroNoRealizadoCommand, RetiroMovilResultDto>
{
    public RegistrarRetiroNoRealizadoCommandHandler(
        IRutaRepository rutas,
        IRetiroRepository retiros,
        IEnvioRepository envios,
        IRepartidorRepository repartidores,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        IClock clock,
        ITableroRealtimeNotifier? tablero = null)
        : base(rutas, retiros, envios, repartidores, unitOfWork, tenant, clock, tablero) { }

    public async Task<RetiroMovilResultDto> Handle(RegistrarRetiroNoRealizadoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (ruta, parada, retiro) = await CargarAsync(request.HojaDeRutaId, request.ParadaId, request.RetiroId, request.IdempotencyKey, cancellationToken);

        if (retiro.HojaDeRutaId == ruta.Id && retiro.EstadoActual == EstadoRetiro.NoRetirado)
        {
            return new RetiroMovilResultDto(retiro.Id, ruta.Id, parada.Id, retiro.CodigoSeguimientoPublico, retiro.EstadoActual.ToString(), parada.Estado,
                true, false, "El resultado del retiro ya se encontraba registrado previamente (operación idempotente).");
        }

        // El motivo debe existir en el catálogo configurado por el operador y estar activo
        var motivo = await Envios.GetMotivoNoEntregaAsync(ruta.OperadorId, request.MotivoNoEntregaId, request.MotivoCodigo, cancellationToken)
            ?? throw new DomainValidationException("El motivo indicado no existe en el catálogo del operador.");
        if (!motivo.Activo)
            throw new DomainValidationException($"El motivo '{motivo.Codigo}' se encuentra inactivo.");

        var huboConflicto = HayConflicto(retiro, parada);
        ValidarRutaEnLaCalle(ruta, huboConflicto);

        if (huboConflicto)
            await AsentarConflictoAsync(retiro, EstadoRetiro.NoRetirado, cancellationToken);

        retiro.RegistrarNoRetiro(
            motivo.Codigo,
            request.Observaciones,
            request.Latitud,
            request.Longitud,
            request.FechaHoraDispositivoUtc ?? Clock.UtcNow,
            Tenant.UsuarioId,
            resolverConflicto: huboConflicto,
            hojaDeRutaId: ruta.Id);

        ruta.RegistrarRetiroNoRealizadoEnParada(parada.Id, resolverConflicto: huboConflicto);
        await GuardarYNotificarAsync(ruta, parada, retiro, cancellationToken);

        return new RetiroMovilResultDto(retiro.Id, ruta.Id, parada.Id, retiro.CodigoSeguimientoPublico, retiro.EstadoActual.ToString(), parada.Estado,
            false, huboConflicto,
            huboConflicto
                ? "Retiro no realizado registrado resolviendo un conflicto con el estado del servidor."
                : "Retiro no realizado registrado. El despachador decidirá una nueva visita.");
    }
}
