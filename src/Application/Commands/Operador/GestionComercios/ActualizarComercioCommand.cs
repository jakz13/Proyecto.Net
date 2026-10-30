using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Comando para modificar los datos de un comercio cliente y su estado (UC-01).
/// </summary>
public record ActualizarComercioCommand(
    Guid Id,
    string RazonSocial,
    string NombreFantasia,
    string Email,
    string Telefono,
    string Direccion,
    bool Activo,
    string SelloModificacion
) : IRequest<ComercioDetalleDto>;

