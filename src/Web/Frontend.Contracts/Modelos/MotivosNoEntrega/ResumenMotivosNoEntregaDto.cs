namespace UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

/// <summary>
/// Métricas globales del catálogo de motivos de no entrega y estado de sincronización (UC-05, RF-21).
/// </summary>
public class ResumenMotivosNoEntregaDto
{
    public int TotalMotivos { get; set; }
    public int MotivosActivosParaMovil { get; set; }
    public int MotivosInactivos { get; set; }
    public DateTime UltimaSincronizacionApp { get; set; }
}
