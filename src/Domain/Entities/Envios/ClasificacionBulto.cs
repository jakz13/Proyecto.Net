using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>Criterio operativo con el que se clasifica un bulto: zona, prioridad y tipo.</summary>
public sealed record ClasificacionBulto(
    string CodigoBulto,
    PrioridadBulto Prioridad,
    Guid? ZonaCoberturaId = null,
    string? Tipo = null);
