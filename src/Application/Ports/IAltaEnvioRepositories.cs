using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

/// <summary>Acceso al cuadro tarifario vigente del operador para tarificar envíos.</summary>
public interface ICuadroTarifarioRepository
{
    Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default);
}

/// <summary>Alta de destinatarios. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IDestinatarioRepository
{
    Task AddAsync(Destinatario destinatario, CancellationToken cancellationToken = default);
}

/// <summary>Alta de direcciones de entrega. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IDireccionRepository
{
    Task AddAsync(Direccion direccion, CancellationToken cancellationToken = default);
}
