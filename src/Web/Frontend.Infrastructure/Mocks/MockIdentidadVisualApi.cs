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

    public Task<IdentidadVisualDto> ActualizarIdentidadVisualAsync(ActualizarIdentidadVisualDto dto, CancellationToken cancelacion = default)
    {
        var actual = _contexto.OperadorActivo;
        var actualizada = actual with
        {
            NombreComercial = string.IsNullOrWhiteSpace(dto.NombreComercial) ? actual.NombreComercial : dto.NombreComercial.Trim(),
            ColorPrimario = dto.ColorPrimario.Trim().ToUpperInvariant(),
            ColorSecundario = dto.ColorSecundario.Trim().ToUpperInvariant(),
            LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim(),
            EmailContacto = dto.EmailContacto.Trim().ToLowerInvariant(),
            TelefonoContacto = dto.TelefonoContacto.Trim(),
            SitioWeb = string.IsNullOrWhiteSpace(dto.SitioWeb) ? null : dto.SitioWeb.Trim(),
            MensajeComunicaciones = string.IsNullOrWhiteSpace(dto.MensajeComunicaciones) ? null : dto.MensajeComunicaciones.Trim()
        };

        _contexto.ActualizarIdentidad(actualizada);
        return Task.FromResult(actualizada);
    }
}
