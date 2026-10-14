using System.Net;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del cuadro tarifario y sus versiones (UC-03, RF-03).
/// Gestiona la versión vigente activa y conserva el historial con sus rangos de vigencia cerrados.
/// </summary>
public class MockCuadroTarifarioApi : ICuadroTarifarioApi
{
    private static readonly List<CuadroTarifarioDto> VersionesEnMemoria = new()
    {
        // Versión 2: Actualmente Vigente (VigenteHasta == null)
        new CuadroTarifarioDto
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999992"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = 2,
            VigenteDesde = DateTime.UtcNow.AddDays(-14),
            VigenteHasta = null,
            Comentario = "Ajuste operativo de primavera con bonificaciones para zona céntrica",
            ReglasTarifa = new List<ReglaTarifaDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Estándar 24-48h",
                    PesoDesde = 0,
                    PesoHasta = 5,
                    VolumenDesde = 0,
                    VolumenHasta = 0.05m,
                    PrecioBase = 180,
                    Recargo = 0,
                    Bonificacion = 0
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Estándar 24-48h",
                    PesoDesde = 5,
                    PesoHasta = 20,
                    VolumenDesde = 0.05m,
                    VolumenHasta = 0.20m,
                    PrecioBase = 320,
                    Recargo = 0,
                    Bonificacion = 0
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Express en el Día",
                    PesoDesde = 0,
                    PesoHasta = 5,
                    VolumenDesde = 0,
                    VolumenHasta = 0.05m,
                    PrecioBase = 250,
                    Recargo = 50,
                    Bonificacion = 0
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Express en el Día",
                    PesoDesde = 5,
                    PesoHasta = 20,
                    VolumenDesde = 0.05m,
                    VolumenHasta = 0.20m,
                    PrecioBase = 420,
                    Recargo = 80,
                    Bonificacion = 0
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    NombreZona = "Montevideo Centro & Sur",
                    ModalidadServicio = "Mismo Día",
                    PesoDesde = 0,
                    PesoHasta = 10,
                    VolumenDesde = 0,
                    VolumenHasta = 0.10m,
                    PrecioBase = 290,
                    Recargo = 40,
                    Bonificacion = 20
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Carga Pesada",
                    PesoDesde = 20,
                    PesoHasta = 100,
                    VolumenDesde = 0.20m,
                    VolumenHasta = 1.00m,
                    PrecioBase = 650,
                    Recargo = 120,
                    Bonificacion = 0
                }
            }
        },
        // Versión 1: Histórica (VigenteHasta cerrado)
        new CuadroTarifarioDto
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999991"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = 1,
            VigenteDesde = DateTime.UtcNow.AddMonths(-6),
            VigenteHasta = DateTime.UtcNow.AddDays(-14),
            Comentario = "Tarifario de lanzamiento de operaciones logísticas",
            ReglasTarifa = new List<ReglaTarifaDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999991"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Estándar 24-48h",
                    PesoDesde = 0,
                    PesoHasta = 5,
                    VolumenDesde = 0,
                    VolumenHasta = 0.05m,
                    PrecioBase = 150,
                    Recargo = 0,
                    Bonificacion = 0
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999991"),
                    ZonaCoberturaId = null,
                    NombreZona = "Todas las zonas",
                    ModalidadServicio = "Express en el Día",
                    PesoDesde = 0,
                    PesoHasta = 5,
                    VolumenDesde = 0,
                    VolumenHasta = 0.05m,
                    PrecioBase = 220,
                    Recargo = 30,
                    Bonificacion = 0
                }
            }
        }
    };

    public Task<IReadOnlyList<CuadroTarifarioDto>> ListarVersionesAsync(CancellationToken cancelacion = default)
    {
        var ordenadas = VersionesEnMemoria.OrderByDescending(v => v.Version).ToList();
        return Task.FromResult<IReadOnlyList<CuadroTarifarioDto>>(ordenadas);
    }

    public Task<CuadroTarifarioDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default)
    {
        var vigente = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        return Task.FromResult(vigente);
    }

    public Task<CuadroTarifarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var cuadro = VersionesEnMemoria.FirstOrDefault(v => v.Id == id);
        return Task.FromResult(cuadro);
    }

    public Task<CuadroTarifarioDto> PublicarNuevaVersionAsync(CrearVersionCuadroTarifarioDto nuevaVersion, CancellationToken cancelacion = default)
    {
        if (nuevaVersion.ReglasTarifa == null || nuevaVersion.ReglasTarifa.Count == 0)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El nuevo cuadro tarifario debe contener al menos una regla de precio.");
        }

        var ahora = DateTime.UtcNow;

        // 1. Cerrar vigencia de la versión actual si está activa
        var versionVigenteActual = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        if (versionVigenteActual != null)
        {
            var indice = VersionesEnMemoria.IndexOf(versionVigenteActual);
            VersionesEnMemoria[indice] = versionVigenteActual with
            {
                VigenteHasta = ahora
            };
        }

        // 2. Determinar nuevo número de versión secuencial
        var siguienteNumeroVersion = VersionesEnMemoria.Count > 0
            ? VersionesEnMemoria.Max(v => v.Version) + 1
            : 1;

        var nuevoId = Guid.NewGuid();

        // 3. Crear DTOs de las reglas asignadas
        var reglasDto = nuevaVersion.ReglasTarifa.Select(r => new ReglaTarifaDto
        {
            Id = Guid.NewGuid(),
            CuadroTarifarioId = nuevoId,
            ZonaCoberturaId = r.ZonaCoberturaId,
            NombreZona = r.ZonaCoberturaId.HasValue ? "Zona Específica" : "Todas las zonas",
            ModalidadServicio = r.ModalidadServicio.Trim(),
            PesoDesde = r.PesoDesde,
            PesoHasta = r.PesoHasta,
            VolumenDesde = r.VolumenDesde,
            VolumenHasta = r.VolumenHasta,
            PrecioBase = r.PrecioBase,
            Recargo = r.Recargo,
            Bonificacion = r.Bonificacion
        }).ToList();

        var nuevoCuadro = new CuadroTarifarioDto
        {
            Id = nuevoId,
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Version = siguienteNumeroVersion,
            VigenteDesde = ahora,
            VigenteHasta = null,
            Comentario = nuevaVersion.Comentario?.Trim(),
            ReglasTarifa = reglasDto
        };

        VersionesEnMemoria.Insert(0, nuevoCuadro);
        return Task.FromResult(nuevoCuadro);
    }

    public Task<CotizacionSimuladaDto> SimularTarifaAsync(SimularTarifaDto datosEnvio, CancellationToken cancelacion = default)
    {
        var vigente = VersionesEnMemoria.FirstOrDefault(v => v.EsVigente);
        if (vigente == null || vigente.ReglasTarifa.Count == 0)
        {
            return Task.FromResult(new CotizacionSimuladaDto
            {
                Exitoso = false,
                Mensaje = "No hay ningún cuadro tarifario vigente disponible para calcular precios."
            });
        }

        // Búsqueda de regla coincidente: primero busca regla específica de zona, si no aplica regla general (ZonaCoberturaId == null)
        var reglaCoincidente = vigente.ReglasTarifa
            .Where(r => string.Equals(r.ModalidadServicio, datosEnvio.ModalidadServicio, StringComparison.OrdinalIgnoreCase))
            .Where(r => datosEnvio.PesoKg >= r.PesoDesde && datosEnvio.PesoKg <= r.PesoHasta)
            .Where(r => datosEnvio.VolumenM3 >= r.VolumenDesde && datosEnvio.VolumenM3 <= r.VolumenHasta)
            .OrderByDescending(r => r.ZonaCoberturaId.HasValue && r.ZonaCoberturaId == datosEnvio.ZonaCoberturaId ? 2 : (r.ZonaCoberturaId == null ? 1 : 0))
            .FirstOrDefault();

        if (reglaCoincidente != null)
        {
            return Task.FromResult(new CotizacionSimuladaDto
            {
                Exitoso = true,
                PrecioFinal = reglaCoincidente.PrecioFinal,
                PrecioBase = reglaCoincidente.PrecioBase,
                Recargo = reglaCoincidente.Recargo,
                Bonificacion = reglaCoincidente.Bonificacion,
                VersionCuadroAplicada = vigente.Version,
                ReglaAplicada = $"{reglaCoincidente.ModalidadServicio} ({reglaCoincidente.NombreZona}) — Rango {reglaCoincidente.PesoDesde}kg a {reglaCoincidente.PesoHasta}kg",
                Mensaje = "Tarifa calculada con éxito bajo la versión vigente."
            });
        }

        // Si no hay regla exacta, aplica cotización estimada base
        var tarifaAproximada = 180 + (datosEnvio.PesoKg * 15);
        return Task.FromResult(new CotizacionSimuladaDto
        {
            Exitoso = true,
            PrecioFinal = tarifaAproximada,
            PrecioBase = tarifaAproximada,
            Recargo = 0,
            Bonificacion = 0,
            VersionCuadroAplicada = vigente.Version,
            ReglaAplicada = "Tarifa base complementaria por exceso o fuera de rango",
            Mensaje = "Tarifa complementaria aplicada por no encontrar regla exacta de peso/volumen."
        });
    }
}
