using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.PublicarReglaOperativa;

public class PublicarReglaOperativaCommandHandler : IRequestHandler<PublicarReglaOperativaCommand, ReglaOperativaDto>
{
    public static readonly string[] TiposPruebaPermitidos = ["Firma", "Foto", "Documento", "Completa"];

    private readonly IReglaOperativaRepository _reglas;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PublicarReglaOperativaCommandHandler(
        IReglaOperativaRepository reglas, ICurrentTenant currentTenant, IClock clock, IUnitOfWork unitOfWork)
    {
        _reglas = reglas;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReglaOperativaDto> Handle(PublicarReglaOperativaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tipoPrueba = request.TipoPruebaEntregaExigida?.Trim() ?? string.Empty;
        var tieneFirma = tipoPrueba.Contains("Firma", StringComparison.OrdinalIgnoreCase);
        var tieneFoto = tipoPrueba.Contains("Foto", StringComparison.OrdinalIgnoreCase);
        var tieneDoc = tipoPrueba.Contains("Documento", StringComparison.OrdinalIgnoreCase);
        var esCompleta = tipoPrueba.Contains("Completa", StringComparison.OrdinalIgnoreCase) || (tieneFirma && tieneFoto);

        string tipoCanonico;
        if (esCompleta)
            tipoCanonico = "Completa";
        else if (tieneFirma && tieneDoc)
            tipoCanonico = "Firma y Documento";
        else if (tieneFoto && tieneDoc)
            tipoCanonico = "Foto y Documento";
        else
            tipoCanonico = TiposPruebaPermitidos.FirstOrDefault(t => string.Equals(t, tipoPrueba, StringComparison.OrdinalIgnoreCase))
                ?? TiposPruebaPermitidos.FirstOrDefault(t => tipoPrueba.Contains(t, StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainValidationException($"El tipo de prueba de entrega debe ser uno de: {string.Join(", ", TiposPruebaPermitidos)}.");

        if (request.PlazoEntreIntentos < TimeSpan.Zero
            || request.PlazoComprometidoEstandar < TimeSpan.Zero
            || request.PlazoComprometidoUrgente < TimeSpan.Zero)
            throw new DomainValidationException("Los plazos no pueden ser negativos.");

        var ahora = _clock.UtcNow;
        var operadorId = _currentTenant.OperadorId;

        var vigente = await _reglas.ObtenerVigenteAsync(operadorId, cancellationToken);
        var version = (await _reglas.ListarAsync(operadorId, cancellationToken)).Select(r => r.Version).DefaultIfEmpty(0).Max() + 1;

        // Se construye primero la nueva versión: si los datos son inválidos no se archiva la vigente.
        var nueva = new ReglaOperativa(
            Guid.NewGuid(),
            operadorId,
            version,
            ahora,
            request.MaxIntentosEntrega,
            request.PlazoEntreIntentos,
            tipoCanonico,
            request.PoliticaDevolucion ?? string.Empty,
            request.PlazoComprometidoEstandar,
            request.PlazoComprometidoUrgente,
            null,
            request.MaxParadasPorRuta,
            request.AdmiteEntregaParcial,
            request.PoliticaEntregaParcial ?? ReglaOperativa.PoliticaNuevaVisitaSinRecargo);

        nueva.RegistrarComentario(request.Comentario);
        vigente?.FinalizarVigencia(ahora);
        await _reglas.AddAsync(nueva, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperadorMapper.ToDto(nueva);
    }
}
