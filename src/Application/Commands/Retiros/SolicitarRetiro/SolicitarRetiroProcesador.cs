using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;

/// <summary>
/// Admite una solicitud de retiro para un comercio y operador dados: valida, tarifica y agrega las entidades al contexto.
/// Lo comparten el portal y la API; quien lo invoca confirma los cambios con IUnitOfWork. El evento hacia el worker lo deja
/// la propia unidad de trabajo en la misma transacción (outbox). Si el comercio repite una referencia, devuelve la solicitud existente.
/// </summary>
public class SolicitarRetiroProcesador
{
    /// <summary>Modalidad con la que el cuadro tarifario identifica la tarifa propia del retiro (RF 41).</summary>
    public const string ModalidadRetiro = "Retiro";

    private const int MaxIntentosCodigoSeguimiento = 5;

    private readonly IRetiroRepository _retiroRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly ICuadroTarifarioRepository _cuadroTarifarioRepository;
    private readonly IClock _clock;

    public SolicitarRetiroProcesador(
        IRetiroRepository retiroRepository,
        IDireccionRepository direccionRepository,
        ICuadroTarifarioRepository cuadroTarifarioRepository,
        IClock clock)
    {
        _retiroRepository = retiroRepository;
        _direccionRepository = direccionRepository;
        _cuadroTarifarioRepository = cuadroTarifarioRepository;
        _clock = clock;
    }

    public async Task<SolicitarRetiroResultDto> ProcesarAsync(
        Guid operadorId,
        SolicitarRetiroCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Contacto is null)
            throw new DomainValidationException("Los datos de contacto de quien entrega los bultos son obligatorios.");

        if (request.Direccion is null)
            throw new DomainValidationException("El domicilio de retiro es obligatorio.");

        if (request.Bultos is null || request.Bultos.Count == 0)
            throw new DomainValidationException("El retiro debe incluir al menos un bulto.");

        var referencia = string.IsNullOrWhiteSpace(request.ReferenciaExterna) ? null : request.ReferenciaExterna.Trim();
        if (referencia is not null)
        {
            var existente = await _retiroRepository.GetByReferenciaExternaAsync(request.ComercioId, referencia, cancellationToken);
            if (existente is not null)
            {
                return new SolicitarRetiroResultDto(
                    existente.Id,
                    existente.CodigoSeguimientoPublico,
                    existente.EstadoActual.ToString(),
                    existente.TarifaCalculada,
                    existente.Bultos.Select(b => b.Identificacion).ToList(),
                    Duplicado: true);
            }
        }

        if (request.FechaSolicitada.HasValue && request.FechaSolicitada.Value < DateOnly.FromDateTime(_clock.UtcNow).AddDays(-1))
            throw new DomainValidationException("La fecha pedida para el retiro no puede estar en el pasado.");

        // Se construyen y validan todas las entidades antes de tocar los repositorios
        var direccion = new Direccion(
            Guid.NewGuid(),
            request.Direccion.Calle,
            request.Direccion.Numero,
            request.Direccion.Ciudad,
            request.Direccion.ZonaCoberturaId,
            request.Direccion.Latitud,
            request.Direccion.Longitud,
            request.Direccion.Referencia);

        var codigo = await GenerarCodigoSeguimientoUnicoAsync(cancellationToken);

        var retiro = new Retiro(
            Guid.NewGuid(),
            operadorId,
            request.ComercioId,
            codigo,
            request.Contacto.Nombre,
            direccion.Id,
            direccion.ToString(),
            request.Motivo,
            request.Contacto.Telefono,
            request.Contacto.Email,
            request.FranjaHorariaId,
            request.FechaSolicitada,
            referencia,
            request.Origen,
            fechaSolicitud: _clock.UtcNow);

        foreach (var bulto in request.Bultos)
        {
            if (bulto is null)
                throw new DomainValidationException("Los datos del bulto son obligatorios.");

            retiro.AgregarBulto(bulto.Descripcion ?? string.Empty, bulto.Peso, bulto.Alto, bulto.Ancho, bulto.Profundo);
        }

        // El retiro tiene su propia tarifa dentro del cuadro vigente del operador; sin ella la solicitud se rechaza
        var cuadro = await _cuadroTarifarioRepository.GetVigenteAsync(operadorId, _clock.UtcNow, cancellationToken)
            ?? throw new DomainValidationException("El operador no tiene un cuadro tarifario vigente.");

        var regla = cuadro.ObtenerReglaAplicable(retiro.PesoTotal, retiro.VolumenTotal, ModalidadRetiro, direccion.ZonaCoberturaId)
            ?? throw new DomainValidationException(
                "El operador no tiene una tarifa de retiro aplicable al peso y volumen informados.");

        retiro.AplicarTarifa(regla.CalcularPrecioFinal(), regla.Id);

        await _direccionRepository.AddAsync(direccion, cancellationToken);
        await _retiroRepository.AddAsync(retiro, cancellationToken);

        return new SolicitarRetiroResultDto(
            retiro.Id,
            retiro.CodigoSeguimientoPublico,
            retiro.EstadoActual.ToString(),
            retiro.TarifaCalculada,
            retiro.Bultos.Select(b => b.Identificacion).ToList());
    }

    private async Task<string> GenerarCodigoSeguimientoUnicoAsync(CancellationToken cancellationToken)
    {
        for (var intento = 0; intento < MaxIntentosCodigoSeguimiento; intento++)
        {
            var codigo = $"RET-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await _retiroRepository.ExisteCodigoSeguimientoAsync(codigo, cancellationToken))
                return codigo;
        }

        throw new InvalidOperationException("No se pudo generar un código de seguimiento único para el retiro.");
    }
}
