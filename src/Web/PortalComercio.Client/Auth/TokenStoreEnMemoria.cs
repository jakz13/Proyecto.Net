namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Almacenamiento en memoria para tokens JWT en Blazor WASM.
/// Contiene un token de demostración precargado sincronizado con el comercio sembrado (MegaStore Uruguay S.R.L.).
/// </summary>
public class TokenStoreEnMemoria : ITokenStore
{
    // JWT demo codificado en Base64Url sincronizado con DatabaseSeeder:
    // Header: {"alg":"HS256","typ":"JWT"}
    // Payload: {"sub":"77777777-7777-7777-7777-777777777777","name":"MegaStore Uruguay S.R.L.","email":"envios@megastore.com.uy","role":"Comercio","operador_id":"11111111-1111-1111-1111-111111111111","comercio_id":"33333333-3333-3333-3333-333333333333"}
    private const string TokenPorDefecto = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                                            "eyJzdWIiOiI3Nzc3Nzc3Ny03Nzc3LTc3NzctNzc3Ny03Nzc3Nzc3Nzc3NzciLCJuYW1lIjoiTWVnYVN0b3JlIFVydWd1YXkgUy5SLkwuIiwiZW1haWwiOiJlbnZpb3NAbWVnYXN0b3JlLmNvbS51eSIsInJvbGUiOiJDb21lcmNpbyIsIm9wZXJhZG9yX2lkIjoiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiY29tZXJjaW9faWQiOiIzMzMzMzMzMy0zMzMzLTMzMzMtMzMzMy0zMzMzMzMzMzMzMzMifQ." +
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
