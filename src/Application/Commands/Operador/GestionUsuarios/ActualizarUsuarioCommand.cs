using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para modificar los datos y perfil de un usuario existente.
/// Soporta campos de nombre y apellido separados para mayor claridad y normalización de datos.
/// </summary>
public record ActualizarUsuarioCommand(
    Guid Id,
    string Nombre,
    string Email,
    Guid PerfilId,
    bool Activo,
    string Apellido = "",
    string? Documento = null,
    string? Telefono = null,
    Guid? VehiculoAsignadoId = null
) : IRequest<UsuarioDetalleDto>;
