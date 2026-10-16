using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Mantiene el operador actualmente seleccionado para la simulación en entorno de desarrollo (Demo).
/// Permite alternar entre dos operadores distintos para comprobar el cambio de identidad visual y aislamiento.
/// </summary>
public class MockContextoOperador
{
    private static readonly List<IdentidadVisualDto> OperadoresDisponibles = new()
    {
        new IdentidadVisualDto
        {
            OperadorId = "OP-01",
            NombreComercial = "TransLogística Express",
            ColorPrimario = "#1D4ED8", // Azul corporativo
            ColorSecundario = "#F97316", // Naranja acento
            LogoUrl = null,
            EmailContacto = "contacto@translogistica.uy",
            TelefonoContacto = "+598 2900 1122"
        },
        new IdentidadVisualDto
        {
            OperadorId = "OP-02",
            NombreComercial = "Veloz Cargo",
            ColorPrimario = "#047857", // Verde esmeralda
            ColorSecundario = "#F59E0B", // Ámbar acento
            LogoUrl = null,
            EmailContacto = "operaciones@velozcargo.uy",
            TelefonoContacto = "+598 2400 3344"
        }
    };

    private IdentidadVisualDto _operadorActivo = OperadoresDisponibles[0];

    public event Action? AlCambiarOperador;

    public IdentidadVisualDto OperadorActivo => _operadorActivo;

    public IReadOnlyList<IdentidadVisualDto> ListarOperadores() => OperadoresDisponibles.AsReadOnly();

    public void SeleccionarOperador(string operadorId)
    {
        var encontrado = OperadoresDisponibles.FirstOrDefault(o => o.OperadorId == operadorId);
        if (encontrado != null && encontrado.OperadorId != _operadorActivo.OperadorId)
        {
            _operadorActivo = encontrado;
            AlCambiarOperador?.Invoke();
        }
    }

    /// <summary>
    /// Actualiza la configuración de identidad visual del operador activo en memoria y notifica a las superficies vinculadas.
    /// </summary>
    public void ActualizarIdentidad(IdentidadVisualDto nuevaIdentidad)
    {
        var indice = OperadoresDisponibles.FindIndex(o => o.OperadorId == nuevaIdentidad.OperadorId);
        if (indice >= 0)
        {
            OperadoresDisponibles[indice] = nuevaIdentidad;
        }

        _operadorActivo = nuevaIdentidad;
        AlCambiarOperador?.Invoke();
    }
}
