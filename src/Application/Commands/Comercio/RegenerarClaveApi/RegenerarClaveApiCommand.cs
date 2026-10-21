using MediatR;

namespace UltimaMilla.Application.Commands.Comercio.RegenerarClaveApi;

/// <summary>El comercio genera una nueva clave de API; la anterior deja de funcionar.</summary>
public record RegenerarClaveApiCommand : IRequest<ClaveApiGeneradaDto>;

/// <summary>La clave en claro se informa una única vez: el sistema solo conserva su huella.</summary>
public record ClaveApiGeneradaDto(string ClaveApi, string Aviso);
