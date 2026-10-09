using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

public class ModeloDominioTests
{
    [Fact]
    public void Usuario_ConDatosValidos_SeCreaActivo()
    {
        var id = Guid.NewGuid();
        var perfilId = Guid.NewGuid();
        var usuario = new Usuario(id, "Carlos Logística", "carlos@operador.com", "hash123", perfilId);

        Assert.Equal(id, usuario.Id);
        Assert.True(usuario.Activo);
        Assert.Equal("carlos@operador.com", usuario.Email);

        usuario.Desactivar();
        Assert.False(usuario.Activo);
    }

    [Fact]
    public void Perfil_PermiteAsignarYRemoverPermisos()
    {
        var perfil = new Perfil(Guid.NewGuid(), "OperadorDepósito", "Perfil para operarios de depósito");
        var permisoId = Guid.NewGuid();

        perfil.AsignarPermiso(permisoId);
        Assert.Single(perfil.Permisos);

        perfil.RemoverPermiso(permisoId);
        Assert.Empty(perfil.Permisos);
    }

    [Fact]
    public void CuentaCorriente_RegistrarMovimiento_ActualizaSaldoYGeneraSello()
    {
        var id = Guid.NewGuid();
        var comercioOperadorId = Guid.NewGuid();
        var cc = new CuentaCorriente(id, comercioOperadorId, "UYU", saldoInicial: 1000m);
        var selloInicial = cc.SelloModificacion;

        cc.RegistrarMovimiento("Cobro de envío", 250m);

        Assert.Equal(1250m, cc.Saldo);
        Assert.Single(cc.Movimientos);
        Assert.NotEqual(selloInicial, cc.SelloModificacion);
    }

    [Fact]
    public void Envio_TransicionarEstado_AgregaEventoYActualizaSello()
    {
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Express",
            "TRK-98765432");

        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Single(envio.Eventos);

        var selloAnterior = envio.SelloModificacion;
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnHub");

        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);
        Assert.Equal(2, envio.Eventos.Count);
        Assert.NotEqual(selloAnterior, envio.SelloModificacion);
    }

    [Fact]
    public void HojaDeRuta_DespacharSinRecursos_LanzaDomainValidationException()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today));

        Assert.Throws<DomainValidationException>(() => ruta.Despachar());
    }

    [Fact]
    public void HojaDeRuta_ConRecursosYParada_DespachaExitosamente()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());

        ruta.Despachar();

        Assert.Equal("Despachada", ruta.Estado);
        Assert.NotNull(ruta.FechaDespacho);
    }
}
