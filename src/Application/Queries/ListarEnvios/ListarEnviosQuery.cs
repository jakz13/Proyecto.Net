using MediatR;
using UltimaMilla.Application.DTOs;

namespace UltimaMilla.Application.Queries.ListarEnvios;

public record ListarEnviosQuery() : IRequest<List<EnvioDto>>;
