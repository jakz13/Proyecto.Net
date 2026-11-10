using System.Net;
using System.Net.Sockets;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Defensa contra SSRF en los avisos a comercios: la URL la elige el comercio, así que el servidor no debe poder usarse
/// para llegar a su propia red (base de datos, cola, metadatos de la nube, localhost). La comprobación se hace sobre la
/// dirección IP ya resuelta, justo al abrir la conexión, para que un nombre que cambie de IP (DNS rebinding) no la eludan.
/// </summary>
public static class DestinoWebhookSeguro
{
    /// <summary>Indica si la dirección pertenece a un rango que no es de Internet público.</summary>
    public static bool EsDireccionInterna(IPAddress direccion)
    {
        if (direccion.IsIPv4MappedToIPv6)
            direccion = direccion.MapToIPv4();

        if (IPAddress.IsLoopback(direccion) || direccion.Equals(IPAddress.Any) || direccion.Equals(IPAddress.IPv6Any))
            return true;

        if (direccion.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = direccion.GetAddressBytes();
            return direccion.IsIPv6LinkLocal || direccion.IsIPv6SiteLocal || direccion.IsIPv6Multicast
                   || (b[0] & 0xFE) == 0xFC; // fc00::/7 (únicas locales)
        }

        var o = direccion.GetAddressBytes();
        return o[0] == 10                                          // 10.0.0.0/8
               || (o[0] == 172 && o[1] >= 16 && o[1] <= 31)        // 172.16.0.0/12 (incluye las redes de Docker)
               || (o[0] == 192 && o[1] == 168)                     // 192.168.0.0/16
               || (o[0] == 169 && o[1] == 254)                     // 169.254.0.0/16 (metadatos de la nube)
               || (o[0] == 100 && o[1] >= 64 && o[1] <= 127)       // 100.64.0.0/10
               || o[0] == 0 || o[0] >= 224;                        // "esta red", multicast y reservadas
    }

    /// <summary>Resuelve el destino y abre la conexión solo si no es interno (salvo excepciones configuradas).</summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CrearConexion(
        bool permitirInternos, IReadOnlySet<string> hostsPermitidos)
    {
        return async (contexto, ct) =>
        {
            var host = contexto.DnsEndPoint.Host;
            var exento = permitirInternos || hostsPermitidos.Contains(host);
            var direcciones = await Dns.GetHostAddressesAsync(host, ct);

            var elegida = direcciones.FirstOrDefault(d => exento || !EsDireccionInterna(d));
            if (elegida is null || (!exento && direcciones.Any(EsDireccionInterna)))
                throw new HttpRequestException("Destino no permitido: la URL del aviso apunta a una dirección interna.");

            var socket = new Socket(elegida.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(elegida, contexto.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
    }
}
