namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Cortacircuitos del caché distribuido: tras un fallo de Redis, durante unos segundos no se lo vuelve a intentar. Sin esto, cada
/// solicitud espera el tiempo de espera completo del almacén caído (medido: una solicitud tardó 19 s con Redis detenido) en vez de
/// ir directo a la base de datos. Se registra como singleton para que lo compartan todas las solicitudes del proceso.
/// </summary>
public sealed class CortacircuitosCache
{
    private readonly TimeSpan _pausa;
    private long _noDisponibleHastaTicks;

    public CortacircuitosCache(TimeSpan? pausa = null) => _pausa = pausa ?? TimeSpan.FromSeconds(10);

    public bool EnPausa => DateTime.UtcNow.Ticks < Interlocked.Read(ref _noDisponibleHastaTicks);

    public void Pausar() => Interlocked.Exchange(ref _noDisponibleHastaTicks, (DateTime.UtcNow + _pausa).Ticks);
}
