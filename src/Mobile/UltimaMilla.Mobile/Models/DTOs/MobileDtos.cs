namespace UltimaMilla.Mobile.Models.DTOs;

public record LoginRequestDto(
    string Email,
    string Password
);

public record LoginResponseDto(
    string Token,
    Guid UsuarioId,
    string Nombre,
    string Email,
    string Perfil,
    Guid? OperadorId,
    Guid? ComercioId,
    List<string>? Permisos,
    DateTime ExpiraUtc
);

public record MotivoNoEntregaDescargaDto(
    Guid Id,
    string Codigo,
    string Descripcion
);

public record RutaActivaRepartidorDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string Estado,
    Guid RepartidorId,
    Guid? VehiculoId,
    string? PatenteVehiculo,
    string? TipoVehiculo,
    List<ParadaNavegacionDto> Paradas,
    List<MotivoNoEntregaDescargaDto>? MotivosNoEntrega
);

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
    bool EsParadaActual,
    // "Entrega" o "Retiro" (retiro en domicilio, RF 37 a 41). En los retiros EnvioId viene vacío y RetiroId identifica la operación
    string Tipo = "Entrega",
    Guid? RetiroId = null
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
    string Mensaje,
    bool ConflictoResuelto = false
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
    string Mensaje,
    bool ConflictoResuelto = false
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
