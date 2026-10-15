namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Almacenamiento en memoria para tokens JWT en Blazor WASM.
/// Contiene un token de demostración precargado para facilitar la defensa oral y pruebas locales sin fricción.
/// </summary>
public class TokenStoreEnMemoria : ITokenStore
{
    // JWT demo codificado en Base64Url:
    // Header: {"alg":"HS256","typ":"JWT"}
    // Payload: {"sub":"usr-com-01","name":"Distribuidora Central","email":"contacto@distribuidoracentral.uy","role":"Comercio","operador_id":"OP-01","comercio_id":"11111111-1111-1111-1111-111111111111"}
    private const string TokenPorDefecto = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                                            "eyJzdWIiOiJ1c3ItY29tLTAxIiwibmFtZSI6IkRpc3RyaWJ1aWRvcmEgQ2VudHJhbCIsImVtYWlsIjoiY29udGFjdG9AZGlzdHJpYnVpZG9yYWNlbnRyYWwudXkiLCJyb2xlIjoiQ29tZXJjaW8iLCJvcGVyYWRvcl9pZCI6Ik9QLTAxIiwiY29tZXJjaW9faWQiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEifQ." +
                                            "demo-firma-jwt";

    private string? _tokenActual = TokenPorDefecto;

    public Task<string?> ObtenerTokenAsync()
    {
        return Task.FromResult(_tokenActual);
    }

    public Task GuardarTokenAsync(string token)
    {
        _tokenActual = token;
        return Task.CompletedTask;
    }

    public Task EliminarTokenAsync()
    {
        _tokenActual = null;
        return Task.CompletedTask;
    }
}
