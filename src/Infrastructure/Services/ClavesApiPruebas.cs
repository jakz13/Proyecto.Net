using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>Clave de pruebas: <c>umt_{relación}_{HMAC-SHA256}</c>, firmada con el secreto de la plataforma.</summary>
public sealed class ClavesApiPruebas : IClavesApiPruebas
{
    private readonly byte[] _secreto;

    public ClavesApiPruebas(IConfiguration configuracion)
    {
        _secreto = Encoding.UTF8.GetBytes("api-pruebas:" + TokenJwtService.ClaveDeFirma(configuracion));
    }

    public string Generar(Guid comercioOperadorId) => $"{IClavesApiPruebas.Prefijo}{comercioOperadorId:N}_{Firma(comercioOperadorId)}";

    public Guid? Validar(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || !clave.StartsWith(IClavesApiPruebas.Prefijo, StringComparison.Ordinal))
            return null;

        var partes = clave[IClavesApiPruebas.Prefijo.Length..].Split('_');
        if (partes.Length != 2 || !Guid.TryParseExact(partes[0], "N", out var id))
            return null;

        var esperada = Encoding.UTF8.GetBytes(Firma(id));
        var recibida = Encoding.UTF8.GetBytes(partes[1]);
        return CryptographicOperations.FixedTimeEquals(esperada, recibida) ? id : null;
    }

    private string Firma(Guid id) =>
        Convert.ToHexString(HMACSHA256.HashData(_secreto, id.ToByteArray()))[..32].ToLowerInvariant();
}
