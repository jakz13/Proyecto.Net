namespace UltimaMilla.Mobile.Services.Tracking;

/// <summary>Posición real del dispositivo para auditar una operación. Si no hay GPS devuelve <c>null</c>: nunca una coordenada inventada.</summary>
public static class UbicacionDispositivo
{
    public static async Task<Location?> ObtenerAsync()
    {
        try
        {
            var ubicacion = await Geolocation.Default.GetLastKnownLocationAsync();
            if (ubicacion is null)
            {
                var solicitud = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                ubicacion = await Geolocation.Default.GetLocationAsync(solicitud);
            }

            return ubicacion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UbicacionDispositivo] GPS no disponible: {ex.Message}");
            return null;
        }
    }
}
