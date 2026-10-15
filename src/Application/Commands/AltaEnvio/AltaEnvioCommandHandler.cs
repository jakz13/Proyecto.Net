using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.AltaEnvio;

public class AltaEnvioCommandHandler : IRequestHandler<AltaEnvioCommand, AltaEnvioResultDto>
{
    private const int MaxIntentosCodigoSeguimiento = 5;

    private readonly IEnvioRepository _envioRepository;
    private readonly IDestinatarioRepository _destinatarioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly ICuadroTarifarioRepository _cuadroTarifarioRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AltaEnvioCommandHandler(
        IEnvioRepository envioRepository,
        IDestinatarioRepository destinatarioRepository,
        IDireccionRepository direccionRepository,
        ICuadroTarifarioRepository cuadroTarifarioRepository,
        ICurrentTenant currentTenant,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _envioRepository = envioRepository;
        _destinatarioRepository = destinatarioRepository;
        _direccionRepository = direccionRepository;
        _cuadroTarifarioRepository = cuadroTarifarioRepository;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<AltaEnvioResultDto> Handle(AltaEnvioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Destinatario is null)
            throw new DomainValidationException("Los datos del destinatario son obligatorios.");

        if (request.Direccion is null)
            throw new DomainValidationException("La dirección de entrega es obligatoria.");

        if (request.Bultos is null || request.Bultos.Count == 0)
            throw new DomainValidationException("El envío debe incluir al menos un bulto.");

        // Se construyen y validan todas las entidades antes de tocar los repositorios
        var destinatario = new Destinatario(
            Guid.NewGuid(),
            request.Destinatario.Nombre,
            request.Destinatario.Documento,
            request.Destinatario.Email,
            request.Destinatario.Telefono);

        var direccion = new Direccion(
            Guid.NewGuid(),
            request.Direccion.Calle,
            request.Direccion.Numero,
            request.Direccion.Ciudad,
            request.Direccion.ZonaCoberturaId,
            request.Direccion.Latitud,
            request.Direccion.Longitud,
            request.Direccion.Referencia);

        var codigoSeguimiento = await GenerarCodigoSeguimientoUnicoAsync(cancellationToken);

        var envio = new Envio(
            Guid.NewGuid(),
            request.ComercioId,
            destinatario.Id,
            request.ModalidadServicio,
            codigoSeguimiento,
            direccion.Id,
            request.FranjaHorariaId,
            loteImportacionId: null,
            direccionDestino: direccion.ToString(),
            nombreDestinatario: destinatario.Nombre);

        var identificaciones = new List<string>(request.Bultos.Count);
        for (var i = 0; i < request.Bultos.Count; i++)
        {
            var bulto = request.Bultos[i] ?? throw new DomainValidationException("Los datos del bulto son obligatorios.");
            var identificacion = $"{codigoSeguimiento}-B{i + 1:00}";
            envio.AgregarBulto(identificacion, bulto.Peso, bulto.Alto, bulto.Ancho, bulto.Profundo);
            identificaciones.Add(identificacion);
        }

        // Tarifa según el cuadro vigente del operador; sin regla aplicable el alta se rechaza
        var cuadro = await _cuadroTarifarioRepository.GetVigenteAsync(_currentTenant.OperadorId, _clock.UtcNow, cancellationToken)
            ?? throw new DomainValidationException("El operador no tiene un cuadro tarifario vigente.");

        var regla = cuadro.ObtenerReglaAplicable(
                envio.PesoTotal,
                envio.VolumenTotal,
                envio.ModalidadServicio,
                direccion.ZonaCoberturaId)
            ?? throw new DomainValidationException(
                $"No existe una regla de tarifa aplicable para la modalidad '{envio.ModalidadServicio}' con el peso y volumen informados.");

        envio.AplicarTarifa(regla.CalcularPrecioFinal(), regla.Id);

        await _destinatarioRepository.AddAsync(destinatario, cancellationToken);
        await _direccionRepository.AddAsync(direccion, cancellationToken);
        await _envioRepository.AddAsync(envio, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AltaEnvioResultDto(
            envio.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            envio.TarifaCalculada,
            identificaciones);
    }

    private async Task<string> GenerarCodigoSeguimientoUnicoAsync(CancellationToken cancellationToken)
    {
        for (var intento = 0; intento < MaxIntentosCodigoSeguimiento; intento++)
        {
            var codigo = $"TRK-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await _envioRepository.ExisteCodigoSeguimientoAsync(codigo, cancellationToken))
                return codigo;
        }

        throw new InvalidOperationException("No se pudo generar un código de seguimiento único.");
    }
}
