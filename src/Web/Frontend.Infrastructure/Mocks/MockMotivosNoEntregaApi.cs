using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para la gestión del catálogo de motivos de no entrega (UC-05, RF-04, RF-21).
/// Permite probar altas, ediciones, bajas lógicas y simulación de sincronización hacia la app móvil sin requerir backend.
/// </summary>
public class MockMotivosNoEntregaApi : IMotivosNoEntregaApi
{
    private readonly List<MotivoNoEntregaDto> _motivos;
    private readonly object _bloqueo = new();
    private DateTime _ultimaSincronizacion = DateTime.UtcNow.AddMinutes(-12);

    public MockMotivosNoEntregaApi()
    {
        _motivos = new List<MotivoNoEntregaDto>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                Codigo = "DESTINATARIO_AUSENTE",
                Descripcion = "No responde al llamado ni se encuentra persona habilitada en el domicilio.",
                Activo = true,
                FechaAlta = DateTime.UtcNow.AddDays(-45),
                CantidadUsos = 34
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                Codigo = "DIRECCION_ERRONEA",
                Descripcion = "La numeración, calle o datos de entrega no corresponden a la zona o no existen.",
                Activo = true,
                FechaAlta = DateTime.UtcNow.AddDays(-40),
                CantidadUsos = 18
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                Codigo = "RECHAZADO_CLIENTE",
                Descripcion = "El destinatario rechaza la recepción del paquete por producto erróneo o decisión propia.",
                Activo = true,
                FechaAlta = DateTime.UtcNow.AddDays(-30),
                CantidadUsos = 12
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                Codigo = "ZONA_INACCESIBLE",
                Descripcion = "Fuerza mayor, corte vial o impedimento geográfico insalvable para el transporte.",
                Activo = true,
                FechaAlta = DateTime.UtcNow.AddDays(-25),
                CantidadUsos = 5
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                Codigo = "CLIENTE_PIDE_REPROGRAMAR",
                Descripcion = "El receptor contacta al repartidor solicitando diferir la visita para otra franja horaria.",
                Activo = true,
                FechaAlta = DateTime.UtcNow.AddDays(-15),
                CantidadUsos = 21
            },
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                Codigo = "PLAZO_VENCIDO_DEPOSITO",
                Descripcion = "Superó el tiempo máximo de custodia en depósito sin instrucción de entrega.",
                Activo = false,
                FechaAlta = DateTime.UtcNow.AddDays(-60),
                CantidadUsos = 3
            }
        };
    }

    public Task<IReadOnlyList<MotivoNoEntregaDto>> ListarMotivosAsync(FiltroMotivosNoEntregaDto? filtro = null, CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var consulta = _motivos.AsEnumerable();

            if (filtro != null)
            {
                if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
                {
                    var termino = filtro.Busqueda.Trim().ToLowerInvariant();
                    consulta = consulta.Where(m =>
                        m.Codigo.ToLowerInvariant().Contains(termino) ||
                        m.Descripcion.ToLowerInvariant().Contains(termino));
                }

                if (filtro.SoloActivos.HasValue)
                {
                    consulta = consulta.Where(m => m.Activo == filtro.SoloActivos.Value);
                }
            }

            // Orden: Activos primero, luego por cantidad de usos descendente
            var resultado = consulta
                .OrderByDescending(m => m.Activo)
                .ThenByDescending(m => m.CantidadUsos)
                .ThenBy(m => m.Codigo)
                .ToList();

            return Task.FromResult<IReadOnlyList<MotivoNoEntregaDto>>(resultado);
        }
    }

    public Task<MotivoNoEntregaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var motivo = _motivos.FirstOrDefault(m => m.Id == id);
            return Task.FromResult(motivo);
        }
    }

    public Task<MotivoNoEntregaDto> CrearMotivoAsync(CrearMotivoNoEntregaDto dto, CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var codigoNormalizado = dto.Codigo.Trim().ToUpperInvariant();

            if (_motivos.Any(m => m.Codigo.Equals(codigoNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion(System.Net.HttpStatusCode.BadRequest, $"Ya existe un motivo registrado con el código '{codigoNormalizado}'.");
            }

            var nuevo = new MotivoNoEntregaDto
            {
                Id = Guid.NewGuid(),
                Codigo = codigoNormalizado,
                Descripcion = dto.Descripcion.Trim(),
                Activo = dto.IniciarActivo,
                FechaAlta = DateTime.UtcNow,
                CantidadUsos = 0
            };

            _motivos.Insert(0, nuevo);
            _ultimaSincronizacion = DateTime.UtcNow;

            return Task.FromResult(nuevo);
        }
    }

    public Task<MotivoNoEntregaDto> ActualizarMotivoAsync(ActualizarMotivoNoEntregaDto dto, CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var motivo = _motivos.FirstOrDefault(m => m.Id == dto.Id);
            if (motivo == null)
            {
                throw new ApiExcepcion(System.Net.HttpStatusCode.NotFound, $"No se encontró el motivo con identificador {dto.Id}.");
            }

            motivo.Descripcion = dto.Descripcion.Trim();
            motivo.Activo = dto.Activo;
            _ultimaSincronizacion = DateTime.UtcNow;

            return Task.FromResult(motivo);
        }
    }

    public Task CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var motivo = _motivos.FirstOrDefault(m => m.Id == id);
            if (motivo == null)
            {
                throw new ApiExcepcion(System.Net.HttpStatusCode.NotFound, $"No se encontró el motivo con identificador {id}.");
            }

            motivo.Activo = activo;
            _ultimaSincronizacion = DateTime.UtcNow;

            return Task.CompletedTask;
        }
    }

    public Task<ResumenMotivosNoEntregaDto> ObtenerResumenAsync(CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var total = _motivos.Count;
            var activos = _motivos.Count(m => m.Activo);
            var inactivos = total - activos;

            var resumen = new ResumenMotivosNoEntregaDto
            {
                TotalMotivos = total,
                MotivosActivosParaMovil = activos,
                MotivosInactivos = inactivos,
                UltimaSincronizacionApp = _ultimaSincronizacion
            };

            return Task.FromResult(resumen);
        }
    }

    public Task<IReadOnlyList<MotivoNoEntregaDto>> ObtenerMotivosParaSincronizacionMovilAsync(CancellationToken cancellationToken = default)
    {
        lock (_bloqueo)
        {
            var activos = _motivos
                .Where(m => m.Activo)
                .OrderBy(m => m.Descripcion)
                .ToList();

            return Task.FromResult<IReadOnlyList<MotivoNoEntregaDto>>(activos);
        }
    }
}
