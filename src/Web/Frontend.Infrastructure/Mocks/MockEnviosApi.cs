using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de gestión de envíos (UC-24, UC-28).
/// </summary>
public class MockEnviosApi : IEnviosApi
{
    private static readonly List<EnvioDto> EnviosEnMemoria = new()
    {
        new EnvioDto
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CodigoSeguimiento = "TRK-2026-0001",
            NombreDestinatario = "Lucía Fernández",
            DireccionDestino = "Bvar. Artigas 1234 Apto 302, Montevideo",
            TelefonoDestinatario = "099 876 543",
            FechaCreacion = DateTime.UtcNow.AddHours(-36),
            Estado = EstadoEnvioDto.Entregado,
            Observaciones = "Entregar en portería si no responde",
            PesoKg = 1.8m,
            VolumenM3 = 0.012m,
            SelloModificacion = "sello-envio-1"
        },
        new EnvioDto
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CodigoSeguimiento = "TRK-2026-0002",
            NombreDestinatario = "Carlos Rodríguez",
            DireccionDestino = "Av. Rivera 2890, Montevideo",
            TelefonoDestinatario = "098 111 222",
            FechaCreacion = DateTime.UtcNow.AddHours(-18),
            Estado = EstadoEnvioDto.EnTransito,
            Observaciones = "Timbre no funciona, llamar al celular",
            PesoKg = 3.5m,
            VolumenM3 = 0.025m,
            SelloModificacion = "sello-envio-2"
        },
        new EnvioDto
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            CodigoSeguimiento = "TRK-2026-0003",
            NombreDestinatario = "Mariana González",
            DireccionDestino = "Colonia 1540 Apto 7, Montevideo",
            TelefonoDestinatario = "091 444 555",
            FechaCreacion = DateTime.UtcNow.AddHours(-8),
            Estado = EstadoEnvioDto.AsignadoARuta,
            Observaciones = null,
            PesoKg = 0.6m,
            VolumenM3 = 0.005m,
            SelloModificacion = "sello-envio-3"
        },
        new EnvioDto
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CodigoSeguimiento = "TRK-2026-0004",
            NombreDestinatario = "Andrés Méndez",
            DireccionDestino = "Camino Carrasco 6120, Canelones",
            TelefonoDestinatario = "094 888 999",
            FechaCreacion = DateTime.UtcNow.AddHours(-4),
            Estado = EstadoEnvioDto.EnDeposito,
            Observaciones = "Paquete frágil",
            PesoKg = 5.2m,
            VolumenM3 = 0.040m,
            SelloModificacion = "sello-envio-4"
        },
        new EnvioDto
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            CodigoSeguimiento = "TRK-2026-0005",
            NombreDestinatario = "Florencia Silva",
            DireccionDestino = "Sarandí 450, Ciudad Vieja, Montevideo",
            TelefonoDestinatario = "092 654 321",
            FechaCreacion = DateTime.UtcNow.AddMinutes(-45),
            Estado = EstadoEnvioDto.Admitido,
            Observaciones = "Horario de recepción comercial 9 a 18",
            PesoKg = 1.1m,
            VolumenM3 = 0.008m,
            SelloModificacion = "sello-envio-5"
        },
        new EnvioDto
        {
            Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            CodigoSeguimiento = "TRK-2026-0006",
            NombreDestinatario = "Martín Olivera",
            DireccionDestino = "Av. Giannattasio km 21, Ciudad de la Costa",
            TelefonoDestinatario = "099 900 100",
            FechaCreacion = DateTime.UtcNow.AddDays(-2),
            Estado = EstadoEnvioDto.NoEntregado,
            Observaciones = "Destinatario ausente en primer intento",
            PesoKg = 2.0m,
            VolumenM3 = 0.015m,
            SelloModificacion = "sello-envio-6"
        }
    };

    private static int _contadorSecuencial = 7;

    public Task<ResultadoPaginadoDto<EnvioDto>> ListarEnviosAsync(FiltroEnviosDto filtro, CancellationToken cancelacion = default)
    {
        var consulta = EnviosEnMemoria.AsQueryable();

        if (filtro.Estado.HasValue)
        {
            consulta = consulta.Where(e => e.Estado == filtro.Estado.Value);
        }

        if (filtro.FechaDesde.HasValue)
        {
            consulta = consulta.Where(e => e.FechaCreacion >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            var finDia = filtro.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
            consulta = consulta.Where(e => e.FechaCreacion <= finDia);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TerminoBusqueda))
        {
            var termino = filtro.TerminoBusqueda.Trim().ToLowerInvariant();
            consulta = consulta.Where(e =>
                e.CodigoSeguimiento.ToLowerInvariant().Contains(termino) ||
                e.NombreDestinatario.ToLowerInvariant().Contains(termino) ||
                e.DireccionDestino.ToLowerInvariant().Contains(termino));
        }

        var paginaSegura = Math.Max(1, filtro.Pagina);
        var tamanoSeguro = Math.Max(1, filtro.TamanoPagina);

        var totalRegistros = consulta.Count();
        var elementos = consulta
            .OrderByDescending(e => e.FechaCreacion)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToList();

        var resultado = new ResultadoPaginadoDto<EnvioDto>
        {
            Elementos = elementos,
            TotalRegistros = totalRegistros,
            PaginaActual = paginaSegura,
            TamanoPagina = tamanoSeguro
        };

        return Task.FromResult(resultado);
    }

    public Task<EnvioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var envio = EnviosEnMemoria.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(envio);
    }

    public Task<EnvioDto> CrearEnvioAsync(CrearEnvioDto nuevoEnvio, CancellationToken cancelacion = default)
    {
        var nuevoId = Guid.NewGuid();
        var codigoSeguimiento = $"TRK-2026-{Interlocked.Increment(ref _contadorSecuencial):D4}";

        var entidad = new EnvioDto
        {
            Id = nuevoId,
            CodigoSeguimiento = codigoSeguimiento,
            NombreDestinatario = nuevoEnvio.NombreDestinatario.Trim(),
            DireccionDestino = nuevoEnvio.DireccionDestino.Trim(),
            TelefonoDestinatario = nuevoEnvio.TelefonoDestinatario?.Trim(),
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoEnvioDto.Admitido,
            Observaciones = nuevoEnvio.Observaciones?.Trim(),
            PesoKg = nuevoEnvio.PesoKg,
            VolumenM3 = nuevoEnvio.VolumenM3,
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        EnviosEnMemoria.Insert(0, entidad);
        return Task.FromResult(entidad);
    }
}
