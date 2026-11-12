using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

/// <summary>Registra los bultos cargados efectivamente en el vehículo y ejecuta el despacho automático de la hoja de ruta.</summary>
public record ConfirmarCargaCommand(
    Guid HojaDeRutaId,
    List<string> CodigosBultosCargados,
    string IdempotencyKey,
    string? ObservacionesDiscrepancia = null,
    bool DespacharAutomaticamente = true
) : IRequest<ConfirmarCargaResultDto>;

public record ConfirmarCargaResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    int TotalBultosCargados,
    int TotalBultosFaltantes,
    int TotalBultosSobrantes,
    bool Idempotente,
    string Mensaje,
    int EnviosEnTransito = 0,
    IReadOnlyList<Guid>? EnviosRetiradosIds = null);
