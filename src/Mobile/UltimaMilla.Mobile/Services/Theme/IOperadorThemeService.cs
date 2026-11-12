namespace UltimaMilla.Mobile.Services.Theme;

public class OperadorIdentidadVisualModel
{
    public Guid? OperadorId { get; set; }
    public string NombreComercial { get; set; } = "Última Milla";
    public string ColorPrimarioHex { get; set; } = "#2563EB";
    public string ColorSecundarioHex { get; set; } = "#64748B";
    public string? LogoUrl { get; set; }
    public string? TelefonoContacto { get; set; }
    public string? EmailContacto { get; set; }

    public string Iniciales
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NombreComercial))
                return "UM";

            var palabras = NombreComercial.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (palabras.Length == 1)
                return palabras[0].Length <= 2 ? palabras[0].ToUpperInvariant() : palabras[0][..2].ToUpperInvariant();

            return $"{char.ToUpperInvariant(palabras[0][0])}{char.ToUpperInvariant(palabras[1][0])}";
        }
    }
}

public interface IOperadorThemeService
{
    OperadorIdentidadVisualModel IdentidadActual { get; }
    event EventHandler<OperadorIdentidadVisualModel>? IdentidadVisualCambiada;

    void CargarDesdePreferencias();
    void AplicarIdentidad(OperadorIdentidadVisualModel identidad);
    Task<OperadorIdentidadVisualModel?> DescargarYActualizarIdentidadAsync(CancellationToken ct = default);
}

