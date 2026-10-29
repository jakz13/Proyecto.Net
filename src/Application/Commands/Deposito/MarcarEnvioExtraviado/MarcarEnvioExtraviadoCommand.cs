using MediatR;

namespace UltimaMilla.Application.Commands.Deposito.MarcarEnvioExtraviado;

/// <summary>Declara un envío como extraviado (RF-11), estado terminal con motivo obligatorio.</summary>
public record MarcarEnvioExtraviadoCommand(Guid EnvioId, string Motivo) : IRequest<MarcarEnvioExtraviadoResultDto>;

public record MarcarEnvioExtraviadoResultDto(Guid EnvioId, string CodigoSeguimiento, string Estado, bool RetiradoDeRuta);
