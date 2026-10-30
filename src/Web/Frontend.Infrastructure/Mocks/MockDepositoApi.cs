using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de depósito para pruebas locales y demo sin backend (UC-14).
/// </summary>
public class MockDepositoApi : IDepositoApi
{
    private static readonly object Bloqueo = new();

    private static readonly List<EnvioAdmitidoDto> EnviosAdmitidosEnMemoria = new()
    {
        new EnvioAdmitidoDto(
            EnvioId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            CodigoSeguimiento: "TRK-2026-0005",
            NombreDestinatario: "Florencia Silva",
            DireccionDestino: "Sarandí 450, Ciudad Vieja, Montevideo",
            TelefonoDestinatario: "092 654 321",
            ModalidadServicio: "Estandar",
            PesoTotal: 1.1m,
            CantidadBultos: 2,
            FechaAlta: DateTime.UtcNow.AddMinutes(-45),
            Bultos: new List<BultoResumenDto>
            {
                new(Guid.NewGuid(), "BLT-0005-01", 0.6m, 15, 20, 25, "Ingresado"),
                new(Guid.NewGuid(), "BLT-0005-02", 0.5m, 10, 15, 20, "Ingresado")
            }
        ),
        new EnvioAdmitidoDto(
            EnvioId: Guid.Parse("cccccccc-1111-2222-3333-000000000005"),
            CodigoSeguimiento: "ENV-2026-00112",
            NombreDestinatario: "Diego Pereira",
            DireccionDestino: "Bulevar Artigas 1100, Montevideo",
            TelefonoDestinatario: "099 111 555",
            ModalidadServicio: "Estandar",
            PesoTotal: 1.0m,
            CantidadBultos: 1,
            FechaAlta: DateTime.UtcNow.AddMinutes(-30),
            Bultos: new List<BultoResumenDto>
            {
                new(Guid.NewGuid(), "BLT-005-A", 1.0m, 20, 20, 20, "Ingresado")
            }
        ),
        new EnvioAdmitidoDto(
            EnvioId: Guid.Parse("99887766-5544-3322-1100-aabbccddeeff"),
            CodigoSeguimiento: "TRK-2026-0010",
            NombreDestinatario: "Martín Méndez",
            DireccionDestino: "18 de Julio 1420 Apto 501, Centro",
            TelefonoDestinatario: "091 234 567",
            ModalidadServicio: "Express",
            PesoTotal: 3.4m,
            CantidadBultos: 3,
            FechaAlta: DateTime.UtcNow.AddMinutes(-15),
            Bultos: new List<BultoResumenDto>
            {
                new(Guid.NewGuid(), "BLT-0010-01", 1.2m, 25, 25, 30, "Ingresado"),
                new(Guid.NewGuid(), "BLT-0010-02", 1.0m, 20, 20, 25, "Ingresado"),
                new(Guid.NewGuid(), "BLT-0010-03", 1.2m, 25, 25, 30, "Ingresado")
            }
        )
    };

    public Task<IReadOnlyList<EnvioAdmitidoDto>> ListarEnviosAdmitidosAsync(CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            return Task.FromResult<IReadOnlyList<EnvioAdmitidoDto>>(EnviosAdmitidosEnMemoria.ToList());
        }
    }

    public Task<EnvioAdmitidoDto?> ObtenerEnvioParaRecepcionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            var encontrado = EnviosAdmitidosEnMemoria.FirstOrDefault(e => e.EnvioId == envioId);
            return Task.FromResult(encontrado);
        }
    }

    public Task<ResultadoRecepcionDto> RecepcionarBultosAsync(
        Guid envioId,
        RecepcionarBultosRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        lock (Bloqueo)
        {
            var envio = EnviosAdmitidosEnMemoria.FirstOrDefault(e => e.EnvioId == envioId);
            if (envio == null)
            {
                return Task.FromResult(new ResultadoRecepcionDto(
                    envioId,
                    "DESCONOCIDO",
                    "NoEncontrado",
                    false,
                    false,
                    false,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "El envío especificado no fue encontrado entre los arribos admitidos."));
            }

            var escaneados = (solicitud.CodigosBultosEscaneados ?? Array.Empty<string>())
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToHashSet();

            var danados = (solicitud.CodigosBultosDanados ?? Array.Empty<string>())
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToHashSet();

            var declarados = envio.Bultos.Select(b => b.Identificacion.Trim().ToUpperInvariant()).ToHashSet();

            var correctos = declarados.Where(c => escaneados.Contains(c) && !danados.Contains(c)).ToList();
            var faltantes = declarados.Where(c => !escaneados.Contains(c)).ToList();
            var sobrantes = escaneados.Where(c => !declarados.Contains(c)).ToList();
            var danadosFinales = declarados.Where(c => danados.Contains(c) && escaneados.Contains(c)).ToList();

            var hayDiscrepancias = faltantes.Count > 0 || sobrantes.Count > 0 || danadosFinales.Count > 0;
            var observacionesVacias = string.IsNullOrWhiteSpace(solicitud.Observaciones);

            if (hayDiscrepancias && observacionesVacias)
            {
                return Task.FromResult(new ResultadoRecepcionDto(
                    envio.EnvioId,
                    envio.CodigoSeguimiento,
                    "Admitido",
                    Recepcionado: false,
                    Idempotente: false,
                    TieneDiscrepancias: true,
                    correctos,
                    faltantes,
                    sobrantes,
                    danadosFinales,
                    "Se detectaron discrepancias: documente las observaciones para completar la recepción."));
            }

            // Operación exitosa: remover de admitidos y pasar a EnDeposito
            EnviosAdmitidosEnMemoria.Remove(envio);

            var bultosRecibidos = envio.Bultos
                .Where(b => correctos.Contains(b.Identificacion) || danadosFinales.Contains(b.Identificacion))
                .Select(b => new BultoClasificadoResumenDto(
                    b.BultoId,
                    b.Identificacion,
                    b.Peso,
                    danadosFinales.Contains(b.Identificacion) ? "Danado" : "Recibido",
                    EstaClasificado: false,
                    Prioridad: null,
                    ZonaClasificadaId: null,
                    TipoClasificacion: null,
                    FechaClasificacion: null))
                .ToList();

            var envioEnDeposito = new EnvioEnDepositoDto(
                envio.EnvioId,
                envio.CodigoSeguimiento,
                envio.NombreDestinatario,
                envio.DireccionDestino,
                envio.ModalidadServicio,
                envio.PesoTotal,
                bultosRecibidos.Count,
                CantidadClasificados: 0,
                EstaClasificado: false,
                DateTime.UtcNow,
                bultosRecibidos);

            EnviosEnDepositoEnMemoria.Insert(0, envioEnDeposito);

            var mensaje = hayDiscrepancias
                ? "Recepción registrada con discrepancias documentadas."
                : "Recepción registrada sin discrepancias.";

            return Task.FromResult(new ResultadoRecepcionDto(
                envio.EnvioId,
                envio.CodigoSeguimiento,
                "EnDeposito",
                Recepcionado: true,
                Idempotente: false,
                TieneDiscrepancias: hayDiscrepancias,
                correctos,
                faltantes,
                sobrantes,
                danadosFinales,
                mensaje));
        }
    }

    private static readonly List<EnvioEnDepositoDto> EnviosEnDepositoEnMemoria = new()
    {
        new EnvioEnDepositoDto(
            EnvioId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CodigoSeguimiento: "TRK-2026-0004",
            NombreDestinatario: "Andrés Méndez",
            DireccionDestino: "Camino Carrasco 6120, Canelones",
            ModalidadServicio: "Estandar",
            PesoTotal: 5.2m,
            CantidadBultos: 2,
            CantidadClasificados: 0,
            EstaClasificado: false,
            FechaAlta: DateTime.UtcNow.AddHours(-4),
            Bultos: new List<BultoClasificadoResumenDto>
            {
                new(Guid.NewGuid(), "BLT-0004-01", 3.2m, "Recibido", false, null, null, null, null),
                new(Guid.NewGuid(), "BLT-0004-02", 2.0m, "Recibido", false, null, null, null, null)
            }
        ),
        new EnvioEnDepositoDto(
            EnvioId: Guid.Parse("cccccccc-1111-2222-3333-000000000004"),
            CodigoSeguimiento: "ENV-2026-00111",
            NombreDestinatario: "Carolina Silva",
            DireccionDestino: "Av. Brasil 2890, Montevideo",
            ModalidadServicio: "Estandar",
            PesoTotal: 2.0m,
            CantidadBultos: 1,
            CantidadClasificados: 1,
            EstaClasificado: true,
            FechaAlta: DateTime.UtcNow.AddHours(-6),
            Bultos: new List<BultoClasificadoResumenDto>
            {
                new(Guid.NewGuid(), "BLT-004-A", 2.0m, "Recibido", true, "Normal", null, "Estándar", DateTime.UtcNow.AddHours(-1))
            }
        )
    };

    public Task<IReadOnlyList<EnvioEnDepositoDto>> ListarEnviosEnDepositoAsync(CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            return Task.FromResult<IReadOnlyList<EnvioEnDepositoDto>>(EnviosEnDepositoEnMemoria.ToList());
        }
    }

    public Task<EnvioEnDepositoDto?> ObtenerEnvioParaClasificacionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            var encontrado = EnviosEnDepositoEnMemoria.FirstOrDefault(e => e.EnvioId == envioId);
            return Task.FromResult(encontrado);
        }
    }

    public Task<ResultadoClasificacionDto> ClasificarBultosAsync(
        Guid envioId,
        ClasificarBultosRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        lock (Bloqueo)
        {
            var envio = EnviosEnDepositoEnMemoria.FirstOrDefault(e => e.EnvioId == envioId);
            if (envio == null)
            {
                return Task.FromResult(new ResultadoClasificacionDto(
                    envioId,
                    "DESCONOCIDO",
                    "NoEncontrado",
                    0, 0, Array.Empty<string>(), false));
            }

            var mapaClasif = (solicitud.Bultos ?? Array.Empty<ItemClasificacionBultoDto>())
                .ToDictionary(b => b.CodigoBulto.Trim().ToUpperInvariant(), b => b);

            var nuevosBultos = new List<BultoClasificadoResumenDto>();
            foreach (var b in envio.Bultos)
            {
                if (mapaClasif.TryGetValue(b.Identificacion.Trim().ToUpperInvariant(), out var clasif))
                {
                    nuevosBultos.Add(new BultoClasificadoResumenDto(
                        b.BultoId,
                        b.Identificacion,
                        b.Peso,
                        b.EstadoBulto,
                        EstaClasificado: true,
                        Prioridad: clasif.Prioridad,
                        ZonaClasificadaId: clasif.ZonaCoberturaId,
                        TipoClasificacion: clasif.Tipo,
                        FechaClasificacion: DateTime.UtcNow));
                }
                else
                {
                    nuevosBultos.Add(b);
                }
            }

            var totalClasificados = nuevosBultos.Count(b => b.EstaClasificado);
            var pendientes = nuevosBultos.Where(b => !b.EstaClasificado).Select(b => b.Identificacion).ToList();
            var listoParaRuta = totalClasificados == nuevosBultos.Count && nuevosBultos.Count > 0;

            var indice = EnviosEnDepositoEnMemoria.IndexOf(envio);
            var envioActualizado = new EnvioEnDepositoDto(
                envio.EnvioId,
                envio.CodigoSeguimiento,
                envio.NombreDestinatario,
                envio.DireccionDestino,
                envio.ModalidadServicio,
                envio.PesoTotal,
                envio.CantidadBultos,
                totalClasificados,
                listoParaRuta,
                envio.FechaAlta,
                nuevosBultos);

            EnviosEnDepositoEnMemoria[indice] = envioActualizado;

            return Task.FromResult(new ResultadoClasificacionDto(
                envio.EnvioId,
                envio.CodigoSeguimiento,
                "EnDeposito",
                nuevosBultos.Count,
                totalClasificados,
                pendientes,
                listoParaRuta));
        }
    }

    private static readonly List<RutaParaRendicionDetalleDto> RutasRendicionEnMemoria = new()
    {
        new RutaParaRendicionDetalleDto(
            HojaDeRutaId: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            NumeroRuta: "HR-20261027-1111",
            Fecha: DateOnly.FromDateTime(DateTime.UtcNow),
            Estado: "Finalizada",
            RepartidorId: Guid.Parse("aaaa1111-2222-3333-4444-555566667777"),
            RepartidorDocumento: "4.891.234-5",
            VehiculoId: Guid.Parse("bbbb2222-3333-4444-5555-666677778888"),
            VehiculoPatente: "SBX-1234",
            VehiculoTipo: "Furgón Utilitario",
            TotalParadas: 3,
            TotalBultos: 4,
            BultosEntregadosCount: 2,
            BultosEsperadosRetornoCount: 2,
            TieneEnviosSinIntento: true,
            Envios: new List<EnvioRendicionDto>
            {
                new(
                    Guid.Parse("e1111111-1111-1111-1111-111111111111"),
                    "TRK-2026-0001",
                    "Juan Pérez",
                    "18 de Julio 1234, Centro",
                    "Entregado",
                    EsEntregado: true,
                    RequiereRetorno: false,
                    EsRetornoSinIntento: false,
                    new List<BultoItemRendicionDto>
                    {
                        new(Guid.NewGuid(), "BLT-0001-01", 1.5m, "CargadoEnVehiculo", true),
                        new(Guid.NewGuid(), "BLT-0001-02", 1.0m, "CargadoEnVehiculo", true)
                    }
                ),
                new(
                    Guid.Parse("e2222222-2222-2222-2222-222222222222"),
                    "TRK-2026-0002",
                    "María González",
                    "Colonia 890, Cordón",
                    "Reprogramado",
                    EsEntregado: false,
                    RequiereRetorno: true,
                    EsRetornoSinIntento: false,
                    new List<BultoItemRendicionDto>
                    {
                        new(Guid.NewGuid(), "BLT-0002-01", 2.0m, "EnRetornoADeposito", true)
                    }
                ),
                new(
                    Guid.Parse("e3333333-3333-3333-3333-333333333333"),
                    "TRK-2026-0003",
                    "Lucas Gómez",
                    "San José 1420, Centro",
                    "EnTransito",
                    EsEntregado: false,
                    RequiereRetorno: true,
                    EsRetornoSinIntento: true,
                    new List<BultoItemRendicionDto>
                    {
                        new(Guid.NewGuid(), "BLT-0003-01", 3.5m, "CargadoEnVehiculo", true)
                    }
                )
            },
            MotivosInternosDisponibles: new List<MotivoInternoOpcionDto>
            {
                new("RETORNO_SIN_INTENTO", "Retorno a depósito sin intento de entrega"),
                new("AVERIA_VEHICULO", "Avería mecánica en vehículo de reparto"),
                new("CIERRE_OPERATIVO", "Cierre de jornada o fin de turno operativo")
            }
        ),
        new RutaParaRendicionDetalleDto(
            HojaDeRutaId: Guid.Parse("22223333-4444-5555-6666-777788889999"),
            NumeroRuta: "HR-20261027-2222",
            Fecha: DateOnly.FromDateTime(DateTime.UtcNow),
            Estado: "Despachada",
            RepartidorId: Guid.Parse("cccc3333-4444-5555-6666-777788889999"),
            RepartidorDocumento: "3.567.890-1",
            VehiculoId: Guid.Parse("dddd4444-5555-6666-7777-888899990000"),
            VehiculoPatente: "SCA-5678",
            VehiculoTipo: "Camioneta Van",
            TotalParadas: 2,
            TotalBultos: 2,
            BultosEntregadosCount: 1,
            BultosEsperadosRetornoCount: 1,
            TieneEnviosSinIntento: false,
            Envios: new List<EnvioRendicionDto>
            {
                new(
                    Guid.Parse("e4444444-4444-4444-4444-444444444444"),
                    "TRK-2026-0004",
                    "Sofía Herrera",
                    "Bulevar España 2200, Pocitos",
                    "Entregado",
                    EsEntregado: true,
                    RequiereRetorno: false,
                    EsRetornoSinIntento: false,
                    new List<BultoItemRendicionDto>
                    {
                        new(Guid.NewGuid(), "BLT-0004-01", 2.2m, "CargadoEnVehiculo", true)
                    }
                ),
                new(
                    Guid.Parse("e5555555-5555-5555-5555-555555555555"),
                    "TRK-2026-0005",
                    "Rodrigo Ramos",
                    "Rivera 3100, Buceo",
                    "EnDevolucion",
                    EsEntregado: false,
                    RequiereRetorno: true,
                    EsRetornoSinIntento: false,
                    new List<BultoItemRendicionDto>
                    {
                        new(Guid.NewGuid(), "BLT-0005-01", 1.8m, "EnRetornoADeposito", true)
                    }
                )
            },
            MotivosInternosDisponibles: new List<MotivoInternoOpcionDto>
            {
                new("RETORNO_SIN_INTENTO", "Retorno a depósito sin intento de entrega"),
                new("AVERIA_VEHICULO", "Avería mecánica en vehículo de reparto")
            }
        )
    };

    public Task<IReadOnlyList<RutaPendienteRendicionDto>> ListarRutasPendientesRendicionAsync(CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            var pendientes = RutasRendicionEnMemoria
                .Where(r => r.Estado != "Rendida")
                .Select(r => new RutaPendienteRendicionDto(
                    r.HojaDeRutaId,
                    r.NumeroRuta,
                    r.Fecha,
                    r.Estado,
                    r.RepartidorId,
                    r.RepartidorDocumento,
                    r.VehiculoId,
                    r.VehiculoPatente,
                    r.VehiculoTipo,
                    r.TotalParadas,
                    r.TotalBultos,
                    r.BultosEntregadosCount,
                    r.BultosEsperadosRetornoCount,
                    DateTime.UtcNow.AddHours(-4)))
                .ToList();

            return Task.FromResult<IReadOnlyList<RutaPendienteRendicionDto>>(pendientes);
        }
    }

    public Task<RutaParaRendicionDetalleDto?> ObtenerRutaParaRendicionAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        lock (Bloqueo)
        {
            var ruta = RutasRendicionEnMemoria.FirstOrDefault(r => r.HojaDeRutaId == hojaDeRutaId);
            return Task.FromResult(ruta);
        }
    }

    public Task<ResultadoRendicionRutaDto> RendirHojaDeRutaAsync(
        Guid hojaDeRutaId,
        RendirHojaDeRutaRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        lock (Bloqueo)
        {
            var ruta = RutasRendicionEnMemoria.FirstOrDefault(r => r.HojaDeRutaId == hojaDeRutaId);
            if (ruta == null)
            {
                return Task.FromResult(new ResultadoRendicionRutaDto(
                    hojaDeRutaId,
                    "NoEncontrada",
                    0, 0, 0,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false,
                    "La hoja de ruta indicada no fue encontrada."));
            }

            var devueltos = (solicitud.CodigosBultosDevueltos ?? Array.Empty<string>())
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToHashSet();

            var todosBultosRetorno = ruta.Envios
                .Where(e => e.RequiereRetorno)
                .SelectMany(e => e.Bultos)
                .Select(b => b.Identificacion.Trim().ToUpperInvariant())
                .ToHashSet();

            var bultosDevueltos = devueltos.Count(c => todosBultosRetorno.Contains(c));
            var bultosPendientes = todosBultosRetorno.Count - bultosDevueltos;
            var sobrantes = devueltos.Where(c => !todosBultosRetorno.Contains(c)).ToList();

            var reintegrados = ruta.Envios
                .Where(e => e.RequiereRetorno && e.EstadoEnvio != "EnDevolucion")
                .Select(e => e.CodigoSeguimiento)
                .ToList();

            var enDevolucion = ruta.Envios
                .Where(e => e.EstadoEnvio == "EnDevolucion")
                .Select(e => e.CodigoSeguimiento)
                .ToList();

            var enviosPendientes = bultosPendientes > 0
                ? ruta.Envios.Where(e => e.RequiereRetorno).Select(e => e.CodigoSeguimiento).Take(1).ToList()
                : new List<string>();

            // Actualizar estado en memoria a Rendida
            var indice = RutasRendicionEnMemoria.IndexOf(ruta);
            RutasRendicionEnMemoria[indice] = ruta with { Estado = "Rendida" };

            var mensaje = bultosPendientes == 0 && sobrantes.Count == 0
                ? "Rendición registrada sin diferencias. La hoja de ruta fue cerrada y el repartidor quedó habilitado."
                : $"Rendición registrada con diferencias: {bultosPendientes} bulto(s) pendiente(s) y {sobrantes.Count} sobrante(s). El repartidor quedó habilitado.";

            return Task.FromResult(new ResultadoRendicionRutaDto(
                ruta.HojaDeRutaId,
                "Rendida",
                ruta.BultosEntregadosCount,
                bultosDevueltos,
                Math.Max(0, bultosPendientes),
                reintegrados,
                enDevolucion,
                enviosPendientes,
                sobrantes,
                Idempotente: false,
                mensaje));
        }
    }
}
