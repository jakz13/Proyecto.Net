using MediatR;

namespace UltimaMilla.Application.Queries.Planificacion;

/// <summary>Listado de hojas de ruta del operador. Los estados se indican separados por coma o como lista.</summary>
public record ListarHojasDeRutaQuery(
    DateOnly? Fecha = null,
    Guid? RepartidorId = null,
    IReadOnlyCollection<string>? Estados = null
) : IRequest<IReadOnlyList<HojaDeRutaResumenDto>>;

public record ObtenerHojaDeRutaQuery(Guid HojaDeRutaId) : IRequest<HojaDeRutaDetalleDto>;

/// <summary>Envíos en depósito, ya clasificados, disponibles para incluir en una hoja de ruta.</summary>
public record ListarEnviosPendientesDeRutaQuery : IRequest<IReadOnlyList<EnvioPendienteDeRutaDto>>;

/// <summary>Control previo al despacho: qué bultos de la ruta se cargaron y cuáles no (UC-09).</summary>
public record ObtenerVerificacionDespachoQuery(Guid HojaDeRutaId) : IRequest<VerificacionDespachoDto>;

public record HojaDeRutaResumenDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string Estado,
    Guid? RepartidorId,
    string? RepartidorDocumento,
    Guid? VehiculoId,
    string? VehiculoPatente,
    string? VehiculoTipo,
    decimal CapacidadPesoKg,
    decimal CapacidadVolumenM3,
    decimal PesoTotalKg,
    decimal VolumenTotalM3,
    int CantidadParadas,
    DateTime? FechaLiberacion,
    DateTime? FechaDespacho,
    Guid SelloModificacion);

public record HojaDeRutaDetalleDto(
    HojaDeRutaResumenDto Ruta,
    IReadOnlyList<ParadaDetalleDto> Paradas);

public record ParadaDetalleDto(
    Guid ParadaId,
    int Orden,
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoParada,
    string Destinatario,
    string Direccion,
    Guid? ZonaId,
    string? VentanaHoraria,
    decimal PesoKg,
    decimal VolumenM3);

public record EnvioPendienteDeRutaDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Destinatario,
    string Direccion,
    Guid? ZonaId,
    string? Prioridad,
    string ModalidadServicio,
    decimal PesoKg,
    decimal VolumenM3,
    Guid? FranjaHorariaId,
    Guid SelloModificacion);

public record VerificacionDespachoDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string EstadoRuta,
    Guid? RepartidorId,
    Guid? VehiculoId,
    string? VehiculoPatente,
    decimal CapacidadPesoKg,
    decimal CapacidadVolumenM3,
    bool TieneRutaPendienteDeRendir,
    string? NumeroRutaPendienteRendicion,
    bool PuedeDespachar,
    IReadOnlyList<BultoVerificacionDto> Bultos)
{
    public int TotalBultos => Bultos.Count;
    public int BultosCargados => Bultos.Count(b => b.CargadoEfectivo);
    public int BultosDiscrepantes => Bultos.Count(b => !b.CargadoEfectivo);
}

public record BultoVerificacionDto(
    Guid BultoId,
    Guid EnvioId,
    string CodigoSeguimiento,
    string CodigoBulto,
    string Destinatario,
    string Direccion,
    decimal PesoKg,
    decimal VolumenM3,
    bool CargadoEfectivo,
    string? MotivoNoCarga);
