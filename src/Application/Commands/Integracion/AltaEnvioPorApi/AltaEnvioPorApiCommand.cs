using MediatR;
using UltimaMilla.Application.Commands.AltaEnvio;

namespace UltimaMilla.Application.Commands.Integracion.AltaEnvioPorApi;

/// <summary>Alta de un envío desde el sistema del comercio, autenticada con su clave de API (no con una sesión de usuario).</summary>
public record AltaEnvioPorApiCommand(
    string? ClaveApi,
    string ModalidadServicio,
    DestinatarioAltaDto Destinatario,
    DireccionAltaDto Direccion,
    IReadOnlyList<BultoAltaDto> Bultos,
    Guid? FranjaHorariaId = null,
    string? ReferenciaExterna = null
) : IRequest<AltaEnvioResultDto>;
