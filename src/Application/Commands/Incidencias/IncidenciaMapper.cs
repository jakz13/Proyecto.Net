using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;
using UltimaMilla.Domain.Entities.Incidencias;

namespace UltimaMilla.Application.Commands.Incidencias;

internal static class IncidenciaMapper
{
    public static IncidenciaResultDto ToResult(Incidencia incidencia) =>
        new(incidencia.Id, incidencia.Tipo, incidencia.Severidad, incidencia.Estado, incidencia.EnvioId, incidencia.HojaDeRutaId, incidencia.FechaApertura);
}
