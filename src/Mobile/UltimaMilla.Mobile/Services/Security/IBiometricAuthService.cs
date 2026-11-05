namespace UltimaMilla.Mobile.Services.Security;

/// <summary>
/// Contrato del servicio de autenticación biométrica y validación de identidad para operaciones sensibles.
/// </summary>
public interface IBiometricAuthService
{
    /// <summary>
    /// Solicita validación biométrica o credencial segura del dispositivo para autorizar una acción crítica.
    /// </summary>
    Task<bool> ValidarIdentidadAsync(string titulo, string motivo);

    /// <summary>
    /// Consulta si el dispositivo actual cuenta con hardware biométrico o bloqueo de pantalla configurado.
    /// </summary>
    Task<bool> DispositivoSoportaBiometriaAsync();
}

