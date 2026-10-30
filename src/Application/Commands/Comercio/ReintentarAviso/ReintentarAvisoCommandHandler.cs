using MediatR;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ReintentarAviso;

/// <summary>
/// Procesa la solicitud de reintento manual de un aviso fallido o agotado (UC-30).
/// </summary>
public class ReintentarAvisoCommandHandler : IRequestHandler<ReintentarAvisoCommand, ResultadoReintentoAvisoDto>
{
    private readonly IAvisoComercioConsultaRepository _avisoRepository;
    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly AvisoComercioEntregador _entregador;

    public ReintentarAvisoCommandHandler(
        IAvisoComercioConsultaRepository avisoRepository,
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant,
        AvisoComercioEntregador entregador)
    {
        _avisoRepository = avisoRepository;
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
        _entregador = entregador;
    }

    public async Task<ResultadoReintentoAvisoDto> Handle(ReintentarAvisoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AvisoId == Guid.Empty)
        {
            throw new DomainValidationException("El identificador del aviso a reintentar no puede ser vacío.");
        }

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para reintentar avisos.");
        }

        var aviso = await _avisoRepository.ObtenerPorIdAsync(request.AvisoId, cancellationToken);
        if (aviso is null)
        {
            throw new KeyNotFoundException($"No se encontró el registro de aviso con ID '{request.AvisoId}'.");
        }

        // Anti-IDOR: Validar que la suscripción pertenezca fehacientemente al comercio autenticado
        var suscripcionesComercio = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);
        if (!suscripcionesComercio.Any(s => s.Id == aviso.SuscripcionAvisoId))
        {
            throw new UnauthorizedAccessException("El aviso especificado no pertenece a la cuenta de su comercio.");
        }

        // El reenvío es real: se firma y se envía al webhook del comercio. Si su sistema sigue caído, el aviso no se da por entregado
        aviso.SolicitarReenvioManual();
        await _entregador.EntregarAsync(new[] { aviso }, cancellationToken);

        var entregado = aviso.Estado == "Entregado";
        return new ResultadoReintentoAvisoDto(
            AvisoId: aviso.Id,
            Exitoso: entregado,
            Estado: aviso.Estado,
            Intentos: aviso.Intentos,
            Mensaje: entregado
                ? "Aviso retransmitido exitosamente hacia el webhook del comercio."
                : $"El sistema del comercio no aceptó el aviso ({aviso.UltimaRespuestaHttp}). Se reintentará automáticamente o puede reenviarlo más tarde.",
            CodigoHttp: ExtraerCodigoHttp(aviso.UltimaRespuestaHttp),
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>Obtiene el primer número de tres dígitos del detalle de la respuesta (por ejemplo "HTTP 503 ..."); 0 si no hay respuesta HTTP.</summary>
    private static int ExtraerCodigoHttp(string? detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle))
            return 0;

        var coincidencia = System.Text.RegularExpressions.Regex.Match(detalle, @"\b[1-5]\d{2}\b");
        return coincidencia.Success ? int.Parse(coincidencia.Value) : 0;
    }
}

