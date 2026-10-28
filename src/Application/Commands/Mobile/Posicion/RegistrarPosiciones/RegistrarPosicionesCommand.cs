using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones;

/// <summary>Posición tomada por el dispositivo; <paramref name="TomadaEn"/> permite enviar lotes acumulados sin conexión.</summary>
public record PosicionDispositivoDto(
    double Latitud,
    double Longitud,
    DateTime? TomadaEn = null,
    double? VelocidadKmh = null,
    int? BateriaPct = null,
    Guid? HojaDeRutaId = null);

/// <summary>Registra las posiciones del vehículo del repartidor autenticado (RF-23).</summary>
public record RegistrarPosicionesCommand(IReadOnlyList<PosicionDispositivoDto> Posiciones) : IRequest<RegistrarPosicionesResultDto>;

public record RegistrarPosicionesResultDto(int Recibidas);
