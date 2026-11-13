using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Al dar de baja un comercio en un operador, cancela sus envíos y retiros que todavía no salieron a la calle
/// (y las hojas de ruta no despachadas que queden sin paradas vigentes). Lo que ya está despachado/en tránsito sigue su curso.
/// </summary>
public class BajaComercioProcesador
{
    public const string MotivoCancelacionPorBaja = "Comercio dado de baja por el operador antes de la salida a calle.";

    private readonly IEnvioRepository _envioRepository;
    private readonly IRetiroRepository _retiroRepository;
    private readonly IRutaRepository _rutaRepository;

    public BajaComercioProcesador(
        IEnvioRepository envioRepository,
        IRetiroRepository retiroRepository,
        IRutaRepository rutaRepository)
    {
        _envioRepository = envioRepository;
        _retiroRepository = retiroRepository;
        _rutaRepository = rutaRepository;
    }

    public async Task CancelarOperacionesPreviasASalidaAsync(
        Guid operadorId,
        Guid comercioOperadorId,
        Guid? responsableUsuarioId,
        CancellationToken cancellationToken = default)
    {
        if (operadorId == Guid.Empty || comercioOperadorId == Guid.Empty)
            return;

        var envios = await _envioRepository.ListarParaBajaComercioAsync(operadorId, comercioOperadorId, cancellationToken);
        var retiros = await _retiroRepository.ListarParaBajaComercioAsync(operadorId, comercioOperadorId, cancellationToken);

        var rutasIds = envios
            .Where(e => e.EstadoActual == EstadoEnvio.AsignadoARuta && e.HojaDeRutaId is { } id && id != Guid.Empty)
            .Select(e => e.HojaDeRutaId!.Value)
            .Concat(retiros
                .Where(r => r.EstadoActual == EstadoRetiro.AsignadoARuta && r.HojaDeRutaId is { } id && id != Guid.Empty)
                .Select(r => r.HojaDeRutaId!.Value))
            .Distinct()
            .ToList();

        var rutasPorId = rutasIds.Count > 0
            ? (await _rutaRepository.GetByIdsWithParadasYAsignacionesAsync(rutasIds, cancellationToken))
                .ToDictionary(r => r.Id)
            : new Dictionary<Guid, HojaDeRuta>();

        var rutasTocados = new HashSet<Guid>();

        foreach (var envio in envios)
        {
            if (envio.EstadoActual is EstadoEnvio.Admitido or EstadoEnvio.EnDeposito)
            {
                envio.Cancelar(MotivoCancelacionPorBaja, responsableUsuarioId, "Backoffice");
                await _envioRepository.UpdateAsync(envio, cancellationToken);
                continue;
            }

            if (envio.EstadoActual == EstadoEnvio.AsignadoARuta
                && envio.HojaDeRutaId is Guid rutaId
                && rutasPorId.TryGetValue(rutaId, out var ruta)
                && ruta.NoSalioACalle)
            {
                if (ruta.Paradas.Any(p => p.EnvioId == envio.Id && p.Estado != "Cancelada"))
                    ruta.RetirarEnvio(envio.Id);

                envio.RetirarDeRuta(MotivoCancelacionPorBaja, responsableUsuarioId);
                envio.Cancelar(MotivoCancelacionPorBaja, responsableUsuarioId, "Backoffice");
                await _envioRepository.UpdateAsync(envio, cancellationToken);
                rutasTocados.Add(ruta.Id);
            }
        }

        foreach (var retiro in retiros)
        {
            if (retiro.EstadoActual == EstadoRetiro.Solicitado)
            {
                retiro.Cancelar(MotivoCancelacionPorBaja, responsableUsuarioId, Retiro.OrigenBackoffice);
                await _retiroRepository.UpdateAsync(retiro, cancellationToken);
                continue;
            }

            if (retiro.EstadoActual == EstadoRetiro.AsignadoARuta
                && retiro.HojaDeRutaId is Guid rutaId
                && rutasPorId.TryGetValue(rutaId, out var ruta)
                && ruta.NoSalioACalle)
            {
                if (ruta.Paradas.Any(p => p.RetiroId == retiro.Id && p.Estado != "Cancelada"))
                    ruta.RetirarRetiro(retiro.Id);

                retiro.LiberarDeRuta(MotivoCancelacionPorBaja, responsableUsuarioId);
                retiro.Cancelar(MotivoCancelacionPorBaja, responsableUsuarioId, Retiro.OrigenBackoffice);
                await _retiroRepository.UpdateAsync(retiro, cancellationToken);
                rutasTocados.Add(ruta.Id);
            }
        }

        foreach (var rutaId in rutasTocados)
        {
            var ruta = rutasPorId[rutaId];
            if (ruta.NoSalioACalle && !ruta.TieneParadasVigentes)
                ruta.Cancelar(MotivoCancelacionPorBaja);

            await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        }
    }
}

