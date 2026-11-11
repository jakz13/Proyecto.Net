using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Envios.ObtenerEvidenciaEntrega;

public class ObtenerEvidenciaEntregaQueryHandler : IRequestHandler<ObtenerEvidenciaEntregaQuery, EvidenciaEntregaDto>
{
    private const string TipoPorDefecto = "image/png";
    private static readonly HashSet<string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp"
    };

    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerEvidenciaEntregaQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<EvidenciaEntregaDto> Handle(ObtenerEvidenciaEntregaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty || request.IntentoId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío o del intento es inválido.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Mismo aislamiento que el detalle: operador y, para usuarios de comercio, su propio comercio.
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");
        if (_currentTenant.ComercioOperadorId.HasValue && envio.ComercioOperadorId != _currentTenant.ComercioOperadorId.Value)
            throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        var prueba = envio.IntentosEntrega.FirstOrDefault(i => i.Id == request.IntentoId)?.PruebaDeEntrega;
        var origen = request.Tipo == TipoEvidenciaEntrega.Firma ? prueba?.FirmaImagenUrl : prueba?.FotoUrl;
        if (string.IsNullOrWhiteSpace(origen))
            throw new NotFoundException("El intento no tiene esa evidencia registrada.");

        return Decodificar(origen);
    }

    /// <summary>Acepta base64 puro o un data URI ("data:image/png;base64,...") y valida que sea una imagen.</summary>
    private static EvidenciaEntregaDto Decodificar(string origen)
    {
        var tipo = TipoPorDefecto;
        var base64 = origen.Trim();

        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var coma = base64.IndexOf(',');
            if (coma < 0)
                throw new NotFoundException("La evidencia registrada no es una imagen válida.");

            var cabecera = base64[5..coma];
            var tipoDeclarado = cabecera.Split(';')[0];
            if (!TiposPermitidos.Contains(tipoDeclarado))
                throw new NotFoundException("La evidencia registrada no es una imagen válida.");

            tipo = tipoDeclarado.ToLowerInvariant();
            base64 = base64[(coma + 1)..];
        }

        try
        {
            return new EvidenciaEntregaDto(Convert.FromBase64String(base64), tipo);
        }
        catch (FormatException)
        {
            throw new NotFoundException("La evidencia registrada no es una imagen válida.");
        }
    }
}
