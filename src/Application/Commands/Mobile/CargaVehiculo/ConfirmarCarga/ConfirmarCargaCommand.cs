using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

/// <summary>Registra qué bultos se cargaron efectivamente en el vehículo. No despacha la ruta: eso lo hace el operario (UC-09).</summary>
public record ConfirmarCargaCommand(
    Guid HojaDeRutaId,
    List<string> CodigosBultosCargados,
    string IdempotencyKey
) : IRequest<ConfirmarCargaResultDto>;

public record ConfirmarCargaResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    int TotalBultosCargados,
    int TotalBultosFaltantes,
    int TotalBultosSobrantes,
    bool Idempotente,
    string Mensaje);
