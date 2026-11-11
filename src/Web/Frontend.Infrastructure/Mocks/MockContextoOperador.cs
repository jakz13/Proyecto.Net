using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Mantiene el operador actualmente seleccionado para la simulación en entorno de desarrollo (Demo).
/// Permite alternar entre dos operadores distintos para comprobar el cambio de identidad visual y aislamiento.
/// Sincroniza el estado mediante un archivo temporal compartido para que cambios hechos en Backoffice
/// se reflejen en Portal del Comercio y Seguimiento Público aun ejecutándose en procesos independientes.
/// </summary>
public class MockContextoOperador
{
    private static readonly string RutaArchivoSincronizacion = Path.Combine(Path.GetTempPath(), "ultimamilla_identidades_demo.json");
    private static DateTime _ultimaLecturaArchivo = DateTime.MinValue;

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

    private IdentidadVisualDto _operadorActivo;

    public MockContextoOperador()
    {
        SincronizarDesdeArchivoSiCorresponde();
        _operadorActivo = OperadoresDisponibles[0];
    }

    public event Action? AlCambiarOperador;

    public IdentidadVisualDto OperadorActivo
    {
        get
        {
            SincronizarDesdeArchivoSiCorresponde();
            return _operadorActivo;
        }
    }

    public IReadOnlyList<IdentidadVisualDto> ListarOperadores()
    {
        SincronizarDesdeArchivoSiCorresponde();
        return OperadoresDisponibles.AsReadOnly();
    }

    public void SeleccionarOperador(string operadorId)
    {
        SincronizarDesdeArchivoSiCorresponde();
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
        else
        {
            OperadoresDisponibles[0] = nuevaIdentidad;
        }

        _operadorActivo = nuevaIdentidad;
        GuardarEnArchivo();
        AlCambiarOperador?.Invoke();
    }

    private void SincronizarDesdeArchivoSiCorresponde()
    {
        try
        {
            if (!File.Exists(RutaArchivoSincronizacion))
                return;

            var ultimaModificacion = File.GetLastWriteTimeUtc(RutaArchivoSincronizacion);
            if (ultimaModificacion <= _ultimaLecturaArchivo)
                return;

            var json = File.ReadAllText(RutaArchivoSincronizacion);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var lista = JsonSerializer.Deserialize<List<IdentidadVisualDto>>(json);
            if (lista is { Count: > 0 })
            {
                OperadoresDisponibles.Clear();
                OperadoresDisponibles.AddRange(lista);

                var activo = OperadoresDisponibles.FirstOrDefault(o => o.OperadorId == _operadorActivo?.OperadorId)
                             ?? OperadoresDisponibles[0];
                _operadorActivo = activo;
                _ultimaLecturaArchivo = ultimaModificacion;
            }
        }
        catch
        {
            // Failsafe: mantener datos en memoria si hay contención de archivo concurrente
        }
    }

    private static void GuardarEnArchivo()
    {
        try
        {
            var json = JsonSerializer.Serialize(OperadoresDisponibles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(RutaArchivoSincronizacion, json);
            _ultimaLecturaArchivo = DateTime.UtcNow;
        }
        catch
        {
            // Failsafe: no bloquear si el archivo está siendo leído
        }
    }
}
