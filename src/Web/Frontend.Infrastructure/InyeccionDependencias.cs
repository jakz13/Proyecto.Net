using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Frontend.Infrastructure.Mocks;

namespace UltimaMilla.Frontend.Infrastructure;

/// <summary>
/// Métodos de extensión para registrar las implementaciones simuladas o reales en el contenedor de dependencias.
/// Permite alternar entre mocks y API HTTP real con una simple configuración booleana.
/// </summary>
public static class InyeccionDependencias
{
    /// <summary>
    /// Registra las implementaciones simuladas (Mocks) en memoria para desarrollo local y demo oral sin requerir backend.
    /// </summary>
    public static IServiceCollection AgregarMocksFrontend(this IServiceCollection servicios)
    {
        servicios.AddSingleton<MockContextoOperador>();
        servicios.AddSingleton<IIdentidadVisualApi, MockIdentidadVisualApi>();
        servicios.AddSingleton<IComerciosApi, MockComerciosApi>();
        servicios.AddSingleton<IUsuariosApi, MockUsuariosApi>();
        servicios.AddSingleton<IZonasCoberturaApi, MockZonasCoberturaApi>();
        servicios.AddSingleton<ICuadroTarifarioApi, MockCuadroTarifarioApi>();
        servicios.AddSingleton<IReglasOperativasApi, MockReglasOperativasApi>();
        servicios.AddSingleton<IMotivosNoEntregaApi, MockMotivosNoEntregaApi>();
        servicios.AddSingleton<IFlotaApi, MockFlotaApi>();
        servicios.AddSingleton<IMensajeriaApi, MockMensajeriaApi>();
        servicios.AddSingleton<IEnviosApi, MockEnviosApi>();
        servicios.AddSingleton<ISeguimientoApi, MockSeguimientoApi>();

        return servicios;
    }

    /// <summary>
    /// Registra los clientes HTTP reales para conectar directamente el frontend con la API backend.
    /// </summary>
    public static IServiceCollection AgregarHttpClientsFrontend(this IServiceCollection servicios, string urlBaseApi)
    {
        servicios.AddSingleton<MockContextoOperador>();

        servicios.AddHttpClient<IComerciosApi, HttpComerciosApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IUsuariosApi, HttpUsuariosApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IZonasCoberturaApi, HttpZonasCoberturaApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<ICuadroTarifarioApi, HttpCuadroTarifarioApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IReglasOperativasApi, HttpReglasOperativasApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IMotivosNoEntregaApi, HttpMotivosNoEntregaApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IIdentidadVisualApi, HttpIdentidadVisualApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IFlotaApi, HttpFlotaApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IMensajeriaApi, HttpMensajeriaApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        // En caso de que se necesiten mantener mocks auxiliares mientras el backend finaliza otros endpoints:
        servicios.AddSingleton<IEnviosApi, MockEnviosApi>();
        servicios.AddSingleton<ISeguimientoApi, MockSeguimientoApi>();

        return servicios;
    }
}
