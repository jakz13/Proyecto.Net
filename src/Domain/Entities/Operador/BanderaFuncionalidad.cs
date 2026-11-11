using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

/// <summary>Funcionalidades que la plataforma puede activar o desactivar por operador, sin nuevo despliegue.</summary>
public static class Funcionalidad
{
    /// <summary>Entrega parcial de bultos (cambio de requerimientos 2.1).</summary>
    public const string EntregaParcial = "entrega-parcial";

    /// <summary>Retiro en domicilio (cambio de requerimientos 2.2).</summary>
    public const string RetiroEnDomicilio = "retiro-en-domicilio";

    /// <summary>Solicitud de reprogramación por el destinatario desde el seguimiento público (RF 26).</summary>
    public const string ReprogramacionPublica = "reprogramacion-publica";

    public static readonly IReadOnlyDictionary<string, string> Catalogo = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [EntregaParcial] = "Entrega parcial de bultos",
        [RetiroEnDomicilio] = "Retiro en domicilio",
        [ReprogramacionPublica] = "Reprogramación desde el seguimiento público"
    };

    public static bool Existe(string clave) => Catalogo.ContainsKey(clave);
}

/// <summary>
/// Estado de una funcionalidad para un operador (banderas de funcionalidad). La activa o desactiva el administrador de la plataforma,
/// quien ofrece el servicio, y no los usuarios de cada operador. Sin registro, la funcionalidad está habilitada: las banderas se usan
/// para apagar o para ofrecer planes, no para ocultar lo que ya existía. No lleva operador como inquilino porque quien la escribe
/// opera sobre todos los operadores.
/// </summary>
public class BanderaFuncionalidad : Entity
{
    public Guid OperadorId { get; private set; }
    public string Clave { get; private set; } = string.Empty;
    public bool Habilitada { get; private set; }
    public Guid? ModificadaPorUsuarioId { get; private set; }
    public string? ModificadaPorNombre { get; private set; }
    public DateTime ModificadaEn { get; private set; }

    protected BanderaFuncionalidad() { }

    public BanderaFuncionalidad(Guid id, Guid operadorId, string clave, bool habilitada, Guid? usuarioId, string? usuarioNombre, DateTime ahora)
        : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la bandera no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (!Funcionalidad.Existe(clave))
            throw new DomainValidationException($"La funcionalidad '{clave}' no existe.");

        OperadorId = operadorId;
        Clave = clave;
        Habilitada = habilitada;
        ModificadaPorUsuarioId = usuarioId;
        ModificadaPorNombre = usuarioNombre?.Trim();
        ModificadaEn = ahora;
    }

    /// <summary>Cambia el valor y devuelve el registro del cambio para la auditoría. Si no hay cambio devuelve null.</summary>
    public HistorialBandera? Establecer(bool habilitada, Guid? usuarioId, string? usuarioNombre, DateTime ahora)
    {
        if (Habilitada == habilitada)
            return null;

        var cambio = new HistorialBandera(Guid.NewGuid(), OperadorId, Clave, Habilitada, habilitada, usuarioId, usuarioNombre, ahora);
        Habilitada = habilitada;
        ModificadaPorUsuarioId = usuarioId;
        ModificadaPorNombre = usuarioNombre?.Trim();
        ModificadaEn = ahora;
        return cambio;
    }
}

/// <summary>Registro inmutable de quién cambió una bandera, cuándo y de qué valor a cuál.</summary>
public class HistorialBandera : Entity
{
    public Guid OperadorId { get; init; }
    public string Clave { get; init; } = string.Empty;
    public bool ValorAnterior { get; init; }
    public bool ValorNuevo { get; init; }
    public Guid? UsuarioId { get; init; }
    public string? UsuarioNombre { get; init; }
    public DateTime Fecha { get; init; }

    protected HistorialBandera() { }

    public HistorialBandera(Guid id, Guid operadorId, string clave, bool valorAnterior, bool valorNuevo, Guid? usuarioId, string? usuarioNombre, DateTime fecha)
        : base(id)
    {
        OperadorId = operadorId;
        Clave = clave;
        ValorAnterior = valorAnterior;
        ValorNuevo = valorNuevo;
        UsuarioId = usuarioId;
        UsuarioNombre = usuarioNombre?.Trim();
        Fecha = fecha;
    }
}
