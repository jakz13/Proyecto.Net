namespace UltimaMilla.Domain.Common;

public interface IHasConcurrencyToken
{
    Guid SelloModificacion { get; }
}
