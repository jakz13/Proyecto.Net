using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Tests;

public class SeedingDomainTests
{
    private static readonly Guid OperadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ComercioOperadorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid HojaDeRutaId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid Parada1Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000001");
    private static readonly Guid Envio1Id = Guid.Parse("cccccccc-1111-2222-3333-000000000001");

    [Fact]
    public void EnvioSeeder_ConCodigoPersonalizadoYBultos_QuedaEnTransitoCorrectamente()
    {
        // Arrange
        var envio = new Envio(
            Envio1Id,
            ComercioOperadorId,
            "Av. 18 de Julio 1450, Centro",
            "Martín Rodríguez",
            "ENV-2026-00108"
        );
        envio.AgregarBulto("BLT-001-A", 1.2m);
        envio.AgregarBulto("BLT-001-B", 1.5m);

        // Act
        envio.AsignarRuta(HojaDeRutaId);
        envio.DespacharEnRuta(HojaDeRutaId, Guid.NewGuid(), "Despacho demo");

        // Assert
        Assert.Equal("ENV-2026-00108", envio.CodigoSeguimientoPublico);
        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
        Assert.Equal(HojaDeRutaId, envio.HojaDeRutaId);
        Assert.Equal(2, envio.Bultos.Count);
        Assert.All(envio.Bultos, b => Assert.Equal("CargadoEnVehiculo", b.EstadoBulto));
    }

    [Fact]
    public void HojaDeRutaSeeder_ConParadaEspecifica_PermiteTransicionAEntregaExitosa()
    {
        // Arrange
        var hojaDeRuta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnTransito");
        hojaDeRuta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        hojaDeRuta.AgregarParada(Parada1Id, Envio1Id, 1, Guid.NewGuid(), Guid.NewGuid(), "Pendiente");

        var envio = new Envio(Envio1Id, ComercioOperadorId, "Av. 18 de Julio 1450", "Martín Rodríguez", "ENV-2026-00108");
        envio.AsignarRuta(HojaDeRutaId);
        envio.DespacharEnRuta(HojaDeRutaId);

        var reglaOperativa = new ReglaOperativa(
            Guid.NewGuid(),
            OperadorId,
            1,
            DateTime.UtcNow.AddYears(-1),
            maxIntentosEntrega: 3,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: "Firma digital y documento del receptor",
            politicaDevolucion: "Retorno a depósito central",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24)
        );

        // Act - Simulación de UC-20 (Entrega exitosa)
        envio.RegistrarEntregaExitosa(
            Parada1Id,
            reglaOperativa,
            nombreReceptor: "Martín Rodríguez",
            documentoReceptor: "4.321.987-6",
            firmaDigitalBase64: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
            fotoEvidenciaBase64: null,
            latitud: -34.9056,
            longitud: -56.1852,
            fechaHoraDispositivoUtc: DateTime.UtcNow
        );
        var parada = hojaDeRuta.RegistrarEntregaExitosaEnParada(Parada1Id);

        // Assert
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal("Entregada", parada.Estado);
        Assert.Single(envio.IntentosEntrega);
        Assert.Equal("Exitoso", envio.IntentosEntrega.First().Resultado);
    }

    [Fact]
    public void HojaDeRutaSeeder_ConParadaEspecifica_PermiteTransicionAIntentoFallido()
    {
        // Arrange
        var hojaDeRuta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnTransito");
        hojaDeRuta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        hojaDeRuta.AgregarParada(Parada1Id, Envio1Id, 1, Guid.NewGuid(), Guid.NewGuid(), "Pendiente");

        var envio = new Envio(Envio1Id, ComercioOperadorId, "Av. 18 de Julio 1450", "Martín Rodríguez", "ENV-2026-00108");
        envio.AsignarRuta(HojaDeRutaId);
        envio.DespacharEnRuta(HojaDeRutaId);

        var reglaOperativa = new ReglaOperativa(
            Guid.NewGuid(),
            OperadorId,
            1,
            DateTime.UtcNow.AddYears(-1),
            maxIntentosEntrega: 3,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: "Firma digital y documento del receptor",
            politicaDevolucion: "Retorno a depósito central",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24)
        );

        var motivo = new MotivoNoEntrega(
            Guid.Parse("dddddddd-0001-0000-0000-000000000001"),
            OperadorId,
            "CLI_AUSENTE",
            "Cliente ausente / No atiende llamadas"
        );

        // Act - Simulación de UC-21 (Intento fallido)
        var (intento, estadoFinal, intentosRestantes) = envio.RegistrarIntentoFallido(
            Parada1Id,
            motivo,
            reglaOperativa,
            observaciones: "No atiende el portero eléctrico ni responde el celular",
            fotoEvidenciaBase64: null,
            latitud: -34.9056,
            longitud: -56.1852,
            fechaHoraDispositivoUtc: DateTime.UtcNow
        );
        var parada = hojaDeRuta.RegistrarIntentoFallidoEnParada(Parada1Id);

        // Assert
        Assert.Equal(EstadoEnvio.Reprogramado, estadoFinal);
        Assert.Equal(EstadoEnvio.Reprogramado, envio.EstadoActual);
        Assert.Equal(2, intentosRestantes);
        Assert.Equal("Fallida", parada.Estado);
        Assert.Equal("Fallido", intento.Resultado);
    }
}
