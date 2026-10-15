using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada de la API de identidad visual para pruebas locales y modo demo.
/// </summary>
public class MockIdentidadVisualApi : IIdentidadVisualApi
{
    private readonly MockContextoOperador _contexto;

    public MockIdentidadVisualApi(MockContextoOperador contexto)
    {
        _contexto = contexto;
    }

    public Task<IdentidadVisualDto> ObtenerIdentidadActualAsync(CancellationToken cancelacion = default)
    {
        return Task.FromResult(_contexto.OperadorActivo);
    }

    public Task<IdentidadVisualDto> ObtenerPorOperadorDemoAsync(string operadorId, CancellationToken cancelacion = default)
    {
        var operador = _contexto.ListarOperadores().FirstOrDefault(o => o.OperadorId == operadorId)
                       ?? _contexto.OperadorActivo;
        return Task.FromResult(operador);
    }

    public Task<IReadOnlyList<IdentidadVisualDto>> ListarOperadoresDemoAsync(CancellationToken cancelacion = default)
    {
        return Task.FromResult(_contexto.ListarOperadores());
    }
}
