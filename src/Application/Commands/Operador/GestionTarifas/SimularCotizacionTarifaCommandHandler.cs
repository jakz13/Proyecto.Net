using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Commands.Operador.GestionTarifas;

/// <summary>
/// Manejador para la simulación del cálculo tarifario bajo la versión vigente.
/// </summary>
public class SimularCotizacionTarifaCommandHandler : IRequestHandler<SimularCotizacionTarifaCommand, CotizacionSimuladaDto>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public SimularCotizacionTarifaCommandHandler(
        ICuadroTarifarioRepository cuadroRepositorio,
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _cuadroRepositorio = cuadroRepositorio;
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<CotizacionSimuladaDto> Handle(SimularCotizacionTarifaCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var ahoraUtc = DateTime.UtcNow;

        var vigente = await _cuadroRepositorio.ObtenerVigenteAsync(operadorId, ahoraUtc, cancellationToken);
        if (vigente == null || vigente.ReglasTarifa.Count == 0)
        {
            return new CotizacionSimuladaDto(
                Exitoso: false,
                PrecioFinal: 0,
                PrecioBase: 0,
                Recargo: 0,
                Bonificacion: 0,
                VersionCuadroAplicada: 0,
                ReglaAplicada: null,
                Mensaje: "No hay ningún cuadro tarifario vigente disponible para calcular precios."
            );
        }

        var regla = vigente.ObtenerReglaAplicable(
            solicitud.PesoKg,
            solicitud.VolumenM3,
            solicitud.ModalidadServicio,
            solicitud.ZonaCoberturaId);

        if (regla == null)
        {
            return new CotizacionSimuladaDto(
                Exitoso: false,
                PrecioFinal: 0,
                PrecioBase: 0,
                Recargo: 0,
                Bonificacion: 0,
                VersionCuadroAplicada: vigente.Version,
                ReglaAplicada: null,
                Mensaje: "Ninguna regla de la versión vigente cubre ese peso, volumen y modalidad de servicio."
            );
        }

        var zonasDelOperador = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonasDelOperador.ToDictionary(z => z.Id, z => z.Nombre);
        var ambito = regla.ZonaCoberturaId.HasValue && mapaNombresZonas.TryGetValue(regla.ZonaCoberturaId.Value, out var nombreZona)
            ? nombreZona
            : "Todas las zonas";

        return new CotizacionSimuladaDto(
            Exitoso: true,
            PrecioFinal: regla.CalcularPrecioFinal(),
            PrecioBase: regla.PrecioBase,
            Recargo: regla.Recargo,
            Bonificacion: regla.Bonificacion,
            VersionCuadroAplicada: vigente.Version,
            ReglaAplicada: $"{regla.ModalidadServicio} ({ambito}) — Rango {regla.PesoDesde}kg a {regla.PesoHasta}kg",
            Mensaje: "Tarifa calculada con éxito bajo la versión vigente."
        );
    }
}
