using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Common;

/// <summary>Validaciones de largo alineadas con las columnas de la base, para rechazar con 400 en vez de fallar al guardar.</summary>
public static class ValidacionTexto
{
    public static void LargoMaximo(string? valor, int maximo, string nombreCampo)
    {
        if (valor is not null && valor.Trim().Length > maximo)
            throw new DomainValidationException($"{nombreCampo} no puede superar los {maximo} caracteres.");
    }
}
