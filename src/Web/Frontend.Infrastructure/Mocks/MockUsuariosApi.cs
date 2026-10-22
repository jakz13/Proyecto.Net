using System.Net;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de gestión de usuarios y perfiles (UC-01, RF-31).
/// </summary>
public class MockUsuariosApi : IUsuariosApi
{
    private static readonly List<PerfilDto> PerfilesEnMemoria = new()
    {
        new PerfilDto
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Nombre = "Administrador",
            Descripcion = "Acceso y gestión global del operador logístico."
        },
        new PerfilDto
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Nombre = "Despachador",
            Descripcion = "Planificación y asignación de hojas de ruta de distribución."
        },
        new PerfilDto
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Nombre = "Operario",
            Descripcion = "Recepción física de paquetes y bultos en depósito."
        },
        new PerfilDto
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Nombre = "ComercioAdmin",
            Descripcion = "Administración y despacho de envíos para un comercio cliente."
        }
    };

    private static readonly List<UsuarioDto> UsuariosEnMemoria = new()
    {
        new UsuarioDto
        {
            Id = Guid.Parse("10101010-1010-1010-1010-101010101010"),
            Nombre = "Felipe Bernardi",
            Email = "felipe.admin@gmail.com",
            PerfilId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            NombrePerfil = "Administrador",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = null,
            NombreComercio = null,
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-6)
        },
        new UsuarioDto
        {
            Id = Guid.Parse("20202020-2020-2020-2020-202020202020"),
            Nombre = "Laura Rodríguez",
            Email = "laura.despacho@gmail.com",
            PerfilId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            NombrePerfil = "Despachador",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = null,
            NombreComercio = null,
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-3)
        },
        new UsuarioDto
        {
            Id = Guid.Parse("30303030-3030-3030-3030-303030303030"),
            Nombre = "Carlos Méndez",
            Email = "carlos.deposito@gmail.com",
            PerfilId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            NombrePerfil = "Operario",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = null,
            NombreComercio = null,
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-2)
        },
        new UsuarioDto
        {
            Id = Guid.Parse("40404040-4040-4040-4040-404040404040"),
            Nombre = "Ana Fernández",
            Email = "ana@gmail.com",
            PerfilId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            NombrePerfil = "ComercioAdmin",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            NombreComercio = "Distribuidora Central",
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-1)
        },
        new UsuarioDto
        {
            Id = Guid.Parse("50505050-5050-5050-5050-505050505050"),
            Nombre = "Pedro Morales",
            Email = "pedro.inactivo@gmail.com",
            PerfilId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            NombrePerfil = "Despachador",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = null,
            NombreComercio = null,
            Activo = false,
            FechaAlta = DateTime.UtcNow.AddMonths(-4)
        },
        new UsuarioDto
        {
            Id = Guid.Parse("60606060-6060-6060-6060-606060606060"),
            Nombre = "Admin TransLogística",
            Email = "admin2@gmail.com",
            PerfilId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            NombrePerfil = "Administrador",
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ComercioId = null,
            NombreComercio = null,
            Activo = true,
            FechaAlta = DateTime.UtcNow.AddMonths(-1)
        }
    };

    public Task<ResultadoPaginadoDto<UsuarioDto>> ListarUsuariosAsync(FiltroUsuariosDto? filtro = null, CancellationToken cancelacion = default)
    {
        filtro ??= new FiltroUsuariosDto();
        var consulta = UsuariosEnMemoria.AsQueryable();

        // Filtro por texto de búsqueda (nombre o correo)
        if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
        {
            var texto = filtro.TextoBusqueda.Trim().ToLowerInvariant();
            consulta = consulta.Where(u =>
                u.Nombre.ToLowerInvariant().Contains(texto) ||
                u.Email.ToLowerInvariant().Contains(texto));
        }

        // Filtro por perfil
        if (filtro.PerfilId.HasValue && filtro.PerfilId.Value != Guid.Empty)
        {
            consulta = consulta.Where(u => u.PerfilId == filtro.PerfilId.Value);
        }

        // Filtro por estado activo/inactivo
        if (filtro.SoloActivos.HasValue)
        {
            consulta = consulta.Where(u => u.Activo == filtro.SoloActivos.Value);
        }

        var paginaSegura = Math.Max(1, filtro.Pagina);
        var tamanoSeguro = Math.Max(1, filtro.TamanoPagina);

        var totalRegistros = consulta.Count();
        var elementos = consulta
            .OrderByDescending(u => u.FechaAlta)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToList();

        var resultado = new ResultadoPaginadoDto<UsuarioDto>
        {
            Elementos = elementos,
            TotalRegistros = totalRegistros,
            PaginaActual = paginaSegura,
            TamanoPagina = tamanoSeguro
        };

        return Task.FromResult(resultado);
    }

    public Task<UsuarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var usuario = UsuariosEnMemoria.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(usuario);
    }

    public Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioDto nuevoUsuario, CancellationToken cancelacion = default)
    {
        // Validar que el email no esté repetido
        if (UsuariosEnMemoria.Any(u => string.Equals(u.Email, nuevoUsuario.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, $"Ya existe un usuario registrado con el correo {nuevoUsuario.Email}.");
        }

        var perfil = PerfilesEnMemoria.FirstOrDefault(p => p.Id == nuevoUsuario.PerfilId)
            ?? throw new ApiExcepcion(HttpStatusCode.BadRequest, "El perfil seleccionado no es válido.");

        var nuevoId = Guid.NewGuid();
        var entidad = new UsuarioDto
        {
            Id = nuevoId,
            Nombre = nuevoUsuario.Nombre.Trim(),
            Email = nuevoUsuario.Email.Trim().ToLowerInvariant(),
            PerfilId = perfil.Id,
            NombrePerfil = perfil.Nombre,
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ComercioId = nuevoUsuario.ComercioId,
            NombreComercio = nuevoUsuario.ComercioId.HasValue ? "Comercio Asociado" : null,
            Activo = true,
            FechaAlta = DateTime.UtcNow
        };

        UsuariosEnMemoria.Insert(0, entidad);
        return Task.FromResult(entidad);
    }

    public Task<UsuarioDto> ActualizarUsuarioAsync(Guid id, ActualizarUsuarioDto usuarioActualizado, CancellationToken cancelacion = default)
    {
        var indice = UsuariosEnMemoria.FindIndex(u => u.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "El usuario solicitado no fue encontrado.");
        }

        var perfil = PerfilesEnMemoria.FirstOrDefault(p => p.Id == usuarioActualizado.PerfilId)
            ?? throw new ApiExcepcion(HttpStatusCode.BadRequest, "El perfil seleccionado no es válido.");

        var usuarioExistente = UsuariosEnMemoria[indice];
        var modificado = usuarioExistente with
        {
            Nombre = usuarioActualizado.Nombre.Trim(),
            Email = usuarioActualizado.Email.Trim().ToLowerInvariant(),
            PerfilId = perfil.Id,
            NombrePerfil = perfil.Nombre,
            Activo = usuarioActualizado.Activo
        };

        UsuariosEnMemoria[indice] = modificado;
        return Task.FromResult(modificado);
    }

    public Task CambiarEstadoUsuarioAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        var indice = UsuariosEnMemoria.FindIndex(u => u.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "El usuario solicitado no fue encontrado.");
        }

        UsuariosEnMemoria[indice] = UsuariosEnMemoria[indice] with { Activo = activo };
        return Task.CompletedTask;
    }

    public Task AsignarPerfilAsync(Guid id, Guid perfilId, CancellationToken cancelacion = default)
    {
        var indice = UsuariosEnMemoria.FindIndex(u => u.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "El usuario solicitado no fue encontrado.");
        }

        var perfil = PerfilesEnMemoria.FirstOrDefault(p => p.Id == perfilId)
            ?? throw new ApiExcepcion(HttpStatusCode.BadRequest, "El perfil especificado no existe.");

        UsuariosEnMemoria[indice] = UsuariosEnMemoria[indice] with
        {
            PerfilId = perfil.Id,
            NombrePerfil = perfil.Nombre
        };

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PerfilDto>> ListarPerfilesAsync(CancellationToken cancelacion = default)
    {
        return Task.FromResult<IReadOnlyList<PerfilDto>>(PerfilesEnMemoria.AsReadOnly());
    }
}
