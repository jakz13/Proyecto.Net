using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

public class ConfirmarCargaCommandHandler : IRequestHandler<ConfirmarCargaCommand, ConfirmarCargaResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IOutboxStorage _outboxStorage;

    public ConfirmarCargaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IOutboxStorage outboxStorage)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _outboxStorage = outboxStorage;
    }

    public async Task<ConfirmarCargaResultDto> Handle(ConfirmarCargaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        // Una ruta ya despachada no admite cambios en la carga: se responde sin modificar nada
        if (ruta.Estado is "Despachada" or "EnTransito" or "Finalizada")
        {
            return new ConfirmarCargaResultDto(
                ruta.Id, ruta.Estado, ruta.EscaneosCarga.Count, 0, 0, true,
                "La hoja de ruta ya fue despachada: la carga no se puede modificar (operación idempotente).");
        }

        var codigos = (request.CodigosBultosCargados ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet();

        if (codigos.Count == 0)
            throw new DomainValidationException("Debe ingresar al menos un bulto cargado.");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);
        var bultosDeLaRuta = envios
            .SelectMany(e => e.Bultos)
            .Where(b => b.EstadoBulto is "Recibido" or "Danado")
            .ToList();

        var cargados = bultosDeLaRuta.Where(b => codigos.Contains(b.Identificacion)).ToList();
        var sobrantes = codigos.Count(c => bultosDeLaRuta.All(b => b.Identificacion != c));
        var faltantes = bultosDeLaRuta.Count - cargados.Count;

        var yaRegistrada = ruta.Estado == "EnCarga";
        ruta.RegistrarCargaEscaneada(cargados.Select(b => b.Id));

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(EventosIntegracion.DeRuta(EventosIntegracion.Tipos.CargaVehiculoRegistrada, ruta, new
        {
            BultosCargados = cargados.Count,
            Faltantes = faltantes,
            Sobrantes = sobrantes
        }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mensaje = faltantes > 0 || sobrantes > 0
            ? $"Carga registrada con discrepancias: {faltantes} bulto(s) faltante(s) y {sobrantes} sobrante(s)."
            : "Carga registrada sin discrepancias.";

        return new ConfirmarCargaResultDto(ruta.Id, ruta.Estado, cargados.Count, faltantes, sobrantes, yaRegistrada, mensaje);
    }
}
