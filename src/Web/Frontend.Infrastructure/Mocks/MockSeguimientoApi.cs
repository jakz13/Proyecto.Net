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
    private readonly MockContextoOperador _contextoOperador;

    public MockSeguimientoApi(MockContextoOperador? contextoOperador = null)
    {
        _contextoOperador = contextoOperador ?? new MockContextoOperador();
    }
    private static readonly Dictionary<string, SeguimientoPublicoDto> EnviosPublicos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRK-2026-0001"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0001",
            EstadoActual = EstadoEnvioDto.Entregado,
            VentanaHorariaEstimada = "Entregado a las 11:20 hs",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-1),
            PermiteReprogramacion = false,
            TotalBultos = 1,
            BultosEntregados = 1,
            BultosPendientes = 0,
            AvanceEntregaBultos = "1 de 1 bultos entregados",
            OperadorNombre = "LittleBox Operaciones",
            OperadorColorPrimario = "#1D4ED8",
            OperadorColorSecundario = "#F97316",
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
            TotalBultos = 2,
            BultosEntregados = 0,
            BultosPendientes = 2,
            AvanceEntregaBultos = "0 de 2 bultos entregados",
            OperadorNombre = "Uruguay Cargo Express",
            OperadorColorPrimario = "#7C3AED",
            OperadorColorSecundario = "#10B981",
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
            TotalBultos = 1,
            BultosEntregados = 0,
            BultosPendientes = 1,
            AvanceEntregaBultos = "0 de 1 bultos entregados",
            OperadorNombre = "LittleBox Operaciones",
            OperadorColorPrimario = "#1D4ED8",
            OperadorColorSecundario = "#F97316",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddDays(-2), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden registrada." },
                new() { Fecha = DateTime.UtcNow.AddDays(-1), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Procesado en depósito." },
                new() { Fecha = DateTime.UtcNow.AddHours(-8), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "En reparto." },
                new() { Fecha = DateTime.UtcNow.AddHours(-5), Estado = EstadoEnvioDto.NoEntregado, DescripcionPublica = "No fue posible concretar la entrega (destinatario no respondió)." }
            }
        },
        ["TRK-2026-0003"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0003",
            EstadoActual = EstadoEnvioDto.Admitido,
            VentanaHorariaEstimada = "Fecha estimada de entrega en coordinación",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-2),
            PermiteReprogramacion = false,
            OperadorNombre = "Uruguay Cargo Express",
            OperadorColorPrimario = "#7C3AED",
            OperadorColorSecundario = "#10B981",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-2), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Paquete admitido y registrado en la tienda emisora." }
            }
        },
        ["TRK-2026-0004"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0004",
            EstadoActual = EstadoEnvioDto.EnDeposito,
            VentanaHorariaEstimada = "Mañana entre 09:00 y 13:00 hs",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-1),
            PermiteReprogramacion = false,
            OperadorNombre = "LittleBox Operaciones",
            OperadorColorPrimario = "#1D4ED8",
            OperadorColorSecundario = "#F97316",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-6), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden recibida en el sistema." },
                new() { Fecha = DateTime.UtcNow.AddHours(-1), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Paquete ingresado y clasificado en centro logístico central." }
            }
        },
        ["TRK-2026-0005"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "TRK-2026-0005",
            EstadoActual = EstadoEnvioDto.EnDevolucion,
            VentanaHorariaEstimada = "Devolución en curso hacia el comercio de origen",
            FechaUltimaActualizacion = DateTime.UtcNow.AddHours(-3),
            PermiteReprogramacion = false,
            OperadorNombre = "Uruguay Cargo Express",
            OperadorColorPrimario = "#7C3AED",
            OperadorColorSecundario = "#10B981",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddDays(-3), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Orden admitida en el sistema." },
                new() { Fecha = DateTime.UtcNow.AddDays(-2), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Procesado en centro logístico." },
                new() { Fecha = DateTime.UtcNow.AddDays(-1), Estado = EstadoEnvioDto.NoEntregado, DescripcionPublica = "Intentos de entrega agotados sin respuesta del destinatario." },
                new() { Fecha = DateTime.UtcNow.AddHours(-3), Estado = EstadoEnvioDto.EnDevolucion, DescripcionPublica = "Paquete reasignado a devolución hacia el remitente." }
            }
        },
        ["ENV-2026-00108"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "ENV-2026-00108",
            EstadoActual = EstadoEnvioDto.Entregado,
            VentanaHorariaEstimada = "Entregado a las 10:45 hs",
            FechaUltimaActualizacion = DateTime.UtcNow,
            PermiteReprogramacion = false,
            TotalBultos = 2,
            BultosEntregados = 2,
            BultosPendientes = 0,
            AvanceEntregaBultos = "2 de 2 bultos entregados",
            OperadorNombre = "LittleBox Operaciones",
            OperadorColorPrimario = "#1D4ED8",
            OperadorColorSecundario = "#F97316",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-4), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Envío registrado y admitido" },
                new() { Fecha = DateTime.UtcNow.AddHours(-3), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Envío recibido en el depósito" },
                new() { Fecha = DateTime.UtcNow.AddHours(-2), Estado = EstadoEnvioDto.AsignadoARuta, DescripcionPublica = "Envío asignado a una ruta de distribución" },
                new() { Fecha = DateTime.UtcNow.AddHours(-1), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "El envío salió a reparto" },
                new() { Fecha = DateTime.UtcNow, Estado = EstadoEnvioDto.Entregado, DescripcionPublica = "Envío entregado" }
            }
        },
        ["ENV-2026-00110"] = new SeguimientoPublicoDto
        {
            CodigoSeguimiento = "ENV-2026-00110",
            EstadoActual = EstadoEnvioDto.EntregadoParcialmente,
            VentanaHorariaEstimada = "1 bulto pendiente programado para próxima visita",
            FechaUltimaActualizacion = DateTime.UtcNow,
            PermiteReprogramacion = true,
            TotalBultos = 3,
            BultosEntregados = 2,
            BultosPendientes = 1,
            AvanceEntregaBultos = "2 de 3 bultos entregados",
            HistorialEventos = new List<HitoSeguimientoDto>
            {
                new() { Fecha = DateTime.UtcNow.AddHours(-4), Estado = EstadoEnvioDto.Admitido, DescripcionPublica = "Envío registrado y admitido" },
                new() { Fecha = DateTime.UtcNow.AddHours(-3), Estado = EstadoEnvioDto.EnDeposito, DescripcionPublica = "Envío recibido en el depósito" },
                new() { Fecha = DateTime.UtcNow.AddHours(-2), Estado = EstadoEnvioDto.AsignadoARuta, DescripcionPublica = "Envío asignado a una ruta de distribución" },
                new() { Fecha = DateTime.UtcNow.AddHours(-1), Estado = EstadoEnvioDto.EnTransito, DescripcionPublica = "El envío salió a reparto" },
                new() { Fecha = DateTime.UtcNow, Estado = EstadoEnvioDto.EntregadoParcialmente, DescripcionPublica = "Se entregaron 2 de 3 bultos. 1 bulto pendiente para próxima visita." }
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
            var operador = _contextoOperador.OperadorActivo;
            if (operador != null)
            {
                seguimiento = seguimiento with
                {
                    OperadorNombre = operador.NombreComercial,
                    OperadorColorPrimario = operador.ColorPrimario,
                    OperadorColorSecundario = operador.ColorSecundario,
                    OperadorLogoUrl = operador.LogoUrl
                };
            }

            return Task.FromResult<SeguimientoPublicoDto?>(seguimiento);
        }

        return Task.FromResult<SeguimientoPublicoDto?>(null);
    }

    private static readonly List<FranjaDisponibleDto> FranjasMock = new()
    {
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Mañana", "09:00", "13:00", false),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mañana", "14:00", "18:00", false),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Pasado mañana", "09:00", "13:00", false),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Pasado mañana", "14:00", "18:00", false)
    };

    public Task<FranjasDisponiblesDto?> ObtenerFranjasDisponiblesAsync(string codigoSeguimiento, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigoSeguimiento?.Trim().ToUpperInvariant() ?? string.Empty;

        if (EnviosPublicos.TryGetValue(codigoLimpio, out var seguimiento))
        {
            var puede = seguimiento.PermiteReprogramacion || seguimiento.EstadoActual == EstadoEnvioDto.NoEntregado;
            return Task.FromResult<FranjasDisponiblesDto?>(new FranjasDisponiblesDto(puede, FranjasMock));
        }

        return Task.FromResult<FranjasDisponiblesDto?>(null);
    }

    public Task<ResultadoReprogramacionDto> SolicitarReprogramacionAsync(string codigoSeguimiento, Guid franjaHorariaId, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigoSeguimiento?.Trim().ToUpperInvariant() ?? string.Empty;

        if (!EnviosPublicos.TryGetValue(codigoLimpio, out var seguimiento))
        {
            return Task.FromResult(new ResultadoReprogramacionDto(false, "Rechazada", "No se encontró el envío indicado.", null));
        }

        if (seguimiento.EstadoActual == EstadoEnvioDto.EnTransito || seguimiento.EstadoActual == EstadoEnvioDto.AsignadoARuta)
        {
            return Task.FromResult(new ResultadoReprogramacionDto(
                false,
                "Rechazada",
                "El envío ya se encuentra en hoja de ruta despachada: no admite cambios en este momento.",
                null));
        }

        var franjaElegida = FranjasMock.FirstOrDefault(f => f.Id == franjaHorariaId)
            ?? new FranjaDisponibleDto(franjaHorariaId, "Próximo día hábil", "14:00", "18:00", false);

        var descripcionFranja = $"{franjaElegida.DiaSemana} de {franjaElegida.HoraInicio} a {franjaElegida.HoraFin} hs";

        var nuevosHitos = new List<HitoSeguimientoDto>(seguimiento.HistorialEventos)
        {
            new()
            {
                Fecha = DateTime.UtcNow,
                Estado = EstadoEnvioDto.Reprogramado,
                DescripcionPublica = $"Entrega reprogramada para {descripcionFranja} a solicitud del destinatario."
            }
        };

        var actualizado = seguimiento with
        {
            EstadoActual = EstadoEnvioDto.Reprogramado,
            VentanaHorariaEstimada = $"Reprogramado: {descripcionFranja}",
            FechaUltimaActualizacion = DateTime.UtcNow,
            PermiteReprogramacion = false,
            HistorialEventos = nuevosHitos
        };

        EnviosPublicos[codigoLimpio] = actualizado;

        return Task.FromResult(new ResultadoReprogramacionDto(
            true,
            "Aprobada",
            null,
            descripcionFranja));
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
