using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Tests;

/// <summary>Cada intento de entrega registra con qué versión de la regla operativa se procesó.</summary>
public class TrazabilidadReglaOperativaTests
{
    private static ReglaOperativa Regla(int version) =>
        new(Guid.NewGuid(), Guid.NewGuid(), version, DateTime.UtcNow.AddDays(-1),
            maxIntentosEntrega: 3,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: "Firma",
            politicaDevolucion: "Retorno a depósito",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24));

    private static Envio EnvioEnTransito()
    {
        var usuarioId = Guid.NewGuid();
        var zonaId = Guid.NewGuid();
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "ENV-TRAZA-001",
            direccionEntregaId: Guid.NewGuid(), direccionDestino: "Av. 18 de Julio 1234",
            nombreDestinatario: "María Rodríguez", operadorId: Guid.NewGuid());
        envio.AgregarBulto("BUL-001", 4m, 20, 20, 20);
        envio.AplicarTarifa(200m, Guid.NewGuid());
        envio.RecepcionarEnDeposito(new[] { "BUL-001" }, null, null, usuarioId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BUL-001", PrioridadBulto.Normal, zonaId, "Paquete") }, usuarioId);
        var rutaId = Guid.NewGuid();
        envio.AsignarRuta(rutaId, usuarioId);
        envio.DespacharEnRuta(rutaId, usuarioId);
        return envio;
    }

    [Fact]
    public void IntentoFallidoYLuegoExitoso_CadaIntentoConservaLaReglaConLaQueSeProceso()
    {
        var envio = EnvioEnTransito();
        var reglaV1 = Regla(1);
        var reglaV2 = Regla(2); // el operador publicó otra versión entre los dos intentos
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "AUSENTE", "Destinatario ausente", true);

        var fallido = envio.RegistrarIntentoFallido(Guid.NewGuid(), motivo, reglaV1, "Nadie atendió", null, null, null, DateTime.UtcNow);
        Assert.Equal(reglaV1.Id, fallido.Intento.ReglaOperativaId);

        var rutaNueva = Guid.NewGuid();
        envio.AsignarRuta(rutaNueva, Guid.NewGuid());
        envio.DespacharEnRuta(rutaNueva, Guid.NewGuid());
        var exitoso = envio.RegistrarEntregaExitosa(Guid.NewGuid(), reglaV2, "María", "41112223", "firma", null, null, null, DateTime.UtcNow);

        Assert.Equal(reglaV2.Id, exitoso.ReglaOperativaId);
        Assert.Equal(reglaV1.Id, envio.IntentosEntrega.First(i => i.Resultado == "Fallido").ReglaOperativaId);
    }

    [Fact]
    public void ReglaPorDefecto_NoSeReferenciaPorqueNoExisteEnLaBase()
    {
        var envio = EnvioEnTransito();
        var porDefecto = ReglaOperativa.PorDefecto(envio.OperadorId, DateTime.UtcNow);

        var intento = envio.RegistrarEntregaExitosa(Guid.NewGuid(), porDefecto, "María", "41112223", "firma", null, null, null, DateTime.UtcNow);

        Assert.True(porDefecto.EsPorDefecto);
        Assert.Null(intento.ReglaOperativaId);
    }
}
