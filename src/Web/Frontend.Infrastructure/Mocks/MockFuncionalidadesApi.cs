using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>Implementación en memoria para el modo de demostración sin backend.</summary>
public class MockFuncionalidadesApi : IFuncionalidadesApi
{
    private readonly Dictionary<(Guid, string), bool> _estado = new();
    private static readonly Guid OperadorDemo = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly (string Clave, string Nombre)[] Catalogo =
    {
        ("retiro-en-domicilio", "Retiro en domicilio"),
        ("entrega-parcial", "Entrega parcial por bultos")
    };

    public Task<IReadOnlyList<OperadorFuncionalidadesDto>> ListarPorOperadorAsync(CancellationToken cancelacion = default)
    {
        IReadOnlyList<OperadorFuncionalidadesDto> lista = new List<OperadorFuncionalidadesDto>
        {
            new(OperadorDemo, "Operador de demostración",
                Catalogo.Select(c => new FuncionalidadDto(c.Clave, c.Nombre, _estado.GetValueOrDefault((OperadorDemo, c.Clave), true), null, null)).ToList())
        };
        return Task.FromResult(lista);
    }

    public Task CambiarAsync(Guid operadorId, string clave, bool habilitada, CancellationToken cancelacion = default)
    {
        _estado[(operadorId, clave)] = habilitada;
        return Task.CompletedTask;
    }
}
