using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>Banderas de funcionalidad por operador (solo el administrador de la plataforma las cambia).</summary>
public interface IFuncionalidadesApi
{
    Task<IReadOnlyList<OperadorFuncionalidadesDto>> ListarPorOperadorAsync(CancellationToken cancelacion = default);

    /// <summary>Funcionalidades del operador del usuario autenticado (clave → habilitada). Sin dato, se asume habilitada.</summary>
    Task<IReadOnlyDictionary<string, bool>> ObtenerDelOperadorAsync(CancellationToken cancelacion = default);

    Task CambiarAsync(Guid operadorId, string clave, bool habilitada, CancellationToken cancelacion = default);
}
