using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;

namespace UltimaMilla.Application.Queries.Mobile.ObtenerRutaActivaRepartidor;

/// <summary>
/// Consulta la hoja de ruta activa (en estado Despachada o EnTransito) asignada al repartidor autenticado.
/// </summary>
public record ObtenerRutaActivaRepartidorQuery() : IRequest<RutaActivaRepartidorDto>;

/// <summary>
/// Datos completos de la hoja de ruta activa del repartidor junto con su secuencia ordenada de paradas, bultos y destinatarios.
/// </summary>
public record RutaActivaRepartidorDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string Estado,
    Guid RepartidorId,
    Guid? VehiculoId,
    string? PatenteVehiculo,
    string? TipoVehiculo,
    IReadOnlyList<ParadaNavegacionDto> Paradas,
    IReadOnlyList<MotivoNoEntregaDescargaDto> MotivosNoEntrega
);

