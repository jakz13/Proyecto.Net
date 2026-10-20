using MediatR;

namespace UltimaMilla.Application.Commands.Planificacion.ReasignarEnvio;

/// <summary>
/// Retira un envío de su hoja de ruta (UC-10) y, según la acción, lo deja en depósito, lo asigna a otra
/// hoja de ruta liberada o lo reprograma para otra franja horaria.
/// Acciones: Retirar, ReasignarARuta, Reprogramar.
/// </summary>
public record ReasignarEnvioCommand(
    Guid HojaDeRutaId,
    Guid EnvioId,
    string Accion = "Retirar",
    Guid? HojaDeRutaDestinoId = null,
    Guid? FranjaHorariaId = null,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null
) : IRequest<ReasignacionResultDto>;

public record ReasignacionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string Accion,
    Guid HojaDeRutaOrigenId,
    Guid? HojaDeRutaDestinoId,
    bool PendienteDeRendicion,
    string Mensaje);
