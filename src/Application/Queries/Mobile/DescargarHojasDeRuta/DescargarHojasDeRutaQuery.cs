using MediatR;

namespace UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;

/// <summary>Descarga las hojas de ruta de la jornada del repartidor autenticado. Sin fecha, se usa el día actual.</summary>
public record DescargarHojasDeRutaQuery(DateOnly? Fecha = null) : IRequest<DescargaHojasDeRutaDto>;

public record DescargaHojasDeRutaDto(
    DateTime GeneradoEnUtc,
    DateOnly Fecha,
    IReadOnlyList<HojaDeRutaDescargaDto> Rutas,
    ConfiguracionOfflineDto Configuracion,
    string Mensaje);

/// <summary>Datos de configuración que la app necesita para operar sin conexión.</summary>
public record ConfiguracionOfflineDto(
    string? TipoPruebaEntregaExigida,
    int? MaxIntentosEntrega,
    IReadOnlyList<MotivoNoEntregaDescargaDto> MotivosNoEntrega);

public record MotivoNoEntregaDescargaDto(Guid Id, string Codigo, string Descripcion);

public record HojaDeRutaDescargaDto(
    Guid HojaDeRutaId,
    DateOnly Fecha,
    string Estado,
    Guid? VehiculoId,
    Guid SelloModificacion,
    IReadOnlyList<ParadaDescargaDto> Paradas);

public record ParadaDescargaDto(
    Guid ParadaId,
    Guid EnvioId,
    int Orden,
    string EstadoParada,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string ModalidadServicio,
    Guid SelloEnvio,
    string? VentanaHoraria,
    DireccionDescargaDto Direccion,
    DestinatarioDescargaDto Destinatario,
    IReadOnlyList<BultoDescargaDto> Bultos);

public record DireccionDescargaDto(
    string Texto,
    double? Latitud,
    double? Longitud,
    string? Referencia);

public record DestinatarioDescargaDto(string Nombre, string? Telefono, string? Email);

public record BultoDescargaDto(Guid BultoId, string Identificacion, decimal PesoKg, string? Prioridad);
