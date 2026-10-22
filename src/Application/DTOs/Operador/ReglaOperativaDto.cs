namespace UltimaMilla.Application.DTOs.Operador;

public record ReglaOperativaDto(
    Guid Id,
    Guid OperadorId,
    int Version,
    DateTime VigenteDesde,
    DateTime? VigenteHasta,
    int MaxIntentosEntrega,
    TimeSpan PlazoEntreIntentos,
    string TipoPruebaEntregaExigida,
    string PoliticaDevolucion,
    TimeSpan PlazoComprometidoEstandar,
    TimeSpan PlazoComprometidoUrgente,
    int MaxParadasPorRuta);

public record ResultadoSimulacionReglasDto(
    bool Exitoso,
    int VersionAplicada,
    int MaxIntentosPermitidos,
    int IntentosRestantes,
    bool SuperaMaximoIntentos,
    DateTime? ProximaFechaReintentoPermitida,
    bool PuedeReintentarAhora,
    DateTime FechaLimiteSla,
    bool SlaVencido,
    string TipoPruebaExigida,
    string PoliticaDevolucion,
    string AccionSugerida);
