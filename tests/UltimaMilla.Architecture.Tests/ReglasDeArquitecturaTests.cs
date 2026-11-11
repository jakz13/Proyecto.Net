using System.Reflection;
using MediatR;
using NetArchTest.Rules;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// Pruebas de arquitectura (letra 6.1). Verifican de forma automática, en cada ejecución del pipeline, la regla de dependencias de la
/// arquitectura por capas del ADR 01: el dominio no depende de nada externo; la aplicación solo depende del dominio y define puertos;
/// la infraestructura implementa esos puertos; la API y el worker son la capa de entrada. Si alguien viola la regla, el pipeline falla.
/// </summary>
public class ReglasDeArquitecturaTests
{
    private static readonly Assembly Dominio = typeof(Envio).Assembly;
    private static readonly Assembly Aplicacion = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly Infraestructura = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    /// <summary>Frameworks, acceso a datos y servicios externos de los que el dominio y la aplicación no deben depender.</summary>
    private static readonly string[] Tecnologia =
    {
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "RabbitMQ", "StackExchange.Redis",
        "Microsoft.Extensions.Caching", "Serilog", "OpenTelemetry", "System.Data", "System.Net.Http"
    };

    private static string Detalle(TestResult resultado) =>
        resultado.FailingTypeNames is null ? string.Empty : "Tipos que violan la regla: " + string.Join(", ", resultado.FailingTypeNames);

    [Fact]
    public void ElDominio_NoDependeDeLasDemasCapas()
    {
        var resultado = Types.InAssembly(Dominio)
            .ShouldNot()
            .HaveDependencyOnAny("UltimaMilla.Application", "UltimaMilla.Infrastructure", "UltimaMilla.Api", "UltimaMilla.Worker", "UltimaMilla.Frontend")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Detalle(resultado));
    }

    [Fact]
    public void ElDominio_NoDependeDeFrameworksNiDeInfraestructuraTecnica()
    {
        var resultado = Types.InAssembly(Dominio)
            .ShouldNot()
            .HaveDependencyOnAny(Tecnologia)
            .GetResult();

        Assert.True(resultado.IsSuccessful, Detalle(resultado));
    }

    [Fact]
    public void LaAplicacion_SoloDependeDelDominio_NoDeInfraestructuraNiDeLaCapaDeEntrada()
    {
        var resultado = Types.InAssembly(Aplicacion)
            .ShouldNot()
            .HaveDependencyOnAny("UltimaMilla.Infrastructure", "UltimaMilla.Api", "UltimaMilla.Worker")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Detalle(resultado));
    }

    [Fact]
    public void LaAplicacion_NoDependeDeAccesoADatosNiDeFrameworksWeb()
    {
        var resultado = Types.InAssembly(Aplicacion)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "RabbitMQ", "StackExchange.Redis")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Detalle(resultado));
    }

    [Fact]
    public void LaInfraestructura_NoDependeDeLaCapaDeEntrada()
    {
        var resultado = Types.InAssembly(Infraestructura)
            .ShouldNot()
            .HaveDependencyOnAny("UltimaMilla.Api", "UltimaMilla.Worker")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Detalle(resultado));
    }

    [Fact]
    public void ElEnsamblado_DelDominioNoReferenciaOtrosProyectosDeLaSolucion()
    {
        // Segunda comprobación, independiente de los espacios de nombres: lo que el ensamblado referencia de verdad
        var referenciasPropias = Dominio.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("UltimaMilla.", StringComparison.Ordinal)).ToList();

        Assert.Empty(referenciasPropias);
    }

    [Fact]
    public void ElEnsamblado_DeLaAplicacionSoloReferenciaAlDominio()
    {
        var referenciasPropias = Aplicacion.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("UltimaMilla.", StringComparison.Ordinal))
            .Select(a => a.Name)
            .ToList();

        Assert.All(referenciasPropias, nombre => Assert.Equal(Dominio.GetName().Name, nombre));
    }

    [Fact]
    public void LosCasosDeUso_VivenEnLaCapaDeAplicacion()
    {
        // Todo manejador de comando o consulta (MediatR) debe estar en la aplicación, no en la API ni en la infraestructura
        var fueraDeLugar = new[] { Infraestructura, Api }
            .SelectMany(a => Types.InAssembly(a).That().ImplementInterface(typeof(IRequestHandler<,>)).GetTypes())
            .Select(t => t.FullName)
            .ToList();

        Assert.True(fueraDeLugar.Count == 0, "Casos de uso fuera de la capa de aplicación: " + string.Join(", ", fueraDeLugar));
    }

    [Fact]
    public void LosPuertos_SonInterfacesDeLaAplicacionYSusImplementacionesVivenEnLaInfraestructura()
    {
        var puertos = Types.InAssembly(Aplicacion).That().ResideInNamespace("UltimaMilla.Application.Ports").GetTypes().ToList();
        Assert.NotEmpty(puertos);

        // Las implementaciones de acceso a datos (Ef*) están en la infraestructura y no en el dominio ni en la aplicación
        var efEnOtrasCapas = new[] { Dominio, Aplicacion }
            .SelectMany(a => Types.InAssembly(a).That().HaveNameStartingWith("Ef").And().AreClasses().GetTypes())
            .Select(t => t.FullName)
            .ToList();

        Assert.True(efEnOtrasCapas.Count == 0, "Implementaciones de acceso a datos fuera de la infraestructura: " + string.Join(", ", efEnOtrasCapas));
    }

    [Fact]
    public void LasEntidadesDelDominio_NoExponenSetsPublicos()
    {
        // Las entidades protegen sus invariantes: el estado se cambia por métodos del dominio, no asignando propiedades desde afuera
        var entidades = Types.InAssembly(Dominio).That().ResideInNamespaceStartingWith("UltimaMilla.Domain.Entities").And().AreClasses().GetTypes();

        var conSetPublico = entidades
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } && !IsInitOnly(p))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.True(conSetPublico.Count == 0, "Propiedades con set público en entidades: " + string.Join(", ", conSetPublico));
    }

    private static bool IsInitOnly(PropertyInfo propiedad) =>
        propiedad.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
