namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Configuración de conexión a RabbitMQ. Las credenciales no se versionan: se toman de user-secrets en desarrollo
/// y de variables de entorno (<c>RabbitMq__UserName</c>, <c>RabbitMq__Password</c>) o del gestor de secretos en producción.
/// </summary>
public class RabbitMqOptions
{
    public const string Seccion = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
