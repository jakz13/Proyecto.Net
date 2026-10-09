using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class ZonaCobertura : AggregateRoot, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string PoligonoGeografico { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    private readonly List<FranjaHoraria> _franjasHorarias = new();
    public IReadOnlyCollection<FranjaHoraria> FranjasHorarias => _franjasHorarias.AsReadOnly();

    protected ZonaCobertura() { }

    public ZonaCobertura(
        Guid id,
        Guid operadorId,
        string nombre,
        string poligonoGeografico) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre de la zona de cobertura no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(poligonoGeografico))
            throw new DomainValidationException("El polígono geográfico de la zona no puede ser vacío.");

        OperadorId = operadorId;
        Nombre = nombre.Trim();
        PoligonoGeografico = poligonoGeografico.Trim();
        Activa = true;
    }

    public void Activar()
    {
        Activa = true;
    }

    public void Desactivar()
    {
        Activa = false;
    }

    public void ActualizarPoligono(string nuevoPoligono)
    {
        if (string.IsNullOrWhiteSpace(nuevoPoligono))
            throw new DomainValidationException("El polígono geográfico no puede ser vacío.");

        PoligonoGeografico = nuevoPoligono.Trim();
    }

    public void AgregarFranjaHoraria(FranjaHoraria franja)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (franja.ZonaCoberturaId != Id)
            throw new DomainValidationException("La franja horaria no pertenece a esta zona de cobertura.");

        _franjasHorarias.Add(franja);
    }
}
