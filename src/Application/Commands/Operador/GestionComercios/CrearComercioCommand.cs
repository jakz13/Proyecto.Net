using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Comando para dar de alta un comercio cliente en el operador (UC-01).
/// </summary>
public record CrearComercioCommand(
    string RazonSocial,
    string NombreFantasia,
    string Rut,
    string Email,
    string Telefono,
    string Direccion
) : IRequest<ComercioDetalleDto>;

