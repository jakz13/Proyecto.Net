using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Queries.Planificacion;

/// <summary>Arma las vistas de lectura de las hojas de ruta a partir de las entidades ya cargadas.</summary>
internal static class HojaDeRutaMapper
{
    public static string NumeroRuta(HojaDeRuta ruta) =>
        $"HR-{ruta.Fecha:yyyyMMdd}-{ruta.Id.ToString()[..4].ToUpperInvariant()}";

    public static HojaDeRutaResumenDto Resumir(
        HojaDeRuta ruta,
        IReadOnlyDictionary<Guid, Envio> envios,
        IReadOnlyDictionary<Guid, Vehiculo> vehiculos,
        IReadOnlyDictionary<Guid, Repartidor> repartidores)
    {
        var enviosDeLaRuta = ParadasVigentes(ruta)
            .Where(p => envios.ContainsKey(p.EnvioId))
            .Select(p => envios[p.EnvioId])
            .ToList();

        Vehiculo? vehiculo = null;
        if (ruta.VehiculoId.HasValue)
            vehiculos.TryGetValue(ruta.VehiculoId.Value, out vehiculo);

        Repartidor? repartidor = null;
        if (ruta.RepartidorId.HasValue)
            repartidores.TryGetValue(ruta.RepartidorId.Value, out repartidor);

        return new HojaDeRutaResumenDto(
            ruta.Id,
            NumeroRuta(ruta),
            ruta.Fecha,
            ruta.Estado,
            ruta.RepartidorId,
            repartidor?.Documento,
            ruta.VehiculoId,
            vehiculo?.Patente,
            vehiculo?.Tipo,
            vehiculo?.CapacidadPeso ?? 0m,
            vehiculo?.CapacidadVolumen ?? 0m,
            enviosDeLaRuta.Sum(e => e.PesoTotal),
            enviosDeLaRuta.Sum(e => e.VolumenTotal),
            ParadasVigentes(ruta).Count(),
            ruta.FechaLiberacion,
            ruta.FechaDespacho,
            ruta.SelloModificacion);
    }

    /// <summary>Las paradas canceladas por faltantes ya no forman parte de la ruta.</summary>
    public static IEnumerable<Parada> ParadasVigentes(HojaDeRuta ruta) =>
        ruta.Paradas.Where(p => p.Estado != "Cancelada").OrderBy(p => p.Orden);

    public static string? FormatearVentana(FranjaHoraria? franja) =>
        franja is null ? null : $"{franja.HoraInicio:HH\\:mm}-{franja.HoraFin:HH\\:mm}";
}
