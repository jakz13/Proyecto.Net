using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Operador;

public class ListarReglasOperativasQueryHandler(IReglaOperativaRepository reglas, ICurrentTenant tenant)
    : IRequestHandler<ListarReglasOperativasQuery, IReadOnlyList<ReglaOperativaDto>>
{
    public async Task<IReadOnlyList<ReglaOperativaDto>> Handle(ListarReglasOperativasQuery request, CancellationToken cancellationToken) =>
        (await reglas.ListarAsync(tenant.OperadorId, cancellationToken)).Select(OperadorMapper.ToDto).ToList();
}

public class ObtenerReglaOperativaVigenteQueryHandler(IReglaOperativaRepository reglas, ICurrentTenant tenant)
    : IRequestHandler<ObtenerReglaOperativaVigenteQuery, ReglaOperativaDto?>
{
    public async Task<ReglaOperativaDto?> Handle(ObtenerReglaOperativaVigenteQuery request, CancellationToken cancellationToken)
    {
        var vigente = await reglas.ObtenerVigenteAsync(tenant.OperadorId, cancellationToken);
        return vigente is null ? null : OperadorMapper.ToDto(vigente);
    }
}

public class ObtenerReglaOperativaQueryHandler(IReglaOperativaRepository reglas, ICurrentTenant tenant)
    : IRequestHandler<ObtenerReglaOperativaQuery, ReglaOperativaDto>
{
    public async Task<ReglaOperativaDto> Handle(ObtenerReglaOperativaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReglaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la regla operativa es inválido.");

        var regla = await reglas.GetByIdAsync(request.ReglaId, cancellationToken)
            ?? throw new NotFoundException($"Regla operativa '{request.ReglaId}' no encontrada.");
        if (regla.OperadorId != tenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la regla no corresponde al operador actual.");

        return OperadorMapper.ToDto(regla);
    }
}

public class SimularReglaOperativaQueryHandler(IReglaOperativaRepository reglas, ICurrentTenant tenant, IClock clock)
    : IRequestHandler<SimularReglaOperativaQuery, ResultadoSimulacionReglasDto>
{
    public async Task<ResultadoSimulacionReglasDto> Handle(SimularReglaOperativaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vigente = await reglas.ObtenerVigenteAsync(tenant.OperadorId, cancellationToken);
        if (vigente is null)
        {
            return new ResultadoSimulacionReglasDto(
                Exitoso: false,
                VersionAplicada: 0,
                MaxIntentosPermitidos: 0,
                IntentosRestantes: 0,
                SuperaMaximoIntentos: false,
                ProximaFechaReintentoPermitida: null,
                PuedeReintentarAhora: false,
                FechaLimiteSla: clock.UtcNow,
                SlaVencido: false,
                TipoPruebaExigida: string.Empty,
                PoliticaDevolucion: string.Empty,
                AccionSugerida: "No hay versión de reglas operativas vigente configurada.");
        }

        var ahora = clock.UtcNow;
        var modalidad = request.ModalidadServicio ?? string.Empty;
        var esModalidadUrgente = modalidad.Contains("Express", StringComparison.OrdinalIgnoreCase) ||
                                 modalidad.Contains("Mismo Día", StringComparison.OrdinalIgnoreCase) ||
                                 modalidad.Contains("Mismo Dia", StringComparison.OrdinalIgnoreCase) ||
                                 modalidad.Contains("Urgente", StringComparison.OrdinalIgnoreCase);

        var plazoComprometido = esModalidadUrgente ? vigente.PlazoComprometidoUrgente : vigente.PlazoComprometidoEstandar;
        var fechaLimiteSla = request.FechaDespacho.Add(plazoComprometido);
        var slaVencido = ahora > fechaLimiteSla;

        var intentosRestantes = Math.Max(0, vigente.MaxIntentosEntrega - request.IntentosRealizados);
        var superaMaximo = request.IntentosRealizados >= vigente.MaxIntentosEntrega;

        DateTime? proximaFecha = null;
        bool puedeReintentarAhora;
        string accionSugerida;

        if (superaMaximo)
        {
            puedeReintentarAhora = false;
            accionSugerida = $"Se alcanzó el máximo de {vigente.MaxIntentosEntrega} intentos fallidos. Debe activarse la política de devolución: {vigente.PoliticaDevolucion}.";
        }
        else if (request.FechaUltimoIntento.HasValue)
        {
            proximaFecha = request.FechaUltimoIntento.Value.Add(vigente.PlazoEntreIntentos);
            if (ahora >= proximaFecha.Value)
            {
                puedeReintentarAhora = true;
                accionSugerida = $"Habilitado para nuevo intento de entrega ({intentosRestantes} restantes). Se cumplió el plazo de {(int)vigente.PlazoEntreIntentos.TotalHours}h entre intentos.";
            }
            else
            {
                puedeReintentarAhora = false;
                accionSugerida = $"En espera reglamentaria. Próximo intento habilitado el {proximaFecha.Value:dd/MM/yyyy HH:mm} (regla de {(int)vigente.PlazoEntreIntentos.TotalHours}h entre intentos).";
            }
        }
        else
        {
            puedeReintentarAhora = true;
            accionSugerida = $"Envío en curso para primer intento. Entrega comprometida antes de {fechaLimiteSla:dd/MM/yyyy HH:mm}.";
        }

        return new ResultadoSimulacionReglasDto(
            Exitoso: true,
            VersionAplicada: vigente.Version,
            MaxIntentosPermitidos: vigente.MaxIntentosEntrega,
            IntentosRestantes: intentosRestantes,
            SuperaMaximoIntentos: superaMaximo,
            ProximaFechaReintentoPermitida: proximaFecha,
            PuedeReintentarAhora: puedeReintentarAhora,
            FechaLimiteSla: fechaLimiteSla,
            SlaVencido: slaVencido,
            TipoPruebaExigida: vigente.TipoPruebaEntregaExigida,
            PoliticaDevolucion: vigente.PoliticaDevolucion,
            AccionSugerida: accionSugerida);
    }
}

public class ListarMotivosNoEntregaQueryHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant tenant)
    : IRequestHandler<ListarMotivosNoEntregaQuery, IReadOnlyList<MotivoNoEntregaDto>>
{
    public async Task<IReadOnlyList<MotivoNoEntregaDto>> Handle(ListarMotivosNoEntregaQuery request, CancellationToken cancellationToken)
    {
        var lista = await motivos.ListarAsync(tenant.OperadorId, false, cancellationToken);
        var filtrados = lista.AsEnumerable();

        if (request.SoloActivos.HasValue)
            filtrados = filtrados.Where(m => m.Activo == request.SoloActivos.Value);

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var busqueda = request.Busqueda.Trim();
            filtrados = filtrados.Where(m =>
                m.Codigo.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                || m.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase));
        }

        return filtrados.Select(OperadorMapper.ToDto).ToList();
    }
}

public class ObtenerResumenMotivosNoEntregaQueryHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant tenant, IClock clock)
    : IRequestHandler<ObtenerResumenMotivosNoEntregaQuery, ResumenMotivosNoEntregaDto>
{
    public async Task<ResumenMotivosNoEntregaDto> Handle(ObtenerResumenMotivosNoEntregaQuery request, CancellationToken cancellationToken)
    {
        var todos = await motivos.ListarAsync(tenant.OperadorId, false, cancellationToken);
        var total = todos.Count;
        var activosMovil = todos.Count(m => m.Activo && !m.EsInterno);
        var inactivos = todos.Count(m => !m.Activo);

        return new ResumenMotivosNoEntregaDto(total, activosMovil, inactivos, clock.UtcNow);
    }
}

public class ListarMotivosParaRepartidorQueryHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant tenant)
    : IRequestHandler<ListarMotivosParaRepartidorQuery, IReadOnlyList<MotivoNoEntregaDto>>
{
    public async Task<IReadOnlyList<MotivoNoEntregaDto>> Handle(ListarMotivosParaRepartidorQuery request, CancellationToken cancellationToken) =>
        (await motivos.ListarAsync(tenant.OperadorId, true, cancellationToken)).Select(OperadorMapper.ToDto).ToList();
}

public class ObtenerMotivoNoEntregaQueryHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant tenant)
    : IRequestHandler<ObtenerMotivoNoEntregaQuery, MotivoNoEntregaDto>
{
    public async Task<MotivoNoEntregaDto> Handle(ObtenerMotivoNoEntregaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MotivoId == Guid.Empty)
            throw new DomainValidationException("El identificador del motivo es inválido.");

        var motivo = await motivos.GetByIdAsync(request.MotivoId, cancellationToken)
            ?? throw new NotFoundException($"Motivo '{request.MotivoId}' no encontrado.");
        if (motivo.OperadorId != tenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el motivo no corresponde al operador actual.");

        return OperadorMapper.ToDto(motivo);
    }
}
