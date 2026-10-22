using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.DTOs.Operador;

public static class OperadorMapper
{
    public static ReglaOperativaDto ToDto(ReglaOperativa r) => new(
        r.Id, r.OperadorId, r.Version, r.VigenteDesde, r.VigenteHasta, r.MaxIntentosEntrega, r.PlazoEntreIntentos,
        r.TipoPruebaEntregaExigida, r.PoliticaDevolucion, r.PlazoComprometidoEstandar, r.PlazoComprometidoUrgente, r.MaxParadasPorRuta);

    public static MotivoNoEntregaDto ToDto(MotivoNoEntrega m) => new(m.Id, m.Codigo, m.Descripcion, m.Activo, m.EsInterno);
}