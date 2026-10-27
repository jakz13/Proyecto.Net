using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Canal de notificación al destinatario para desarrollo y laboratorio: no hay proveedor de correo ni de SMS contratado,
/// así que el envío se registra en el log con el contacto enmascarado. Se reemplaza por un adaptador real sin tocar la aplicación.
/// </summary>
public sealed class RegistroNotificacionDestinatarioService : INotificacionDestinatarioService
{
    private readonly ILogger<RegistroNotificacionDestinatarioService> _logger;

    public RegistroNotificacionDestinatarioService(ILogger<RegistroNotificacionDestinatarioService> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        _logger.LogInformation(
            "Notificación al destinatario por {Canal} a {Contacto}: el envío {Codigo} pasó a {Estado}.",
            notificacion.Canal,
            Enmascarar(notificacion.Contacto),
            notificacion.CodigoSeguimiento,
            notificacion.Estado);

        return Task.CompletedTask;
    }

    /// <summary>Oculta el dato de contacto: de un correo deja la inicial y el dominio; de un teléfono, los últimos 2 dígitos.</summary>
    public static string Enmascarar(string? contacto)
    {
        if (string.IsNullOrWhiteSpace(contacto))
            return "***";

        var valor = contacto.Trim();
        var arroba = valor.IndexOf('@');
        if (arroba > 0)
            return $"{valor[0]}***{valor[arroba..]}";

        return valor.Length <= 2 ? "***" : $"***{valor[^2..]}";
    }
}
