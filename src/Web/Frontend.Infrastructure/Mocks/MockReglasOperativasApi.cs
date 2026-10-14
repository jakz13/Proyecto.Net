using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para la gestión y versionado de reglas operativas (UC-04, RF-04).
/// </summary>
public class MockReglasOperativasApi : IReglasOperativasApi
{
    private static readonly List<ReglaOperativaDto> VersionesEnMemoria = new()
    {
        // Versión 2: Vigente actual (VigenteHasta == null)
        new ReglaOperativaDto
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888882"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = 2,
            VigenteDesde = DateTime.UtcNow.AddDays(-20),
            VigenteHasta = null,
            MaxIntentosEntrega = 3,
            PlazoEntreIntentos = TimeSpan.FromHours(24),
            TipoPruebaEntregaExigida = "Firma digital del receptor y Foto del paquete en domicilio",
            PoliticaDevolucion = "Custodia por 72 horas en depósito central antes de retorno definitivo al comercio remitente",
            PlazoComprometidoEstandar = TimeSpan.FromHours(48),
            PlazoComprometidoUrgente = TimeSpan.FromHours(12),
            Comentario = "Ajuste de SLA para envíos urgentes y exigencia de doble factor de entrega (firma + foto)"
        },
        // Versión 1: Histórica archivada (VigenteHasta cerrado)
        new ReglaOperativaDto
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888881"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = 1,
            VigenteDesde = DateTime.UtcNow.AddMonths(-5),
            VigenteHasta = DateTime.UtcNow.AddDays(-20),
            MaxIntentosEntrega = 2,
            PlazoEntreIntentos = TimeSpan.FromHours(48),
            TipoPruebaEntregaExigida = "Firma digital simple",
            PoliticaDevolucion = "Devolución automática e inmediata al comercio remitente",
            PlazoComprometidoEstandar = TimeSpan.FromHours(72),
            PlazoComprometidoUrgente = TimeSpan.FromHours(24),
            Comentario = "Reglas operativas base de lanzamiento de operaciones logísticas"
        }
    };

    public Task<IReadOnlyList<ReglaOperativaDto>> ListarVersionesAsync(CancellationToken cancelacion = default)
    {
        var ordenadas = VersionesEnMemoria.OrderByDescending(v => v.Version).ToList();
        return Task.FromResult<IReadOnlyList<ReglaOperativaDto>>(ordenadas);
    }

    public Task<ReglaOperativaDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default)
    {
        var vigente = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        return Task.FromResult(vigente);
    }

    public Task<ReglaOperativaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var regla = VersionesEnMemoria.FirstOrDefault(v => v.Id == id);
        return Task.FromResult(regla);
    }

    public Task<ReglaOperativaDto> PublicarNuevaVersionAsync(CrearReglaOperativaDto nuevaVersion, CancellationToken cancelacion = default)
    {
        var ahora = DateTime.UtcNow;

        // 1. Cerrar vigencia de la versión actual
        var versionVigenteActual = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        if (versionVigenteActual != null)
        {
            var indice = VersionesEnMemoria.IndexOf(versionVigenteActual);
            VersionesEnMemoria[indice] = versionVigenteActual with
            {
                VigenteHasta = ahora
            };
        }

        // 2. Determinar correlativo de versión
        var siguienteVersion = VersionesEnMemoria.Count > 0
            ? VersionesEnMemoria.Max(v => v.Version) + 1
            : 1;

        var nuevaEntidad = new ReglaOperativaDto
        {
            Id = Guid.NewGuid(),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = siguienteVersion,
            VigenteDesde = ahora,
            VigenteHasta = null,
            MaxIntentosEntrega = nuevaVersion.MaxIntentosEntrega,
            PlazoEntreIntentos = TimeSpan.FromHours(nuevaVersion.HorasEntreIntentos),
            TipoPruebaEntregaExigida = nuevaVersion.TipoPruebaEntregaExigida.Trim(),
            PoliticaDevolucion = nuevaVersion.PoliticaDevolucion.Trim(),
            PlazoComprometidoEstandar = TimeSpan.FromHours(nuevaVersion.HorasPlazoComprometidoEstandar),
            PlazoComprometidoUrgente = TimeSpan.FromHours(nuevaVersion.HorasPlazoComprometidoUrgente),
            Comentario = nuevaVersion.Comentario?.Trim()
        };

        VersionesEnMemoria.Insert(0, nuevaEntidad);
        return Task.FromResult(nuevaEntidad);
    }

    public Task<ResultadoSimulacionReglasDto> SimularReglasAsync(SimularReglaOperativaDto datosEnvio, CancellationToken cancelacion = default)
    {
        var vigente = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        if (vigente == null)
        {
            return Task.FromResult(new ResultadoSimulacionReglasDto
            {
                Exitoso = false,
                AccionSugerida = "No hay versión de reglas operativas vigente configurada."
            });
        }

        var esModalidadUrgente = datosEnvio.ModalidadServicio.Contains("Express", StringComparison.OrdinalIgnoreCase) ||
                                 datosEnvio.ModalidadServicio.Contains("Mismo Día", StringComparison.OrdinalIgnoreCase);

        var plazoComprometido = esModalidadUrgente ? vigente.PlazoComprometidoUrgente : vigente.PlazoComprometidoEstandar;
        var fechaLimiteSla = datosEnvio.FechaDespacho.Add(plazoComprometido);
        var slaVencido = DateTime.Now > fechaLimiteSla;

        var intentosRestantes = Math.Max(0, vigente.MaxIntentosEntrega - datosEnvio.IntentosRealizados);
        var superaMaximo = datosEnvio.IntentosRealizados >= vigente.MaxIntentosEntrega;

        DateTime? proximaFecha = null;
        bool puedeReintentarAhora = false;
        string accionSugerida;

        if (superaMaximo)
        {
            accionSugerida = $"Se alcanzó el máximo de {vigente.MaxIntentosEntrega} intentos fallidos. Debe activarse la política de devolución: {vigente.PoliticaDevolucion}.";
        }
        else if (datosEnvio.FechaUltimoIntento.HasValue)
        {
            proximaFecha = datosEnvio.FechaUltimoIntento.Value.Add(vigente.PlazoEntreIntentos);
            if (DateTime.Now >= proximaFecha.Value)
            {
                puedeReintentarAhora = true;
                accionSugerida = $"Habilitado para nuevo intento de entrega ({intentosRestantes} restantes). Se cumplió el plazo de {vigente.HorasEntreIntentos}h entre intentos.";
            }
            else
            {
                puedeReintentarAhora = false;
                accionSugerida = $"En espera reglamentaria. Próximo intento habilitado el {proximaFecha.Value:dd/MM/yyyy HH:mm} (regla de {vigente.HorasEntreIntentos}h entre intentos).";
            }
        }
        else
        {
            puedeReintentarAhora = true;
            accionSugerida = $"Envío en curso para primer intento. Entrega comprometida antes de {fechaLimiteSla:dd/MM/yyyy HH:mm}.";
        }

        return Task.FromResult(new ResultadoSimulacionReglasDto
        {
            Exitoso = true,
            VersionAplicada = vigente.Version,
            MaxIntentosPermitidos = vigente.MaxIntentosEntrega,
            IntentosRestantes = intentosRestantes,
            SuperaMaximoIntentos = superaMaximo,
            ProximaFechaReintentoPermitida = proximaFecha,
            PuedeReintentarAhora = puedeReintentarAhora,
            FechaLimiteSla = fechaLimiteSla,
            SlaVencido = slaVencido,
            TipoPruebaExigida = vigente.TipoPruebaEntregaExigida,
            PoliticaDevolucion = vigente.PoliticaDevolucion,
            AccionSugerida = accionSugerida
        });
    }
}
