using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Common;

internal static class ComercioOperadorActivoGuard
{
    public const string MensajeComercioDadoDeBaja =
        "El comercio se encuentra dado de baja en este operador y solo puede consultar información histórica.";

    public static async Task AsegurarActivoAsync(
        IComercioOperadorRepository? repositorio,
        Guid? comercioOperadorId,
        CancellationToken cancellationToken = default)
    {
        if (repositorio is null || !comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
            return;

        var vinculo = await repositorio.GetByIdSinFiltroAsync(comercioOperadorId.Value, cancellationToken);
        if (vinculo is not null && !vinculo.EstaActivo)
            throw new UnauthorizedAccessException(MensajeComercioDadoDeBaja);
    }
}

