using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Services;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Deposito.ClasificarBultos;

public class ClasificarBultosCommandHandler : IRequestHandler<ClasificarBultosCommand, ClasificacionResultDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IOutboxStorage _outboxStorage;

    private readonly IDireccionRepository? _direccionRepository;
    private readonly VerificadorCobertura? _verificadorCobertura;

    public ClasificarBultosCommandHandler(
        IEnvioRepository envioRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IOutboxStorage outboxStorage,
        IDireccionRepository? direccionRepository = null,
        VerificadorCobertura? verificadorCobertura = null)
    {
        _envioRepository = envioRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _outboxStorage = outboxStorage;
        _direccionRepository = direccionRepository;
        _verificadorCobertura = verificadorCobertura;
    }

    public async Task<ClasificacionResultDto> Handle(ClasificarBultosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        if (request.Bultos is null || request.Bultos.Count == 0)
            throw new DomainValidationException("Debe indicar al menos un bulto a clasificar.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario de depósito.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Anti-IDOR: el envío debe pertenecer al operador del usuario autenticado
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        var clasificaciones = request.Bultos.Select(Convertir).ToList();

        // La zona se detecta sola: al bulto sin zona se le pone la que cubre la dirección de entrega (si hay varias, cualquiera de ellas)
        if (clasificaciones.Any(c => c.ZonaCoberturaId is null))
        {
            var zonaDetectada = await DetectarZonaDeEntregaAsync(envio, cancellationToken);
            if (zonaDetectada is not null)
                clasificaciones = clasificaciones
                    .Select(c => c.ZonaCoberturaId is null ? c with { ZonaCoberturaId = zonaDetectada } : c)
                    .ToList();
        }

        envio.NormalizarBultosEnDeposito();
        envio.ClasificarBultos(clasificaciones, usuarioId);

        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _outboxStorage.SaveAsync(EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioClasificado, envio, new
        {
            ClasificacionCompleta = envio.EstaClasificado,
            BultosClasificados = clasificaciones.Count
        }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var recibidos = envio.Bultos.Where(b => b.EstadoBulto is "Recibido" or "Danado").ToList();
        var pendientes = recibidos.Where(b => !b.EstaClasificado).Select(b => b.Identificacion).ToList();

        return new ClasificacionResultDto(
            envio.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            recibidos.Count,
            recibidos.Count - pendientes.Count,
            pendientes,
            envio.EstaClasificado);
    }

    private async Task<Guid?> DetectarZonaDeEntregaAsync(Envio envio, CancellationToken cancellationToken)
    {
        if (_direccionRepository is null || envio.DireccionEntregaId is not { } direccionId)
            return null;

        var direccion = (await _direccionRepository.GetByIdsAsync(new[] { direccionId }, cancellationToken)).FirstOrDefault();
        if (direccion is null)
            return null;

        if (direccion.ZonaCoberturaId is { } zonaGuardada)
            return zonaGuardada;

        if (_verificadorCobertura is null)
            return null;

        try
        {
            var cobertura = await _verificadorCobertura.VerificarAsync(
                envio.OperadorId, direccion.Calle, direccion.Numero, direccion.Ciudad,
                null, direccion.Latitud, direccion.Longitud, cancellationToken);
            return cobertura.ZonaCoberturaId;
        }
        catch (DomainValidationException)
        {
            // Fuera de cobertura o sin poder ubicarla: queda sin zona y la validación de la clasificación lo informa
            return null;
        }
    }

    private static ClasificacionBulto Convertir(ClasificacionBultoDto dto)
    {
        if (dto is null)
            throw new DomainValidationException("Cada clasificación debe indicar los datos del bulto.");

        // Solo se aceptan nombres de prioridad; un número suelto no debe colarse como valor del enum
        if (string.IsNullOrWhiteSpace(dto.Prioridad)
            || int.TryParse(dto.Prioridad, out _)
            || !Enum.TryParse<PrioridadBulto>(dto.Prioridad.Trim(), ignoreCase: true, out var prioridad)
            || !Enum.IsDefined(prioridad))
        {
            throw new DomainValidationException($"La prioridad '{dto.Prioridad}' no es válida. Valores admitidos: Baja, Normal, Alta, Urgente.");
        }

        return new ClasificacionBulto(dto.CodigoBulto, prioridad, dto.ZonaCoberturaId, dto.Tipo);
    }
}
