using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class HojaDeRutaLiberacionTests
{
    private static HojaDeRuta NuevaRuta() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 16));

    [Fact]
    public void Liberar_ConRecursosYParadas_PasaALiberadaYRegistraFecha()
    {
        var ruta = NuevaRuta();
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());

        ruta.Liberar();

        Assert.Equal("Liberada", ruta.Estado);
        Assert.NotNull(ruta.FechaLiberacion);
    }

    [Fact]
    public void Liberar_SinRecursos_LanzaExcepcion()
    {
        var ruta = NuevaRuta();
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());

        Assert.Throws<DomainValidationException>(() => ruta.Liberar());
        Assert.Equal("Borrador", ruta.Estado);
    }

    [Fact]
    public void Liberar_SinParadas_LanzaExcepcion()
    {
        var ruta = NuevaRuta();
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainValidationException>(() => ruta.Liberar());
    }

    [Fact]
    public void Liberar_DosVeces_LanzaExcepcion()
    {
        var ruta = NuevaRuta();
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());
        ruta.Liberar();

        Assert.Throws<DomainValidationException>(() => ruta.Liberar());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ReglaOperativa_MaximoDeParadasNoPositivo_LanzaExcepcion(int maxParadas)
    {
        Assert.Throws<DomainValidationException>(() => new ReglaOperativa(
            Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow, 3, TimeSpan.FromHours(1),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24), null, maxParadas));
    }

    [Fact]
    public void ReglaOperativa_SinIndicarMaximo_UsaCincuentaParadas()
    {
        var regla = new ReglaOperativa(
            Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow, 3, TimeSpan.FromHours(1),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));

        Assert.Equal(50, regla.MaxParadasPorRuta);
    }

    [Fact]
    public void Envio_AsignarRuta_DesdeAdmitido_LanzaExcepcionYNoGuardaLaRuta()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ASIG0001");

        Assert.Throws<DomainValidationException>(() => envio.AsignarRuta(Guid.NewGuid()));
        Assert.Null(envio.HojaDeRutaId);
    }

    [Fact]
    public void Envio_ObtenerMotivoNoAsignable_SinDireccion_InformaElMotivo()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ASIG0002");
        envio.AgregarBulto("TRK-ASIG0002-B01", 1m, 1m, 1m, 1m);
        envio.RecepcionarEnDeposito(new[] { "TRK-ASIG0002-B01" }, null, null, Guid.NewGuid());
        envio.ClasificarBultos(new[] { new ClasificacionBulto("TRK-ASIG0002-B01", PrioridadBulto.Normal) }, Guid.NewGuid());

        Assert.Contains("dirección", envio.ObtenerMotivoNoAsignable());
    }

    [Fact]
    public void Envio_PesoTotal_NoCuentaLosBultosFaltantesEnRecepcion()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ASIG0003");
        envio.AgregarBulto("TRK-ASIG0003-B01", 4m, 1m, 1m, 1m);
        envio.AgregarBulto("TRK-ASIG0003-B02", 6m, 1m, 1m, 1m);
        envio.RecepcionarEnDeposito(new[] { "TRK-ASIG0003-B01" }, null, "Falta un bulto", Guid.NewGuid());

        Assert.Equal(4m, envio.PesoTotal);
    }
}
