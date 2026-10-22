using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.CrearMotivoNoEntrega;

/// <summary>UC-05: da de alta un motivo de no entrega (activo) con código único dentro del operador.</summary>
public record CrearMotivoNoEntregaCommand(string? Codigo, string? Descripcion, bool EsInterno = false) : IRequest<MotivoNoEntregaDto>;