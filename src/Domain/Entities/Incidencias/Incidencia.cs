using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Incidencias;

/// <summary>Situación anómala de la jornada, con su ciclo de apertura, seguimiento y cierre.</summary>
public class Incidencia : AggregateRoot, IMustHaveTenant
{
    public const string EstadoAbierta = "Abierta";
    public const string EstadoEnSeguimiento = "EnSeguimiento";
    public const string EstadoCerrada = "Cerrada";

    public static readonly string[] Severidades = { "Baja", "Media", "Alta", "Critica" };

    public Guid OperadorId { get; private set; }
    public Guid? EnvioId { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Severidad { get; private set; } = "Media";
    public string Descripcion { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;
    public Guid? ResponsableUsuarioId { get; private set; }
    public Guid? CreadaPorUsuarioId { get; private set; }
    public DateTime FechaApertura { get; private set; }
    public DateTime? FechaCierre { get; private set; }
    public string? Resolucion { get; private set; }

    private readonly List<NotaIncidencia> _notas = new();
    public IReadOnlyCollection<NotaIncidencia> Notas => _notas.AsReadOnly();

    protected Incidencia() { }

    public Incidencia(
        Guid id,
        Guid operadorId,
        Guid? envioId,
        string tipo,
        string descripcion,
        string severidad = "Media",
        Guid? hojaDeRutaId = null,
        Guid? creadaPorUsuarioId = null,
        DateTime? fechaApertura = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la incidencia no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipo))
            throw new DomainValidationException("El tipo de incidencia no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainValidationException("La descripción de la incidencia no puede ser vacía.");

        var severidadNormalizada = Severidades.FirstOrDefault(s => s.Equals(severidad?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainValidationException($"La severidad '{severidad}' no es válida. Valores admitidos: {string.Join(", ", Severidades)}.");

        OperadorId = operadorId;
        EnvioId = envioId;
        HojaDeRutaId = hojaDeRutaId;
        Tipo = tipo.Trim();
        Descripcion = descripcion.Trim();
        Severidad = severidadNormalizada;
        CreadaPorUsuarioId = creadaPorUsuarioId;
        Estado = EstadoAbierta;
        FechaApertura = fechaApertura ?? DateTime.UtcNow;
    }

    public bool EstaCerrada => Estado == EstadoCerrada;

    /// <summary>El usuario toma la incidencia a su cargo; pasa a seguimiento.</summary>
    public void AsignarResponsable(Guid usuarioId, DateTime? fecha = null)
    {
        AsegurarAbierta();

        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario responsable no puede ser vacío.");

        ResponsableUsuarioId = usuarioId;
        Estado = EstadoEnSeguimiento;
        _notas.Add(new NotaIncidencia(Guid.NewGuid(), Id, usuarioId, "Incidencia tomada a cargo.", fecha ?? DateTime.UtcNow));
    }

    public NotaIncidencia AgregarNota(Guid usuarioId, string comentario, DateTime? fecha = null)
    {
        AsegurarAbierta();

        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario no puede ser vacío.");

        var nota = new NotaIncidencia(Guid.NewGuid(), Id, usuarioId, comentario, fecha ?? DateTime.UtcNow);
        _notas.Add(nota);

        if (Estado == EstadoAbierta)
            Estado = EstadoEnSeguimiento;

        return nota;
    }

    public void Cerrar(Guid usuarioId, string resolucion, DateTime? fecha = null)
    {
        AsegurarAbierta();

        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(resolucion))
            throw new DomainValidationException("Para cerrar la incidencia indique cómo se resolvió.");

        var momento = fecha ?? DateTime.UtcNow;
        Resolucion = resolucion.Trim();
        Estado = EstadoCerrada;
        FechaCierre = momento;
        _notas.Add(new NotaIncidencia(Guid.NewGuid(), Id, usuarioId, $"Incidencia cerrada: {Resolucion}", momento));
    }

    private void AsegurarAbierta()
    {
        if (EstaCerrada)
            throw new DomainValidationException("La incidencia ya está cerrada.");
    }
}
