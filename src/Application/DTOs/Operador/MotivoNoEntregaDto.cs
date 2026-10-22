namespace UltimaMilla.Application.DTOs.Operador;

public record MotivoNoEntregaDto(Guid Id, string Codigo, string Descripcion, bool Activo, bool EsInterno);

public record ResumenMotivosNoEntregaDto(
    int TotalMotivos,
    int MotivosActivosParaMovil,
    int MotivosInactivos,
    DateTime UltimaSincronizacionApp);
