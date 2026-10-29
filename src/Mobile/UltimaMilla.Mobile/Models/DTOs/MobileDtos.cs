namespace UltimaMilla.Mobile.Models.DTOs;

public record DestinatarioContactoDto(
    string Nombre,
    string? Telefono,
    string? Email,
    string? ReferenciaEntrega
);

public record ResumenBultosParadaDto(
    int CantidadBultos,
    List<string> CodigosBultos,
    decimal PesoTotalKg
);

public record ParadaNavegacionDto(
    Guid ParadaId,
    Guid EnvioId,
    int Orden,
    string EstadoParada,
    string CodigoSeguimiento,
    string DireccionFormateada,
    double? Latitud,
    double? Longitud,
    string? VentanaHoraria,
    DestinatarioContactoDto Destinatario,
    ResumenBultosParadaDto ResumenBultos,
    bool EsParadaActual
);

public record ConfirmarCargaResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    DateTime FechaDespacho,
    int TotalBultosCargados,
    int TotalEnviosEnTransito,
    int TotalEnviosDesasignados,
    bool Idempotente,
    string Mensaje
);

public record ParadaActualizadaDto(
    Guid HojaDeRutaId,
    Guid ParadaId,
    string EstadoAnterior,
    string NuevoEstado,
    DateTime Timestamp,
    string Mensaje
);

public record EntregaExitosaResultDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoParada,
    DateTime FechaHoraEntregaUtc,
    bool Idempotente,
    string Mensaje
);

public record ResultadoIntentoFallidoDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoFinalResuelto,
    int IntentosAcumulados,
    int IntentosRestantes,
    int MaxIntentosPermitidos,
    string Motivo,
    DateTime TimestampUtc,
    bool Idempotente,
    string Mensaje
);

public record ResultadoVerificacionCargaDto(
    Guid HojaDeRutaId,
    int TotalBultosAsignados,
    int TotalBultosEscaneados,
    List<string> BultosCorrectos,
    List<string> BultosFaltantes,
    List<string> BultosSobrantes,
    bool EsCargaCompletaYExacta,
    string Mensaje
);

public record RetiroDevolucionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoDevolucion,
    int BultosEnRetorno,
    bool Idempotente,
    string Mensaje
);
