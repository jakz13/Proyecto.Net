namespace UltimaMilla.Domain.Enums;

/// <summary>
/// Estados del ciclo de vida de un retiro en domicilio. El flujo comienza en el comprador y termina en el comercio,
/// a diferencia del envío, que va del comercio al destinatario.
/// </summary>
public enum EstadoRetiro
{
    Solicitado = 1,
    AsignadoARuta = 2,
    Retirado = 3,
    NoRetirado = 4,
    EnDeposito = 5,
    EntregadoAlComercio = 6,
    Cancelado = 99
}
