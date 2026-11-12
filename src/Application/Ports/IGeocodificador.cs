namespace UltimaMilla.Application.Ports;

/// <summary>Convierte una dirección postal en coordenadas.</summary>
public interface IGeocodificador
{
    /// <summary>Devuelve la coordenada de la dirección, o null si no se pudo ubicar. Lanza excepción si el servicio no está disponible.</summary>
    Task<(double Latitud, double Longitud)?> GeocodificarAsync(string calle, string numero, string ciudad, CancellationToken cancellationToken = default);
}
