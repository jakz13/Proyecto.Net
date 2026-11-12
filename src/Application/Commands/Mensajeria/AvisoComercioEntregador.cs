using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Commands.Mensajeria;

/// <summary>Resultado de intentar entregar un conjunto de avisos a los sistemas de los comercios.</summary>
public record ResultadoEntregaAvisos(int Entregados, int EnReintento, int Agotados);

/// <summary>
/// Entrega avisos automáticos a los comercios: llamada HTTP firmada, reintentos con espera creciente y, al agotarse,
/// estado "Agotado" para reenvío manual. Una falla de entrega nunca interrumpe el procesamiento del mensaje que originó el aviso:
/// queda registrada en el propio aviso y el proceso de reintentos la retoma.
/// </summary>
public class AvisoComercioEntregador
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly IWebhookSender _webhookSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IMetricasNegocio? _metricas;

    public AvisoComercioEntregador(
        IAvisoComercioRepository avisoRepository,
        IWebhookSender webhookSender,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMetricasNegocio? metricas = null)
    {
        _avisoRepository = avisoRepository;
        _webhookSender = webhookSender;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _metricas = metricas;
    }

    public async Task<ResultadoEntregaAvisos> EntregarAsync(IReadOnlyCollection<AvisoComercio> avisos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(avisos);

        int entregados = 0, enReintento = 0, agotados = 0;

        foreach (var aviso in avisos)
        {
            var suscripcion = await _avisoRepository.GetSuscripcionAsync(aviso.SuscripcionAvisoId, cancellationToken);
            if (suscripcion is null || !suscripcion.Activa)
            {
                aviso.MarcarAgotado("La suscripción del comercio ya no existe o fue desactivada.");
                _metricas?.RegistrarIntentoAviso(false);
                agotados++;
                continue;
            }

            var resultado = await _webhookSender.EnviarAsync(suscripcion.UrlWebhook, aviso.Payload, aviso.FirmaEnviada, aviso.Id, cancellationToken);
            _metricas?.RegistrarIntentoAviso(resultado.Exitoso);

            if (resultado.Exitoso)
            {
                aviso.MarcarEntregado(resultado.Detalle);
                entregados++;
            }
            else if (PoliticaReintentoAvisos.SeAgotan(aviso.Intentos))
            {
                aviso.MarcarAgotado(resultado.Detalle);
                agotados++;
            }
            else
            {
                aviso.RegistrarFallo(resultado.Detalle, _clock.UtcNow + PoliticaReintentoAvisos.EsperaDespuesDeFallar(aviso.Intentos));
                enReintento++;
            }
        }

        if (avisos.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoEntregaAvisos(entregados, enReintento, agotados);
    }
}
