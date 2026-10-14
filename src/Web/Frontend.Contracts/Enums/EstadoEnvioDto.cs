namespace UltimaMilla.Frontend.Contracts.Enums;

/// <summary>
/// Estados posibles de una orden de envío en la plataforma.
/// Mapea de forma directa con la máquina de estados del backend.
/// </summary>
public enum EstadoEnvioDto
{
    Admitido = 1,
    EnDeposito = 2,
    AsignadoARuta = 3,
    EnTransito = 4,
    Entregado = 5,
    NoEntregado = 6,
    Reprogramado = 7,
    EnDevolucion = 8,
    Devuelto = 9,
    Extraviado = 10
}
