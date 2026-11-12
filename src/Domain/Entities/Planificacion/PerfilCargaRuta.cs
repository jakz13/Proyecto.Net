namespace UltimaMilla.Domain.Entities.Planificacion;

/// <summary>Una parada vista solo por lo que cambia en el vehículo: una entrega lo descarga y un retiro lo carga.</summary>
public readonly record struct ParadaCarga(bool EsRetiro, decimal Peso, decimal Volumen);

/// <summary>Resultado de recorrer la ruta: carga máxima alcanzada y la primera parada en que se excedería la capacidad.</summary>
public readonly record struct ResultadoPerfilCarga(
    bool Cabe,
    decimal PicoPeso,
    decimal PicoVolumen,
    int? PosicionQueExcede,
    string? Motivo);

/// <summary>
/// Validación de capacidad de una hoja de ruta mixta. Con solo entregas la carga máxima es la del inicio, porque el vehículo
/// sale cargado y solo se descarga. Con retiros la carga varía a lo largo del recorrido: los bultos retirados ocupan lugar desde la
/// parada en que se suben, así que hay que recorrer la ruta en orden y comprobar la capacidad después de cada parada.
/// </summary>
public static class PerfilCargaRuta
{
    /// <param name="paradasEnOrden">Paradas en el orden en que se visitarán.</param>
    /// <param name="capacidadPeso">Capacidad de peso del vehículo.</param>
    /// <param name="capacidadVolumen">Capacidad de volumen del vehículo.</param>
    public static ResultadoPerfilCarga Evaluar(IReadOnlyList<ParadaCarga> paradasEnOrden, decimal capacidadPeso, decimal capacidadVolumen)
    {
        ArgumentNullException.ThrowIfNull(paradasEnOrden);

        // El vehículo sale del depósito con todas las entregas a bordo
        var peso = paradasEnOrden.Where(p => !p.EsRetiro).Sum(p => p.Peso);
        var volumen = paradasEnOrden.Where(p => !p.EsRetiro).Sum(p => p.Volumen);
        var picoPeso = peso;
        var picoVolumen = volumen;

        if (peso > capacidadPeso)
            return new ResultadoPerfilCarga(false, picoPeso, picoVolumen, 0, $"La carga inicial ({peso}) supera la capacidad de peso del vehículo ({capacidadPeso}).");

        if (volumen > capacidadVolumen)
            return new ResultadoPerfilCarga(false, picoPeso, picoVolumen, 0, $"La carga inicial ({volumen}) supera la capacidad de volumen del vehículo ({capacidadVolumen}).");

        for (var i = 0; i < paradasEnOrden.Count; i++)
        {
            var parada = paradasEnOrden[i];
            if (parada.EsRetiro)
            {
                peso += parada.Peso;
                volumen += parada.Volumen;
            }
            else
            {
                peso -= parada.Peso;
                volumen -= parada.Volumen;
            }

            picoPeso = Math.Max(picoPeso, peso);
            picoVolumen = Math.Max(picoVolumen, volumen);

            if (peso > capacidadPeso)
                return new ResultadoPerfilCarga(false, picoPeso, picoVolumen, i + 1,
                    $"Después de la parada {i + 1} el vehículo llevaría {peso} de peso y su capacidad es {capacidadPeso}.");

            if (volumen > capacidadVolumen)
                return new ResultadoPerfilCarga(false, picoPeso, picoVolumen, i + 1,
                    $"Después de la parada {i + 1} el vehículo llevaría {volumen} de volumen y su capacidad es {capacidadVolumen}.");
        }

        return new ResultadoPerfilCarga(true, picoPeso, picoVolumen, null, null);
    }
}
