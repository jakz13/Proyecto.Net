using MediatR;

namespace UltimaMilla.Application.Commands.Comercio.ReintentarAviso;

/// <summary>
/// Comando para solicitar el reenvío manual de un aviso a webhook fallido o agotado (UC-30, RF-28).
/// </summary>
public record ReintentarAvisoCommand(Guid AvisoId) : IRequest<ResultadoReintentoAvisoDto>;

public record ResultadoReintentoAvisoDto(
    Guid AvisoId,
    bool Exitoso,
    string Estado,
    int Intentos,
    string Mensaje,
    int CodigoHttp,
    DateTime Timestamp);

