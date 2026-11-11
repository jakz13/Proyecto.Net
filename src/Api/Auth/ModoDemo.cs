namespace UltimaMilla.Api.Auth;

/// <summary>
/// El modo demostración (identidad sin sesión, listado de operadores sin login, receptor de pruebas) solo corresponde al desarrollo local.
/// Es "seguro por defecto": si <c>Auth:PermitirModoDemo</c> no está definido, solo se permite en el entorno Development, así que olvidar
/// una variable en un servidor nunca lo deja abierto. Para habilitarlo o apagarlo a la fuerza se define explícitamente.
/// </summary>
public static class ModoDemo
{
    public static bool Permitido(IConfiguration configuracion, IHostEnvironment entorno) =>
        configuracion.GetValue<bool?>("Auth:PermitirModoDemo") ?? entorno.IsDevelopment();
}
