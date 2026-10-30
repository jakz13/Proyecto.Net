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
        servicios.AddSingleton<ICuentaCorrienteApi, MockCuentaCorrienteApi>();
        servicios.AddSingleton<IEnviosApi, MockEnviosApi>();
        servicios.AddSingleton<ISeguimientoApi, MockSeguimientoApi>();
        servicios.AddSingleton<IRutasApi, MockRutasApi>();
        servicios.AddSingleton<IDespachoApi, MockDespachoApi>();
        servicios.AddSingleton<IDepositoApi, MockDepositoApi>();
        servicios.AddSingleton<IIncidenciasApi, MockIncidenciasApi>();
        servicios.AddSingleton<ITableroApi, MockTableroApi>();
        servicios.AddSingleton<IReportesApi, MockReportesApi>();
        servicios.AddSingleton<IAvisosComercioApi, MockAvisosComercioApi>();

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

        servicios.AddHttpClient<ISeguimientoApi, HttpSeguimientoApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IDespachoApi, HttpDespachoApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IEnviosApi, HttpEnviosApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IRutasApi, HttpRutasApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IIncidenciasApi, HttpIncidenciasApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IDepositoApi, HttpDepositoApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IAvisosComercioApi, HttpAvisosComercioApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<ITableroApi, HttpTableroApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<ICuentaCorrienteApi, HttpCuentaCorrienteApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });

        servicios.AddHttpClient<IReportesApi, HttpReportesApi>(cliente =>
        {
            cliente.BaseAddress = new Uri(urlBaseApi);
            cliente.Timeout = TimeSpan.FromSeconds(30);
        });

        return servicios;
    }
}
