using System.Text.Json;
using UltimaMilla.Application.Events;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Tests;

public class EventosIntegracionTests
{
    [Fact]
    public void DeEnvio_ArmaElSobreComunConLosDatosDelEnvio()
    {
        var operadorId = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var envio = new Envio(Guid.NewGuid(), comercioId, Guid.NewGuid(), "Estandar", "TRK-EVENTO", operadorId: operadorId);

        var mensaje = EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioAdmitido, envio, new { Tarifa = 120m });

        Assert.Equal("Envio", mensaje.AgregadoOrigen);
        Assert.Equal(envio.Id, mensaje.AgregadoId);
        Assert.Equal(EventosIntegracion.Tipos.EnvioAdmitido, mensaje.TipoEvento);
        Assert.False(mensaje.Procesado);

        var json = JsonDocument.Parse(mensaje.Payload).RootElement;
        Assert.Equal(EventosIntegracion.VersionContrato, json.GetProperty("Version").GetInt32());
        Assert.Equal(operadorId, json.GetProperty("OperadorId").GetGuid());
        Assert.Equal(comercioId, json.GetProperty("ComercioId").GetGuid());
        Assert.Equal(envio.Id, json.GetProperty("EnvioId").GetGuid());
        Assert.Equal("TRK-EVENTO", json.GetProperty("CodigoSeguimiento").GetString());
        Assert.Equal("Admitido", json.GetProperty("Estado").GetString());
        Assert.Equal(120m, json.GetProperty("Detalle").GetProperty("Tarifa").GetDecimal());
    }

    [Fact]
    public void DeRuta_IncluyeOperadorYEstadoDeLaRuta()
    {
        var operadorId = Guid.NewGuid();
        var ruta = new HojaDeRuta(Guid.NewGuid(), operadorId, DateOnly.FromDateTime(DateTime.UtcNow));

        var mensaje = EventosIntegracion.DeRuta(EventosIntegracion.Tipos.RutaArmada, ruta);

        Assert.Equal("HojaDeRuta", mensaje.AgregadoOrigen);
        var json = JsonDocument.Parse(mensaje.Payload).RootElement;
        Assert.Equal(operadorId, json.GetProperty("OperadorId").GetGuid());
        Assert.Equal(ruta.Id, json.GetProperty("HojaDeRutaId").GetGuid());
        Assert.Equal(ruta.Estado, json.GetProperty("Estado").GetString());
    }

    [Fact]
    public void LosTiposDeEvento_SonUnicosYTerminanEnIntegrationEvent()
    {
        var tipos = typeof(EventosIntegracion.Tipos)
            .GetFields()
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(tipos);
        Assert.Equal(tipos.Count, tipos.Distinct().Count());
        Assert.All(tipos, t => Assert.EndsWith("IntegrationEvent", t));
    }
}
