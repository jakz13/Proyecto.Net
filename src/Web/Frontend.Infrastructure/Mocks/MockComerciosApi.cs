using System.Net;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de gestión de comercios (UC-01).
/// Soporta control de concurrencia optimista con lanzamiento de HTTP 409 Conflict ante sellos discordantes.
/// </summary>
public class MockComerciosApi : IComerciosApi
{
    private static readonly List<ComercioDto> ComerciosEnMemoria = new()
    {
        new ComercioDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RazonSocial = "Distribuidora Del Sur S.A.",
            NombreFantasia = "Distribuidora Central",
            Rut = "214567890014",
            Email = "contacto@distribuidoracentral.uy",
            Telefono = "099 123 456",
            Direccion = "Av. Italia 2450, Montevideo",
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-3),
            SelloModificacion = "sello-inicial-comercio-1"
        },
        new ComercioDto
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RazonSocial = "Soluciones Digitales S.R.L.",
            NombreFantasia = "Electrónica Sol",
            Rut = "219876540018",
            Email = "ventas@electronicasol.uy",
            Telefono = "098 765 432",
            Direccion = "18 de Julio 1020 Apto 401, Montevideo",
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-1),
            SelloModificacion = "sello-inicial-comercio-2"
        },
        new ComercioDto
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            RazonSocial = "Confecciones del Plata S.A.",
            NombreFantasia = "Moda Urbana",
            Rut = "215554440012",
            Email = "pedidos@modaurbana.uy",
            Telefono = "094 333 222",
            Direccion = "Bvar. Artigas 3200, Montevideo",
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddDays(-15),
            SelloModificacion = "sello-inicial-comercio-3"
        },
        new ComercioDto
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            RazonSocial = "Librería Oriental Ltda.",
            NombreFantasia = "El Rincón del Lector",
            Rut = "213332220019",
            Email = "contacto@elrincondellector.uy",
            Telefono = "091 888 777",
            Direccion = "Sarandí 450, Montevideo",
            Activo = false,
            FechaAlta = DateTime.UtcNow.AddMonths(-5),
            SelloModificacion = "sello-inicial-comercio-4"
        }
    };

    public Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(FiltroComerciosDto filtro, CancellationToken cancelacion = default)
    {
        var consulta = ComerciosEnMemoria.AsQueryable();

        // Filtro por texto libre
        if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
        {
            var texto = filtro.TextoBusqueda.Trim().ToLowerInvariant();
            consulta = consulta.Where(c =>
                c.RazonSocial.ToLowerInvariant().Contains(texto) ||
                c.NombreFantasia.ToLowerInvariant().Contains(texto) ||
                c.Rut.Contains(texto) ||
                c.Email.ToLowerInvariant().Contains(texto));
        }

        // Filtro por estado activo/inactivo
        if (filtro.SoloActivos.HasValue)
        {
            consulta = consulta.Where(c => c.Activo == filtro.SoloActivos.Value);
        }

        var paginaSegura = Math.Max(1, filtro.Pagina);
        var tamanoSeguro = Math.Max(1, filtro.TamanoPagina);

        var totalRegistros = consulta.Count();
        var elementos = consulta
            .OrderByDescending(c => c.FechaAlta)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToList();

        var resultado = new ResultadoPaginadoDto<ComercioDto>
        {
            Elementos = elementos,
            TotalRegistros = totalRegistros,
            PaginaActual = paginaSegura,
            TamanoPagina = tamanoSeguro
        };

        return Task.FromResult(resultado);
    }

    public Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(int pagina = 1, int tamanoPagina = 10, CancellationToken cancelacion = default)
    {
        return ListarComerciosAsync(new FiltroComerciosDto
        {
            Pagina = pagina,
            TamanoPagina = tamanoPagina
        }, cancelacion);
    }

    public Task<ComercioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var comercio = ComerciosEnMemoria.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(comercio);
    }

    public Task<ComercioDto> CrearComercioAsync(CrearComercioDto nuevoComercio, CancellationToken cancelacion = default)
    {
        var nuevoId = Guid.NewGuid();
        var entidad = new ComercioDto
        {
            Id = nuevoId,
            RazonSocial = nuevoComercio.RazonSocial.Trim(),
            NombreFantasia = nuevoComercio.NombreFantasia.Trim(),
            Rut = nuevoComercio.Rut.Trim(),
            Email = nuevoComercio.Email.Trim(),
            Telefono = nuevoComercio.Telefono.Trim(),
            Direccion = nuevoComercio.Direccion.Trim(),
            Activo = true,
            FechaAlta = DateTime.UtcNow,
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        ComerciosEnMemoria.Insert(0, entidad);
        return Task.FromResult(entidad);
    }

    public Task<ComercioDto> ActualizarComercioAsync(Guid id, ActualizarComercioDto comercioActualizado, CancellationToken cancelacion = default)
    {
        var indice = ComerciosEnMemoria.FindIndex(c => c.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "El comercio solicitado no fue encontrado.");
        }

        var comercioExistente = ComerciosEnMemoria[indice];

        // Verificación de concurrencia optimista: si el sello enviado no coincide con el guardado
        if (!string.Equals(comercioExistente.SelloModificacion, comercioActualizado.SelloModificacion, StringComparison.Ordinal))
        {
            throw new ApiExcepcion(
                HttpStatusCode.Conflict,
                "Otra persona modificó este registro, recargá y reintentá."
            );
        }

        // Generar un nuevo sello tras la modificación exitosa
        var comercioModificado = comercioExistente with
        {
            RazonSocial = comercioActualizado.RazonSocial.Trim(),
            NombreFantasia = comercioActualizado.NombreFantasia.Trim(),
            Email = comercioActualizado.Email.Trim(),
            Telefono = comercioActualizado.Telefono.Trim(),
            Direccion = comercioActualizado.Direccion.Trim(),
            Activo = comercioActualizado.Activo,
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        ComerciosEnMemoria[indice] = comercioModificado;
        return Task.FromResult(comercioModificado);
    }

    public Task CambiarEstadoComercioAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        var indice = ComerciosEnMemoria.FindIndex(c => c.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "El comercio solicitado no fue encontrado.");
        }

        var comercio = ComerciosEnMemoria[indice];
        ComerciosEnMemoria[indice] = comercio with
        {
            Activo = activo,
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        return Task.CompletedTask;
    }
}
