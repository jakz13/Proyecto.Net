namespace UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;

/// <summary>Una funcionalidad del catálogo de la plataforma y si está habilitada para un operador.</summary>
public record FuncionalidadDto(string Clave, string Nombre, bool Habilitada, string? ModificadaPor, DateTime? ModificadaEn);

/// <summary>Funcionalidades de un operador (inquilino) con su estado actual.</summary>
public record OperadorFuncionalidadesDto(Guid OperadorId, string Nombre, IReadOnlyList<FuncionalidadDto> Funcionalidades);
