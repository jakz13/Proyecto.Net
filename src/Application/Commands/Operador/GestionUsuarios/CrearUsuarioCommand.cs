using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para dar de alta un usuario en el operador o asociado a un comercio (UC-01, RF-01).
/// Soporta campos de nombre y apellido separados para mayor claridad y normalización de datos.
/// </summary>
public record CrearUsuarioCommand(
    string Nombre,
    string Email,
    string Password,
    Guid PerfilId,
    Guid? ComercioId = null,
    string Apellido = "",
    string? Documento = null,
    string? Telefono = null,
    Guid? VehiculoAsignadoId = null
) : IRequest<UsuarioDetalleDto>;
