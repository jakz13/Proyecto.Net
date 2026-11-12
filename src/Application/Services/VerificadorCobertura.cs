using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Services;

/// <summary>Zona del operador que cubre una dirección y la coordenada con la que se verificó (si hubo).</summary>
public record CoberturaVerificada(Guid? ZonaCoberturaId, double? Latitud, double? Longitud);

/// <summary>
/// Regla de cobertura: un envío solo se admite si la dirección cae dentro de una zona activa que el operador definió.
/// Con coordenadas informadas se usan tal cual; sin ellas la dirección se geocodifica. Si el alta ya trae una zona,
/// debe ser una zona activa del operador (y, si además hay coordenadas, contenerlas).
/// </summary>
public class VerificadorCobertura
{
    private readonly OpcionesCobertura _opciones;
    private readonly IZonaCoberturaRepository _zonas;
    private readonly IGeocodificador _geocodificador;

    public VerificadorCobertura(IZonaCoberturaRepository zonas, IGeocodificador geocodificador, OpcionesCobertura? opciones = null)
    {
        _opciones = opciones ?? new OpcionesCobertura();
        _zonas = zonas;
        _geocodificador = geocodificador;
    }

    public async Task<CoberturaVerificada> VerificarAsync(
        Guid operadorId,
        string calle,
        string numero,
        string ciudad,
        Guid? zonaCoberturaId,
        double? latitud,
        double? longitud,
        CancellationToken cancellationToken = default)
    {
        // Apagado solo para las pruebas de integración, que no tienen salida a Internet; en producción siempre está activo
        if (!_opciones.Validar)
            return new CoberturaVerificada(zonaCoberturaId, latitud, longitud);

        var activas = await _zonas.ListarZonasDelOperadorAsync(operadorId, null, true, cancellationToken);
        if (activas.Count == 0)
            throw new DomainValidationException("El operador no tiene zonas de cobertura activas: no puede admitir envíos.");

        if (zonaCoberturaId.HasValue && !activas.Any(z => z.Id == zonaCoberturaId.Value))
            throw new DomainValidationException("La zona de cobertura indicada no existe o no está activa para el operador.");

        if (zonaCoberturaId.HasValue && !(latitud.HasValue && longitud.HasValue))
            return new CoberturaVerificada(zonaCoberturaId.Value, null, null);

        if (!(latitud.HasValue && longitud.HasValue))
        {
            (double Latitud, double Longitud)? ubicada;
            try
            {
                ubicada = await _geocodificador.GeocodificarAsync(calle, numero, ciudad, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new DomainValidationException("No se pudo verificar la cobertura de la dirección en este momento. Intente nuevamente en unos minutos.");
            }

            if (ubicada is null)
                throw new DomainValidationException($"No se pudo ubicar la dirección \"{calle} {numero}, {ciudad}\". Revise calle, número y ciudad: solo se admiten envíos dentro de las zonas de cobertura del operador.");

            latitud = ubicada.Value.Latitud;
            longitud = ubicada.Value.Longitud;
        }

        var candidatas = zonaCoberturaId.HasValue ? activas.Where(z => z.Id == zonaCoberturaId.Value) : activas;
        var zona = candidatas.FirstOrDefault(z => z.Contiene(latitud!.Value, longitud!.Value))
            ?? throw new DomainValidationException(
                $"La dirección \"{calle} {numero}, {ciudad}\" está fuera de las zonas de cobertura del operador. No se puede admitir el envío.");

        return new CoberturaVerificada(zona.Id, latitud, longitud);
    }
}

/// <summary>Opciones de la verificación de cobertura (sección Cobertura de la configuración).</summary>
public sealed class OpcionesCobertura
{
    public bool Validar { get; init; } = true;
}
