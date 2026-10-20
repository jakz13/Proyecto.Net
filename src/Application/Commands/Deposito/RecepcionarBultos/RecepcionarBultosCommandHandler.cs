using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;

public class RecepcionarBultosCommandHandler : IRequestHandler<RecepcionarBultosCommand, RecepcionResultDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;

    public RecepcionarBultosCommandHandler(
        IEnvioRepository envioRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
    }

    public async Task<RecepcionResultDto> Handle(RecepcionarBultosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario de depósito.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Anti-IDOR: el envío debe pertenecer al operador del usuario autenticado
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        // Idempotencia: reintentar la recepción de un envío ya recibido no duplica discrepancias ni eventos
        if (envio.EstadoActual != EstadoEnvio.Admitido)
        {
            if (envio.EstadoActual != EstadoEnvio.EnDeposito)
                throw new DomainValidationException($"El envío {envio.CodigoSeguimientoPublico} no se puede recepcionar porque su estado es '{envio.EstadoActual}'.");

            return new RecepcionResultDto(
                envio.Id,
                envio.CodigoSeguimientoPublico,
                envio.EstadoActual.ToString(),
                Recepcionado: true,
                Idempotente: true,
                TieneDiscrepancias: envio.Discrepancias.Count > 0,
                Correctos: envio.Bultos.Where(b => b.EstadoBulto == "Recibido").Select(b => b.Identificacion).ToList(),
                Faltantes: envio.Bultos.Where(b => b.EstadoBulto == "FaltanteEnRecepcion").Select(b => b.Identificacion).ToList(),
                Sobrantes: Array.Empty<string>(),
                Danados: envio.Bultos.Where(b => b.EstadoBulto == "Danado").Select(b => b.Identificacion).ToList(),
                Mensaje: "El envío ya se encontraba recepcionado en depósito (operación idempotente).");
        }

        var resultado = envio.RecepcionarEnDeposito(
            request.CodigosBultosEscaneados,
            request.CodigosBultosDanados,
            request.Observaciones,
            usuarioId);

        if (resultado.Recepcionado)
        {
            await _envioRepository.UpdateAsync(envio, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var mensaje = resultado switch
        {
            { Recepcionado: true, TieneDiscrepancias: false } => "Recepción registrada sin discrepancias.",
            { Recepcionado: true } => "Recepción registrada con discrepancias documentadas.",
            _ => "Se detectaron discrepancias: documente las observaciones para completar la recepción."
        };

        return new RecepcionResultDto(
            envio.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            resultado.Recepcionado,
            Idempotente: false,
            resultado.TieneDiscrepancias,
            resultado.Correctos,
            resultado.Faltantes,
            resultado.Sobrantes,
            resultado.Danados,
            mensaje);
    }
}
