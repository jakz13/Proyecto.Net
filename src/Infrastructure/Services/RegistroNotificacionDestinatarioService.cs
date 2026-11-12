using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Servicio de notificación al destinatario (UC-35, RF-27, Sección 5.1.5 y 5.5 de la Letra).
/// Registra siempre el evento con el contacto enmascarado en el log estructurado (Regla 05) y,
/// cuando el canal es Email y existen credenciales SMTP configuradas, envía un correo HTML real
/// personalizado con la Identidad Visual del Operador y el enlace directo al Seguimiento Público.
/// </summary>
public sealed class RegistroNotificacionDestinatarioService : INotificacionDestinatarioService
{
    private static readonly ConcurrentDictionary<string, DateTime> EnviosRecientes = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<RegistroNotificacionDestinatarioService> _logger;
    private readonly NotificacionesCorreoOptions _opciones;
    private readonly IServiceScopeFactory? _scopeFactory;

    public RegistroNotificacionDestinatarioService(
        ILogger<RegistroNotificacionDestinatarioService> logger,
        IOptions<NotificacionesCorreoOptions>? opciones = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _logger = logger;
        _opciones = opciones?.Value ?? new NotificacionesCorreoOptions();
        _scopeFactory = scopeFactory;
    }

    public async Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        _logger.LogInformation(
            "Notificación al destinatario por {Canal} a {Contacto}: el envío {Codigo} pasó a {Estado}.",
            notificacion.Canal,
            Enmascarar(notificacion.Contacto),
            notificacion.CodigoSeguimiento,
            notificacion.Estado);

        if (!string.Equals(notificacion.Canal, "Email", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(notificacion.Contacto)
            || !notificacion.Contacto.Contains('@', StringComparison.Ordinal))
        {
            return;
        }

        if (!_opciones.TieneCredencialesConfiguradas)
        {
            return;
        }

        // Evitar envíos duplicados para el mismo paquete y estado dentro de una ventana de 90 segundos
        LimpiarCacheEnviosRecientes();
        var claveDeduplicacion = $"{notificacion.EnvioId}:{notificacion.Estado}:{notificacion.Contacto.Trim()}";
        if (EnviosRecientes.TryGetValue(claveDeduplicacion, out var ultimoEnvio)
            && (DateTime.UtcNow - ultimoEnvio).TotalSeconds < 90)
        {
            return;
        }

        EnviosRecientes[claveDeduplicacion] = DateTime.UtcNow;

        try
        {
            var contexto = await ObtenerContextoMarcaYEnvioAsync(notificacion.EnvioId, null, cancellationToken);
            await DespacharCorreoSmtpAsync(notificacion, contexto, cancellationToken);

            _logger.LogInformation(
                "Correo electrónico enviado exitosamente por SMTP a {Contacto} para el envío {Codigo} ({Estado}).",
                Enmascarar(notificacion.Contacto),
                notificacion.CodigoSeguimiento,
                notificacion.Estado);
        }
        catch (Exception ex)
        {
            // Se registra la advertencia sin interrumpir la transacción del consumidor si el servidor SMTP externo falla
            EnviosRecientes.TryRemove(claveDeduplicacion, out _);
            _logger.LogWarning(
                ex,
                "No se pudo entregar el correo SMTP a {Contacto} para el envío {Codigo}: {Mensaje}",
                Enmascarar(notificacion.Contacto),
                notificacion.CodigoSeguimiento,
                ex.Message);
        }
    }

    /// <summary>
    /// Permite probar el envío de correo con la identidad visual actual del operador desde el Backoffice.
    /// </summary>
    public async Task<(bool Exitoso, string Mensaje)> EnviarCorreoPruebaAsync(
        Guid operadorId,
        string emailDestino,
        EstadoEnvio estadoPrueba = EstadoEnvio.EnTransito,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailDestino) || !emailDestino.Contains('@', StringComparison.Ordinal))
        {
            return (false, "Debe ingresar una dirección de correo electrónico válida.");
        }

        if (!_opciones.TieneCredencialesConfiguradas)
        {
            return (false, "Las credenciales SMTP aún no están configuradas en el servidor (variables Smtp__Usuario y Smtp__Password).");
        }

        try
        {
            var contexto = await ObtenerContextoMarcaYEnvioAsync(Guid.Empty, operadorId, cancellationToken);
            var notificacionPrueba = new NotificacionDestinatario(
                Guid.NewGuid(),
                "TRK-2026-0001",
                "Destinatario de Prueba",
                "Email",
                emailDestino.Trim(),
                estadoPrueba);

            await DespacharCorreoSmtpAsync(notificacionPrueba, contexto, cancellationToken);
            return (true, $"Correo de prueba ({ObtenerTituloEstado(estadoPrueba)}) enviado correctamente a {emailDestino.Trim()} con la identidad visual de {contexto.NombreOperador}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al enviar correo de prueba a {Contacto}.", Enmascarar(emailDestino));
            return (false, $"Error al conectar con el servidor SMTP ({_opciones.Host}:{_opciones.Port}): {ex.Message}");
        }
    }

    private async Task DespacharCorreoSmtpAsync(
        NotificacionDestinatario notificacion,
        ContextoCorreoOperador contexto,
        CancellationToken cancellationToken)
    {
        var urlBase = string.IsNullOrWhiteSpace(_opciones.UrlBaseSeguimiento)
            ? "https://shippingbox.duckdns.org/seguimiento"
            : _opciones.UrlBaseSeguimiento.TrimEnd('/');
        var urlSeguimiento = $"{urlBase}/{Uri.EscapeDataString(notificacion.CodigoSeguimiento)}";

        var asunto = ConstruirAsunto(contexto.NombreOperador, notificacion.CodigoSeguimiento, notificacion.Estado);
        var cuerpoHtml = ConstruirCuerpoHtml(notificacion, contexto, urlSeguimiento);
        var cuerpoTexto = ConstruirCuerpoTextoPlano(notificacion, contexto, urlSeguimiento);

        var emailRemitente = !string.IsNullOrWhiteSpace(_opciones.RemitenteEmail)
            ? _opciones.RemitenteEmail.Trim()
            : _opciones.Usuario!.Trim();

        var nombreRemitente = !string.IsNullOrWhiteSpace(_opciones.RemitenteNombre)
            ? _opciones.RemitenteNombre.Trim()
            : contexto.NombreOperador;

        using var mensaje = new MailMessage
        {
            From = new MailAddress(emailRemitente, nombreRemitente, Encoding.UTF8),
            Subject = asunto,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };

        mensaje.To.Add(new MailAddress(notificacion.Contacto.Trim(), notificacion.NombreDestinatario, Encoding.UTF8));

        if (!string.IsNullOrWhiteSpace(contexto.EmailContacto) && contexto.EmailContacto.Contains('@', StringComparison.Ordinal))
        {
            try
            {
                mensaje.ReplyToList.Add(new MailAddress(contexto.EmailContacto, contexto.NombreOperador));
            }
            catch
            {
                // Ignorar si el correo de respuesta configurado en identidad visual no tiene formato estándar
            }
        }

        var vistaTexto = AlternateView.CreateAlternateViewFromString(cuerpoTexto, Encoding.UTF8, MediaTypeNames.Text.Plain);
        var vistaHtml = AlternateView.CreateAlternateViewFromString(cuerpoHtml, Encoding.UTF8, MediaTypeNames.Text.Html);
        mensaje.AlternateViews.Add(vistaTexto);
        mensaje.AlternateViews.Add(vistaHtml);

        using var clienteSmtp = new SmtpClient(_opciones.Host.Trim(), _opciones.Port)
        {
            EnableSsl = _opciones.UsarSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_opciones.Usuario!.Trim(), _opciones.Password!.Trim()),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Max(5, _opciones.TimeoutSegundos) * 1000
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _opciones.TimeoutSegundos)));
        await clienteSmtp.SendMailAsync(mensaje, cts.Token);
    }

    private async Task<ContextoCorreoOperador> ObtenerContextoMarcaYEnvioAsync(
        Guid envioId,
        Guid? operadorIdExplicito,
        CancellationToken cancellationToken)
    {
        var contexto = new ContextoCorreoOperador();
        if (_scopeFactory is null)
            return contexto;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<AppDbContext>();
            if (db is null)
                return contexto;

            Guid? operadorId = operadorIdExplicito;

            if (envioId != Guid.Empty)
            {
                var envio = await db.Envios
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == envioId, cancellationToken);

                if (envio is not null)
                {
                    operadorId = envio.OperadorId;
                    contexto.ModalidadServicio = envio.ModalidadServicio.ToString();
                    if (!string.IsNullOrWhiteSpace(envio.DireccionDestino))
                    {
                        contexto.DireccionEntrega = envio.DireccionDestino;
                    }
                    else if (envio.DireccionEntregaId.HasValue)
                    {
                        var direccion = await db.Direcciones
                            .AsNoTracking()
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(d => d.Id == envio.DireccionEntregaId.Value, cancellationToken);

                        if (direccion is not null)
                        {
                            var numero = string.IsNullOrWhiteSpace(direccion.Numero) || direccion.Numero == "S/N"
                                ? string.Empty
                                : $" {direccion.Numero}";
                            contexto.DireccionEntrega = $"{direccion.Calle}{numero}, {direccion.Ciudad}".Trim();
                        }
                    }
                }
            }

            if (operadorId.HasValue && operadorId.Value != Guid.Empty)
            {
                var operador = await db.Operadores
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.Id == operadorId.Value, cancellationToken);

                if (operador is not null && !string.IsNullOrWhiteSpace(operador.NombreComercial))
                {
                    contexto.NombreOperador = operador.NombreComercial;
                }

                var identidad = await db.IdentidadesVisuales
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.OperadorId == operadorId.Value, cancellationToken);

                if (identidad is not null)
                {
                    if (!string.IsNullOrWhiteSpace(identidad.ColorPrimario))
                        contexto.ColorPrimario = identidad.ColorPrimario;
                    if (!string.IsNullOrWhiteSpace(identidad.ColorSecundario))
                        contexto.ColorSecundario = identidad.ColorSecundario;
                    if (!string.IsNullOrWhiteSpace(identidad.EmailContacto))
                        contexto.EmailContacto = identidad.EmailContacto;
                    if (!string.IsNullOrWhiteSpace(identidad.TelefonoContacto))
                        contexto.TelefonoContacto = identidad.TelefonoContacto;
                    if (!string.IsNullOrWhiteSpace(identidad.SitioWeb))
                        contexto.SitioWeb = identidad.SitioWeb;
                    if (!string.IsNullOrWhiteSpace(identidad.MensajeComunicaciones))
                        contexto.MensajeComunicaciones = identidad.MensajeComunicaciones;
                }
            }
        }
        catch
        {
            // Si la base de datos no está disponible en un test unitario aislado, usa los valores por defecto de marca
        }

        return contexto;
    }

    private static string ConstruirAsunto(string nombreOperador, string codigoSeguimiento, EstadoEnvio estado) =>
        estado switch
        {
            EstadoEnvio.Admitido => $"[{nombreOperador}] Tu envío {codigoSeguimiento} fue registrado",
            EstadoEnvio.AsignadoARuta => $"[{nombreOperador}] Tu envío {codigoSeguimiento} fue asignado a una hoja de ruta",
            EstadoEnvio.EnTransito => $"[{nombreOperador}] ¡Tu paquete {codigoSeguimiento} está en camino!",
            EstadoEnvio.Entregado => $"[{nombreOperador}] ¡Tu paquete {codigoSeguimiento} fue entregado!",
            EstadoEnvio.EntregadoParcialmente => $"[{nombreOperador}] Entrega parcial registrada para tu envío {codigoSeguimiento}",
            EstadoEnvio.NoEntregado => $"[{nombreOperador}] Visitamos tu domicilio por el envío {codigoSeguimiento}",
            EstadoEnvio.Reprogramado => $"[{nombreOperador}] Tu envío {codigoSeguimiento} fue reprogramado",
            _ => $"[{nombreOperador}] Actualización de tu envío {codigoSeguimiento}"
        };

    private static string ObtenerTituloEstado(EstadoEnvio estado) =>
        estado switch
        {
            EstadoEnvio.Admitido => "Envío Admitido",
            EstadoEnvio.AsignadoARuta => "Asignado a Hoja de Ruta",
            EstadoEnvio.EnTransito => "En Camino a tu Domicilio",
            EstadoEnvio.Entregado => "Paquete Entregado",
            EstadoEnvio.EntregadoParcialmente => "Entrega Parcial Registrada",
            EstadoEnvio.NoEntregado => "Intento de Entrega sin Éxito",
            EstadoEnvio.Reprogramado => "Entrega Reprogramada",
            _ => estado.ToString()
        };

    private static string ObtenerMensajeEstado(EstadoEnvio estado) =>
        estado switch
        {
            EstadoEnvio.Admitido =>
                "Hemos registrado tu envío en nuestra plataforma logística. En cuanto ingrese a nuestro centro de distribución y salga a reparto te volveremos a avisar por este medio.",
            EstadoEnvio.AsignadoARuta =>
                "Tu paquete ya fue clasificado en nuestro depósito y asignado a una hoja de ruta de reparto. Próximamente el repartidor iniciará el recorrido hacia tu domicilio.",
            EstadoEnvio.EnTransito =>
                "¡El repartidor acaba de iniciar la ruta con tu paquete! Tu envío se encuentra en tránsito hacia la dirección de entrega indicada.",
            EstadoEnvio.Entregado =>
                "Confirmamos que tu paquete fue entregado exitosamente en el domicilio de destino. ¡Muchas gracias por confiar en nuestro servicio!",
            EstadoEnvio.EntregadoParcialmente =>
                "Se concretó la entrega de una parte de los bultos de tu envío. Los bultos restantes quedaron registrados para su próxima gestión.",
            EstadoEnvio.NoEntregado =>
                "Nuestro repartidor visitó tu domicilio pero no fue posible concretar la entrega. Podés ingresar al enlace de seguimiento público a continuación para elegir una nueva franja horaria de reprogramación.",
            EstadoEnvio.Reprogramado =>
                "Tu solicitud de reprogramación fue confirmada exitosamente. Visitaremos tu domicilio en la nueva ventana horaria acordada.",
            _ =>
                "Se registró una actualización en el estado de tu envío."
        };

    private static string ConstruirCuerpoTextoPlano(
        NotificacionDestinatario notificacion,
        ContextoCorreoOperador contexto,
        string urlSeguimiento)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{contexto.NombreOperador} — Notificación de Seguimiento");
        sb.AppendLine(new string('=', 56));
        sb.AppendLine();
        sb.AppendLine($"Hola {notificacion.NombreDestinatario},");
        sb.AppendLine();
        sb.AppendLine($"Estado actual: {ObtenerTituloEstado(notificacion.Estado)}");
        sb.AppendLine($"Código de seguimiento: {notificacion.CodigoSeguimiento}");
        if (!string.IsNullOrWhiteSpace(contexto.DireccionEntrega))
        {
            sb.AppendLine($"Dirección de entrega: {contexto.DireccionEntrega}");
        }
        sb.AppendLine();
        sb.AppendLine(ObtenerMensajeEstado(notificacion.Estado));
        sb.AppendLine();
        sb.AppendLine("Consultá el estado en vivo y gestioná tu entrega desde el siguiente enlace público:");
        sb.AppendLine(urlSeguimiento);
        sb.AppendLine();
        sb.AppendLine(new string('-', 56));
        if (!string.IsNullOrWhiteSpace(contexto.MensajeComunicaciones))
        {
            sb.AppendLine(contexto.MensajeComunicaciones);
        }
        sb.AppendLine($"Contacto: {contexto.EmailContacto} | Tel: {contexto.TelefonoContacto}");
        return sb.ToString();
    }

    private static string ConstruirCuerpoHtml(
        NotificacionDestinatario notificacion,
        ContextoCorreoOperador contexto,
        string urlSeguimiento)
    {
        var tituloEstado = WebUtility.HtmlEncode(ObtenerTituloEstado(notificacion.Estado));
        var mensajeEstado = WebUtility.HtmlEncode(ObtenerMensajeEstado(notificacion.Estado));
        var nombreDestinatario = WebUtility.HtmlEncode(notificacion.NombreDestinatario);
        var codigo = WebUtility.HtmlEncode(notificacion.CodigoSeguimiento);
        var nombreOperador = WebUtility.HtmlEncode(contexto.NombreOperador);
        var colorPrimario = WebUtility.HtmlEncode(contexto.ColorPrimario);
        var colorSecundario = WebUtility.HtmlEncode(contexto.ColorSecundario);
        var direccion = WebUtility.HtmlEncode(contexto.DireccionEntrega ?? "Domicilio declarado por el comercio remitente");
        var firma = WebUtility.HtmlEncode(contexto.MensajeComunicaciones ?? $"Gracias por confiar en {contexto.NombreOperador}.");
        var emailContacto = WebUtility.HtmlEncode(contexto.EmailContacto);
        var telefonoContacto = WebUtility.HtmlEncode(contexto.TelefonoContacto);
        var urlSegura = WebUtility.HtmlEncode(urlSeguimiento);

        return $@"<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{tituloEstado} - {codigo}</title>
</head>
<body style=""margin:0;padding:0;background-color:#F1F5F9;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#0F172A;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#F1F5F9;padding:28px 12px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:14px;overflow:hidden;box-shadow:0 4px 16px rgba(15,23,42,0.08);"">
          <!-- Cabecera con Identidad Visual del Operador -->
          <tr>
            <td style=""background-color:{colorPrimario};padding:24px 32px;border-bottom:4px solid {colorSecundario};"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
                <tr>
                  <td>
                    <div style=""font-size:11px;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;color:#E2E8F0;margin-bottom:4px;"">
                      Notificación Automática de Envío
                    </div>
                    <div style=""font-size:22px;font-weight:800;color:#FFFFFF;"">
                      {nombreOperador}
                    </div>
                  </td>
                  <td align=""right"">
                    <span style=""display:inline-block;background-color:{colorSecundario};color:#FFFFFF;font-size:12px;font-weight:700;padding:6px 12px;border-radius:999px;"">
                      {tituloEstado}
                    </span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Cuerpo Principal -->
          <tr>
            <td style=""padding:32px;"">
              <p style=""margin:0 0 12px 0;font-size:16px;color:#0F172A;"">
                Hola <strong>{nombreDestinatario}</strong>,
              </p>
              <p style=""margin:0 0 24px 0;font-size:15px;line-height:1.6;color:#334155;"">
                {mensajeEstado}
              </p>

              <!-- Tarjeta de Datos del Paquete -->
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#F8FAFC;border:1px solid #E2E8F0;border-radius:10px;padding:20px;margin-bottom:26px;"">
                <tr>
                  <td style=""padding-bottom:12px;border-bottom:1px solid #E2E8F0;"">
                    <div style=""font-size:11px;font-weight:700;text-transform:uppercase;color:#64748B;"">Código de Seguimiento</div>
                    <div style=""font-size:20px;font-weight:800;font-family:Consolas,monospace;color:{colorPrimario};margin-top:2px;"">{codigo}</div>
                  </td>
                </tr>
                <tr>
                  <td style=""padding-top:12px;"">
                    <div style=""font-size:12px;color:#64748B;""><strong>Estado actual:</strong> {tituloEstado}</div>
                    <div style=""font-size:12px;color:#64748B;margin-top:4px;""><strong>Destino:</strong> {direccion}</div>
                  </td>
                </tr>
              </table>

              <!-- Botón de Seguimiento Público -->
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-bottom:20px;"">
                <tr>
                  <td align=""center"">
                    <a href=""{urlSegura}"" target=""_blank"" style=""display:inline-block;background-color:{colorPrimario};color:#FFFFFF;text-decoration:none;font-size:15px;font-weight:700;padding:14px 28px;border-radius:8px;"">
                      Seguir mi envío en tiempo real &rarr;
                    </a>
                  </td>
                </tr>
              </table>

              <p style=""margin:0;font-size:12px;text-align:center;color:#64748B;"">
                O copiá y pegá este enlace en tu navegador:<br />
                <a href=""{urlSegura}"" style=""color:{colorPrimario};word-break:break-all;"">{urlSegura}</a>
              </p>
            </td>
          </tr>

          <!-- Pie con Firma e Identidad del Operador -->
          <tr>
            <td style=""background-color:#F8FAFC;border-top:1px solid #E2E8F0;padding:20px 32px;text-align:center;"">
              <p style=""margin:0 0 8px 0;font-size:13px;font-style:italic;color:#475569;"">
                &ldquo;{firma}&rdquo;
              </p>
              <p style=""margin:0;font-size:12px;color:#64748B;"">
                <strong>{nombreOperador}</strong> &middot; Atención al destinatario: {emailContacto} &middot; Tel: {telefonoContacto}
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static void LimpiarCacheEnviosRecientes()
    {
        if (EnviosRecientes.Count < 100)
            return;

        var umbral = DateTime.UtcNow.AddMinutes(-5);
        foreach (var item in EnviosRecientes)
        {
            if (item.Value < umbral)
            {
                EnviosRecientes.TryRemove(item.Key, out _);
            }
        }
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

    private sealed class ContextoCorreoOperador
    {
        public string NombreOperador { get; set; } = "TransLogística Express";
        public string ColorPrimario { get; set; } = "#1D4ED8";
        public string ColorSecundario { get; set; } = "#F97316";
        public string EmailContacto { get; set; } = "contacto@translogistica.uy";
        public string TelefonoContacto { get; set; } = "+598 2900 1122";
        public string? SitioWeb { get; set; } = "https://shippingbox.duckdns.org";
        public string? MensajeComunicaciones { get; set; }
        public string? DireccionEntrega { get; set; }
        public string? ModalidadServicio { get; set; }
    }
}
