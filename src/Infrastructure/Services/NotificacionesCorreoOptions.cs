namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Opciones de configuración para el envío real de notificaciones por correo electrónico (SMTP) al destinatario.
/// Se enlaza desde la sección "Smtp" de appsettings.json o mediante variables de entorno (Smtp__Usuario, Smtp__Password, etc.).
/// </summary>
public sealed class NotificacionesCorreoOptions
{
    public const string Seccion = "Smtp";

    /// <summary>Servidor SMTP (por defecto smtp.gmail.com).</summary>
    public string Host { get; set; } = "smtp.gmail.com";

    /// <summary>Puerto del servidor SMTP (587 para STARTTLS en Gmail/Outlook).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Indica si se utiliza conexión segura TLS/SSL.</summary>
    public bool UsarSsl { get; set; } = true;

    /// <summary>Cuenta de usuario del servidor SMTP (ej. tucorreo@gmail.com).</summary>
    public string? Usuario { get; set; }

    /// <summary>Contraseña o Contraseña de Aplicación (App Password de 16 caracteres en Gmail).</summary>
    public string? Password { get; set; }

    /// <summary>Dirección de correo remitente opcional (si se omite, se utiliza Usuario).</summary>
    public string? RemitenteEmail { get; set; }

    /// <summary>Nombre visible del remitente opcional (si se omite, se utiliza el Nombre Comercial del Operador).</summary>
    public string? RemitenteNombre { get; set; }

    /// <summary>URL base pública donde el destinatario consulta el seguimiento de su paquete al abrir el enlace del correo.</summary>
    public string UrlBaseSeguimiento { get; set; } = "https://shippingbox.duckdns.org/seguimiento";

    /// <summary>Tiempo máximo de espera en segundos para la operación SMTP.</summary>
    public int TimeoutSegundos { get; set; } = 15;

    /// <summary>Indica si las credenciales mínimas están presentes para despachar correos reales por SMTP.</summary>
    public bool TieneCredencialesConfiguradas =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !string.IsNullOrWhiteSpace(Password);
}

