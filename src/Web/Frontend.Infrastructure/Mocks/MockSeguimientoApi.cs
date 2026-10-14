using System.Net;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada del servicio de seguimiento público anónimo (UC-33) y reprogramación (UC-34).
/// Simula códigos conocidos, códigos desconocidos (anti-enumeración) y límite de tasa (HTTP 429).
/// </summary>
public class MockSeguimientoApi : ISeguimientoApi
{
    private static readonly Dictionary<string, SeguimientoPublicoDto> EnviosPublicos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRK-2026-0001"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0001",
            EstadoActual = EstadoEnvioDto.Entregado,
            VentanaHorariaEstimada = "Entregado a las 11:20 hs",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-1),
            PermiteReprogramacion = false,
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-36), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden recibida en el sistema logístico." },
                new() { Fecha = DateTime.UtcNow.AddHours(-24), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Paquete clasificado en centro de distribución." },
                new() { Fecha = DateTime.UtcNow.AddHours(-6), Estado = EstadoEnvioDto.AsignadoARuta, DescripcionPublica = "Paquete incluido en la hoja de ruta del repartidor." },
                new() { Fecha = DateTime.UtcNow.AddHours(-3), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "El repartidor está en camino a su dirección." },
                new() { Fecha = DateTime.UtcNow.AddHours(-1), Estado = EstadoEnvioDto.Entregado, DescripcionPublica = "Entrega completada exitosamente." }
            }
        },
        ["TRK-2026-0002"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0002",
            EstadoActual = EstadoEnvioDto.EnTransito,
            VentanaHorariaEstimada = "Hoy entre 15:30 y 18:30 hs",
            FechaUltimaActualizacion = DateTime.UtcNow.AddMinutes(-30),
            PermiteReprogramacion = false,
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-18), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden recibida en el sistema." },
                new() { Fecha = DateTime.UtcNow.AddHours(-10), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Ingreso a depósito central verificado." },
                new() { Fecha = DateTime.UtcNow.AddHours(-2), Estado = EstadoEnvioDto.AsignadoARuta, DescripcionPublica = "Hoja de ruta despachada." },
                new() { Fecha = DateTime.UtcNow.AddMinutes(-30), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "En camino hacia su domicilio." }
            }
        },
        ["TRK-2026-0006"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0006",
            EstadoActual = EstadoEnvioDto.NoEntregado,
            VentanaHorariaEstimada = "Próximo intento coordinado para mañana",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-5),
            PermiteReprogramacion = true,
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddDays(-2), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden registrada." },
                new() { Fecha = DateTime.UtcNow.AddDays(-1), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Procesado en depósito." },
                new() { Fecha = DateTime.UtcNow.AddHours(-8), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "En reparto." },
                new() { Fecha = DateTime.UtcNow.AddHours(-5), Estado = EstadoEnvioDto.NoEntregado, DescripcionPublica = "No fue posible concretar la entrega (destinatario no respondió)." }
            }
        }
    };

    public Task<SeguimientoPublicoDto?> ConsultarSeguimientoAsync(string codigoSeguimiento, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigoSeguimiento?.Trim().ToUpperInvariant() ?? string.Empty;

        // Código especial de prueba para simular límite de tasa en la defensa oral (HTTP 429)
        if (codigoLimpio == "TRK-TEST-429")
        {
            throw new ApiExcepcion(
                (HttpStatusCode)429,
                "Demasiadas consultas en poco tiempo. Por favor aguarde unos instantes antes de reintentar."
            );
        }

        if (EnviosPublicos.TryGetValue(codigoLimpio, out var seguimiento))
        {
            return Task.FromResult<SeguimientoPublicoDto?>(seguimiento);
        }

        // Si el código no existe o no se encuentra, retorna null.
        // La UI mostrará el mismo mensaje genérico para evitar enumeración de códigos de envío.
        return Task.FromResult<SeguimientoPublicoDto?>(null);
    }

    public Task<bool> SolicitarReprogramacionAsync(string codigoSeguimiento, DateTime nuevaFecha, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigoSeguimiento?.Trim().ToUpperInvariant() ?? string.Empty;

        if (EnviosPublicos.TryGetValue(codigoLimpio, out var seguimiento))
        {
            var nuevosHitos = new List<HitoSeguimientoDto>(seguimiento.HistorialEventos)
            {
                new()
                {
                    Fecha = DateTime.UtcNow,
                    Estado = EstadoEnvioDto.Reprogramado,
                    DescripcionPublica = $"Entrega reprogramada para el {nuevaFecha:dd/MM/yyyy} a solicitud del destinatario."
                }
            };

            var actualizado = seguimiento with
            {
                EstadoActual = EstadoEnvioDto.Reprogramado,
                VentanaHorariaEstimada = $"Reprogramado para el {nuevaFecha:dd/MM/yyyy}",
                FechaUltimaActualizacion = DateTime.UtcNow,
                PermiteReprogramacion = false,
                HistorialEventos = nuevosHitos
            };

            EnviosPublicos[codigoLimpio] = actualizado;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
