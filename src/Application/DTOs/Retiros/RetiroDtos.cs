using UltimaMilla.Domain.Entities.Retiros;

namespace UltimaMilla.Application.DTOs.Retiros;

public record BultoRetiroDto(string Identificacion, string Descripcion, decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);

public record EventoRetiroDto(DateTime Fecha, string Tipo, string? EstadoAnterior, string EstadoNuevo, string Origen, string? Detalle);

public record RetiroResumenDto(
    Guid Id,
    string CodigoSeguimiento,
    string Estado,
    string NombreContacto,
    string DireccionRetiro,
    string Motivo,
    int CantidadBultos,
    decimal PesoTotal,
    decimal Tarifa,
    DateTime FechaSolicitud,
    DateOnly? FechaSolicitada,
    Guid? HojaDeRutaId,
    Guid ComercioOperadorId,
    string? ReferenciaExterna);

public record RetiroDetalleDto(
    RetiroResumenDto Resumen,
    string? TelefonoContacto,
    string? EmailContacto,
    Guid? FranjaHorariaId,
    IReadOnlyList<BultoRetiroDto> Bultos,
    IReadOnlyList<EventoRetiroDto> Eventos,
    DateTime? FechaRetiro,
    string? NombreEntrega,
    string? MotivoNoRetiro,
    bool TieneFotoEstado,
    bool TieneFirma);

public record PaginaRetirosDto(IReadOnlyList<RetiroResumenDto> Items, int Total, int Pagina, int TamanoPagina);

/// <summary>Retiros del operador agrupados por estado, para el tablero de operación y los reportes (RF 41).</summary>
public record ResumenRetirosOperadorDto(
    IReadOnlyDictionary<string, int> PorEstado,
    int Total,
    int EnCurso,
    IReadOnlyList<RetiroResumenDto> Items);

public static class RetiroMapper
{
    public static RetiroResumenDto Resumir(Retiro r) => new(
        r.Id,
        r.CodigoSeguimientoPublico,
        r.EstadoActual.ToString(),
        r.NombreContacto,
        r.DireccionRetiro,
        r.Motivo,
        r.Bultos.Count,
        r.PesoTotal,
        r.TarifaCalculada,
        r.FechaSolicitud,
        r.FechaSolicitada,
        r.HojaDeRutaId,
        r.ComercioOperadorId,
        r.ReferenciaExterna);

    public static RetiroDetalleDto Detallar(Retiro r) => new(
        Resumir(r),
        r.TelefonoContacto,
        r.EmailContacto,
        r.FranjaHorariaId,
        r.Bultos
            .OrderBy(b => b.Identificacion, StringComparer.Ordinal)
            .Select(b => new BultoRetiroDto(b.Identificacion, b.Descripcion, b.Peso, b.Alto, b.Ancho, b.Profundo))
            .ToList(),
        r.Eventos
            .OrderBy(e => e.Timestamp)
            .Select(e => new EventoRetiroDto(e.Timestamp, e.TipoEvento, e.EstadoAnterior?.ToString(), e.EstadoNuevo.ToString(), e.Origen, e.Payload))
            .ToList(),
        r.FechaRetiro,
        r.NombreEntrega,
        r.MotivoNoRetiroCodigo,
        !string.IsNullOrWhiteSpace(r.FotoEstadoBase64),
        !string.IsNullOrWhiteSpace(r.FirmaBase64));
}
