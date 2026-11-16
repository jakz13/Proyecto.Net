using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("HojasDeRuta")]
public class HojaDeRutaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public DateTime Fecha { get; set; } = DateTime.Today;

    public string Estado { get; set; } = "EnTransito";

    public string NumeroRuta { get; set; } = string.Empty;

    public int TotalParadas { get; set; }

    public string? PatenteVehiculo { get; set; }

    public DateTime FechaDescargaUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Elemento visual del selector corporativo de hoja de ruta activa.</summary>
public partial class HojaDeRutaSelectorItem : ObservableObject
{
    public Guid Id { get; }
    public string NumeroRuta { get; }
    public string Estado { get; }
    public int TotalParadas { get; }
    public string TituloSelector { get; }
    public string FechaTexto { get; }
    public string EstadoDescripcion { get; }
    public int ParadasPendientes { get; }
    public int ParadasCompletadas { get; }
    public string? VehiculoPatente { get; }

    [ObservableProperty]
    private bool _esSeleccionada;

    public string ResumenParadasModalTexto =>
        $"{ParadasCompletadas} completadas · {ParadasPendientes} pendientes ({TotalParadas} total)";

    public string VehiculoTexto =>
        string.IsNullOrWhiteSpace(VehiculoPatente) ? "Vehículo asignado pendiente" : $"Patente {VehiculoPatente}";

    public string ResumenSubtituloCard =>
        $"{EstadoDescripcion} · {FechaTexto} · {TotalParadas} paradas";

    public string EstadoBgColor => Estado switch
    {
        "EnTransito" => "#EFF6FF",
        "Despachada" => "#ECFEFF",
        "EnCarga" => "#FEF3C7",
        "Liberada" => "#F1F5F9",
        "Finalizada" => "#DCFCE7",
        "Rendida" or "Cerrada" => "#E2E8F0",
        _ => "#F1F5F9"
    };

    public string EstadoBorderColor => Estado switch
    {
        "EnTransito" => "#93C5FD",
        "Despachada" => "#67E8F9",
        "EnCarga" => "#FCD34D",
        "Liberada" => "#CBD5E1",
        "Finalizada" => "#86EFAC",
        "Rendida" or "Cerrada" => "#CBD5E1",
        _ => "#CBD5E1"
    };

    public string EstadoTextColor => Estado switch
    {
        "EnTransito" => "#1D4ED8",
        "Despachada" => "#0E7490",
        "EnCarga" => "#B45309",
        "Liberada" => "#475569",
        "Finalizada" => "#15803D",
        "Rendida" or "Cerrada" => "#475569",
        _ => "#334155"
    };

    public HojaDeRutaSelectorItem(
        Guid id,
        string numeroRuta,
        string estado,
        int totalParadas,
        string tituloSelector,
        string fechaTexto = "",
        string estadoDescripcion = "",
        int paradasPendientes = 0,
        int paradasCompletadas = 0,
        string? vehiculoPatente = null,
        bool esSeleccionada = false)
    {
        Id = id;
        NumeroRuta = numeroRuta;
        Estado = estado;
        TotalParadas = totalParadas;
        TituloSelector = tituloSelector;
        FechaTexto = fechaTexto;
        EstadoDescripcion = string.IsNullOrWhiteSpace(estadoDescripcion) ? DescribirEstado(estado) : estadoDescripcion;
        ParadasPendientes = paradasPendientes;
        ParadasCompletadas = paradasCompletadas;
        VehiculoPatente = vehiculoPatente;
        _esSeleccionada = esSeleccionada;
    }

    public static string DescribirEstado(string estado) => estado switch
    {
        "EnTransito" => "En Tránsito",
        "Despachada" => "Despachada",
        "EnCarga" => "En Carga",
        "Liberada" => "Liberada",
        "Finalizada" => "Completada",
        "Rendida" => "Rendida en depósito",
        "Cerrada" => "Cerrada",
        _ => estado
    };

    public static HojaDeRutaSelectorItem Crear(
        HojaDeRutaLocal ruta,
        IReadOnlyCollection<ParadaLocal>? paradas = null,
        bool esSeleccionada = false)
    {
        var total = paradas is { Count: > 0 } ? paradas.Count : ruta.TotalParadas;
        var completadas = paradas?.Count(p => p.Estado is "Entregado" or "Fallido" or "EnDevolucion") ?? 0;
        var pendientes = Math.Max(0, total - completadas);
        var descEstado = DescribirEstado(ruta.Estado);

        return new HojaDeRutaSelectorItem(
            id: ruta.Id,
            numeroRuta: ruta.NumeroRuta,
            estado: ruta.Estado,
            totalParadas: total,
            tituloSelector: $"{ruta.NumeroRuta} - {descEstado} ({total} paradas)",
            fechaTexto: ruta.Fecha.ToString("dd/MM/yyyy"),
            estadoDescripcion: descEstado,
            paradasPendientes: pendientes,
            paradasCompletadas: completadas,
            vehiculoPatente: ruta.PatenteVehiculo,
            esSeleccionada: esSeleccionada);
    }

    public static void ActualizarSeleccion(IEnumerable<HojaDeRutaSelectorItem> items, Guid? seleccionadaId)
    {
        foreach (var item in items)
        {
            item.EsSeleccionada = seleccionadaId.HasValue && item.Id == seleccionadaId.Value;
        }
    }
}

/// <summary>Elemento genérico seleccionable para modales y chips corporativos.</summary>
public partial class OpcionSeleccionableItem : ObservableObject
{
    public string Texto { get; }
    public string? Subtitulo { get; }

    [ObservableProperty]
    private bool _esSeleccionada;

    public OpcionSeleccionableItem(string texto, bool esSeleccionada = false, string? subtitulo = null)
    {
        Texto = texto;
        _esSeleccionada = esSeleccionada;
        Subtitulo = subtitulo;
    }

    public bool TieneSubtitulo => !string.IsNullOrWhiteSpace(Subtitulo);
}
