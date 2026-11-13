using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Retiros;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Retiros.GestionRetiros;

/// <summary>Base de los casos de uso que operan sobre un retiro existente: carga con aislamiento por operador y por comercio.</summary>
public abstract class RetiroHandlerBase
{
    protected readonly IRetiroRepository Retiros;
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ICurrentTenant Tenant;

    protected RetiroHandlerBase(IRetiroRepository retiros, IUnitOfWork unitOfWork, ICurrentTenant tenant)
    {
        Retiros = retiros;
        UnitOfWork = unitOfWork;
        Tenant = tenant;
    }

    /// <summary>
    /// El retiro de otro operador no existe para quien consulta (filtro global). Quien opera con la identidad de un comercio solo ve los suyos:
    /// el de otro comercio se informa como inexistente para no revelar que existe. Qué perfiles pueden invocar cada operación lo decide
    /// la política de autorización del endpoint.
    /// </summary>
    protected async Task<Retiro> CargarAsync(Guid retiroId, CancellationToken cancellationToken)
    {
        if (retiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro es inválido.");

        var retiro = await Retiros.GetByIdAsync(retiroId, cancellationToken)
            ?? throw new NotFoundException($"Retiro '{retiroId}' no encontrado.");

        if (retiro.OperadorId != Tenant.OperadorId)
            throw new NotFoundException($"Retiro '{retiroId}' no encontrado.");

        if (Tenant.ComercioOperadorId.HasValue && retiro.ComercioOperadorId != Tenant.ComercioOperadorId.Value)
            throw new NotFoundException($"Retiro '{retiroId}' no encontrado.");

        return retiro;
    }

    protected async Task<RetiroDetalleDto> GuardarAsync(Retiro retiro, CancellationToken cancellationToken)
    {
        await Retiros.UpdateAsync(retiro, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return RetiroMapper.Detallar(retiro);
    }
}

public class CancelarRetiroCommandHandler : RetiroHandlerBase, IRequestHandler<CancelarRetiroCommand, RetiroDetalleDto>
{
    private readonly IComercioOperadorRepository? _comercioOperadorRepository;

    public CancelarRetiroCommandHandler(
        IRetiroRepository retiros,
        IUnitOfWork unitOfWork,
        ICurrentTenant tenant,
        IComercioOperadorRepository? comercioOperadorRepository = null)
        : base(retiros, unitOfWork, tenant)
    {
        _comercioOperadorRepository = comercioOperadorRepository;
    }

    public async Task<RetiroDetalleDto> Handle(CancelarRetiroCommand request, CancellationToken cancellationToken)
    {
        await ComercioOperadorActivoGuard.AsegurarActivoAsync(_comercioOperadorRepository, Tenant.ComercioOperadorId, cancellationToken);

        var retiro = await CargarAsync(request.RetiroId, cancellationToken);

        if (retiro.EstadoActual == EstadoRetiro.AsignadoARuta)
            throw new DomainValidationException(
                $"El retiro {retiro.CodigoSeguimientoPublico} ya está en una hoja de ruta. Solicite al operador que lo saque de la ruta antes de cancelarlo.");

        var origen = Tenant.ComercioOperadorId.HasValue ? Retiro.OrigenPortal : Retiro.OrigenBackoffice;
        retiro.Cancelar(request.Motivo, Tenant.UsuarioId, origen);
        return await GuardarAsync(retiro, cancellationToken);
    }
}

public class ReprogramarRetiroCommandHandler : RetiroHandlerBase, IRequestHandler<ReprogramarRetiroCommand, RetiroDetalleDto>
{
    public ReprogramarRetiroCommandHandler(IRetiroRepository retiros, IUnitOfWork unitOfWork, ICurrentTenant tenant)
        : base(retiros, unitOfWork, tenant) { }

    public async Task<RetiroDetalleDto> Handle(ReprogramarRetiroCommand request, CancellationToken cancellationToken)
    {
        var retiro = await CargarAsync(request.RetiroId, cancellationToken);
        retiro.ReprogramarVisita(Tenant.UsuarioId);
        return await GuardarAsync(retiro, cancellationToken);
    }
}

public class RecibirRetiroEnDepositoCommandHandler : RetiroHandlerBase, IRequestHandler<RecibirRetiroEnDepositoCommand, RetiroDetalleDto>
{
    public RecibirRetiroEnDepositoCommandHandler(IRetiroRepository retiros, IUnitOfWork unitOfWork, ICurrentTenant tenant)
        : base(retiros, unitOfWork, tenant) { }

    public async Task<RetiroDetalleDto> Handle(RecibirRetiroEnDepositoCommand request, CancellationToken cancellationToken)
    {
        var retiro = await CargarAsync(request.RetiroId, cancellationToken);

        // Idempotente: recibir dos veces el mismo retiro no duplica el evento
        if (retiro.EstadoActual == EstadoRetiro.EnDeposito)
            return RetiroMapper.Detallar(retiro);

        retiro.RecibirEnDeposito(Tenant.UsuarioId);
        return await GuardarAsync(retiro, cancellationToken);
    }
}

public class EntregarRetiroAlComercioCommandHandler : RetiroHandlerBase, IRequestHandler<EntregarRetiroAlComercioCommand, RetiroDetalleDto>
{
    public EntregarRetiroAlComercioCommandHandler(IRetiroRepository retiros, IUnitOfWork unitOfWork, ICurrentTenant tenant)
        : base(retiros, unitOfWork, tenant) { }

    public async Task<RetiroDetalleDto> Handle(EntregarRetiroAlComercioCommand request, CancellationToken cancellationToken)
    {
        var retiro = await CargarAsync(request.RetiroId, cancellationToken);

        if (retiro.EstadoActual == EstadoRetiro.EntregadoAlComercio)
            return RetiroMapper.Detallar(retiro);

        retiro.EntregarAlComercio(request.Receptor, Tenant.UsuarioId);
        return await GuardarAsync(retiro, cancellationToken);
    }
}

public class SacarRetiroDeRutaCommandHandler : RetiroHandlerBase, IRequestHandler<SacarRetiroDeRutaCommand, RetiroDetalleDto>
{
    private readonly IRutaRepository _rutas;

    public SacarRetiroDeRutaCommandHandler(IRetiroRepository retiros, IRutaRepository rutas, IUnitOfWork unitOfWork, ICurrentTenant tenant)
        : base(retiros, unitOfWork, tenant)
    {
        _rutas = rutas;
    }

    public async Task<RetiroDetalleDto> Handle(SacarRetiroDeRutaCommand request, CancellationToken cancellationToken)
    {
        var retiro = await CargarAsync(request.RetiroId, cancellationToken);

        if (retiro.EstadoActual != EstadoRetiro.AsignadoARuta || !retiro.HojaDeRutaId.HasValue)
            throw new DomainValidationException($"El retiro {retiro.CodigoSeguimientoPublico} no está asignado a una hoja de ruta.");

        var ruta = await _rutas.GetByIdWithParadasYAsignacionesAsync(retiro.HojaDeRutaId.Value, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{retiro.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != Tenant.OperadorId)
            throw new NotFoundException($"Hoja de ruta '{retiro.HojaDeRutaId}' no encontrada.");

        ruta.RetirarRetiro(retiro.Id);
        retiro.LiberarDeRuta("Retiro sacado de la hoja de ruta por el despachador.", Tenant.UsuarioId);

        await _rutas.UpdateAsync(ruta, cancellationToken);
        return await GuardarAsync(retiro, cancellationToken);
    }
}
