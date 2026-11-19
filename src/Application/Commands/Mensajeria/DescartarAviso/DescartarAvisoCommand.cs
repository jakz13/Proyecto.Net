using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.DescartarAviso;

/// <summary>El administrador cierra un aviso agotado o fallido sin reenviarlo, dejando el motivo registrado.</summary>
public record DescartarAvisoCommand(Guid AvisoId, string Motivo) : IRequest;
