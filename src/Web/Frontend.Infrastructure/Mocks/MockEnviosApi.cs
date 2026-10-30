using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de gestión de envíos y extravíos (UC-24, UC-28, UC-43, RF-11, RF-12).
/// </summary>
public class MockEnviosApi : IEnviosApi
{
    public const string ComercioAutenticadoPortal = "MegaStore Uruguay";

    private readonly MockContextoOperador? _contextoOperador;

    public MockEnviosApi() : this(null)
    {
    }

    public MockEnviosApi(MockContextoOperador? contextoOperador)
    {
        _contextoOperador = contextoOperador;
    }

    private static readonly Dictionary<Guid, List<EventoHistorialEnvioDto>> HistorialPorEnvio = new();

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
            ModalidadServicio = "Estandar",
            Tarifa = 205.00m,
            Observaciones = "Entregar en portería si no responde",
            PesoKg = 1.8m,
            VolumenM3 = 0.012m,
            ComercioNombre = ComercioAutenticadoPortal,
            HojaDeRutaNumero = "HDR-2026-0078",
            RepartidorNombre = "Juan Pérez",
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
            ModalidadServicio = "Express",
            Tarifa = 337.50m,
            Observaciones = "Timbre no funciona, llamar al celular",
            PesoKg = 3.5m,
            VolumenM3 = 0.025m,
            ComercioNombre = ComercioAutenticadoPortal,
            HojaDeRutaNumero = "HDR-2026-0081",
            RepartidorNombre = "Juan Pérez",
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
            ModalidadServicio = "Estandar",
            Tarifa = 175.00m,
            Observaciones = null,
            PesoKg = 0.6m,
            VolumenM3 = 0.005m,
            ComercioNombre = "Supermercados Ta-Ta",
            HojaDeRutaNumero = "HDR-2026-0082",
            RepartidorNombre = "Lucía Fernández",
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
            ModalidadServicio = "Estandar",
            Tarifa = 290.00m,
            Observaciones = "Paquete frágil",
            PesoKg = 5.2m,
            VolumenM3 = 0.040m,
            ComercioNombre = ComercioAutenticadoPortal,
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
            ModalidadServicio = "Estandar",
            Tarifa = 187.50m,
            BultosIdentificaciones = new[] { "TRK-2026-0005-B01" },
            Observaciones = "Horario de recepción comercial 9 a 18",
            PesoKg = 1.1m,
            VolumenM3 = 0.008m,
            ComercioNombre = ComercioAutenticadoPortal,
            OperadorNombre = "TransLogística Express",
            SelloModificacion = "sello-envio-5"
        },
        new EnvioDto
        {
            Id = Guid.Parse("33445566-7788-99aa-bbcc-ddeeff001122"),
            CodigoSeguimiento = "TRK-2026-0009",
            NombreDestinatario = "Sebastián Morales",
            DireccionDestino = "Bvar. España 2210 Apto 501, Pocitos, Montevideo",
            TelefonoDestinatario = "099 456 789",
            FechaCreacion = DateTime.UtcNow.AddMinutes(-20),
            Estado = EstadoEnvioDto.Admitido,
            ModalidadServicio = "Express",
            Tarifa = 365.00m,
            BultosIdentificaciones = new[] { "TRK-2026-0009-B01", "TRK-2026-0009-B02" },
            Observaciones = "Entregar en recepción del edificio",
            PesoKg = 3.2m,
            VolumenM3 = 0.022m,
            ComercioNombre = ComercioAutenticadoPortal,
            OperadorNombre = "TransLogística Express",
            SelloModificacion = "sello-envio-9"
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
            ModalidadServicio = "Estandar",
            Tarifa = 210.00m,
            Observaciones = "Destinatario ausente en primer intento",
            PesoKg = 2.0m,
            VolumenM3 = 0.015m,
            ComercioNombre = ComercioAutenticadoPortal,
            SelloModificacion = "sello-envio-6"
        },
        new EnvioDto
        {
            Id = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
            CodigoSeguimiento = "TRK-2026-0007",
            NombreDestinatario = "Valeria Dupont",
            DireccionDestino = "Av. Brasil 3120 Apto 402, Pocitos",
            TelefonoDestinatario = "099 222 333",
            FechaCreacion = DateTime.UtcNow.AddDays(-1),
            Estado = EstadoEnvioDto.EnTransito,
            Observaciones = "Bulto reportado ausente en la rendición de la ruta",
            PesoKg = 0.9m,
            VolumenM3 = 0.006m,
            ComercioNombre = "Tienda Inglesa Express",
            HojaDeRutaNumero = "HDR-2026-0081",
            RepartidorNombre = "Juan Pérez",
            SelloModificacion = "sello-envio-7"
        },
        new EnvioDto
        {
            Id = Guid.Parse("22334455-6677-8899-aabb-ccddeeff0011"),
            CodigoSeguimiento = "TRK-2026-0008",
            NombreDestinatario = "Joaquín Castro",
            DireccionDestino = "San Fructuoso 1150, Bella Vista",
            TelefonoDestinatario = "091 777 888",
            FechaCreacion = DateTime.UtcNow.AddDays(-3),
            Estado = EstadoEnvioDto.Extraviado,
            Observaciones = "No apareció tras rendición ni en depósito de muelle",
            PesoKg = 1.4m,
            VolumenM3 = 0.010m,
            ComercioNombre = "Farmashop Uruguay",
            FechaExtravio = DateTime.UtcNow.AddDays(-1),
            MotivoExtravio = "No aparece en rendición de la hoja de ruta HDR-2026-0075",
            IncidenciaAsociadaNumero = "INC-2026-0038",
            SelloModificacion = "sello-envio-8"
        },
        new EnvioDto
        {
            Id = Guid.Parse("77889900-aabb-ccdd-eeff-112233445566"),
            CodigoSeguimiento = "TRK-2026-0010",
            NombreDestinatario = "Gonzalo Silva",
            DireccionDestino = "21 de Setiembre 2840 Apto 601, Pocitos, Montevideo",
            TelefonoDestinatario = "099 321 654",
            FechaCreacion = DateTime.UtcNow.AddDays(-2),
            Estado = EstadoEnvioDto.EnDevolucion,
            ModalidadServicio = "Estandar",
            Tarifa = 215.00m,
            Observaciones = "Destinatario rechazó el producto por embalaje golpeado. Retornado a depósito y en camino de retorno al comercio.",
            PesoKg = 1.4m,
            VolumenM3 = 0.010m,
            ComercioNombre = ComercioAutenticadoPortal,
            OperadorNombre = "TransLogística Express",
            HojaDeRutaNumero = "HDR-2026-0080",
            RepartidorNombre = "Juan Pérez",
            SelloModificacion = "sello-envio-10"
        }
    };

    /// <summary>
    /// Configura o reestablece un envío en la colección en memoria para pruebas unitarias.
    /// </summary>
    public static void ConfigurarEnvioEnMemoriaParaPruebas(EnvioDto envio)
    {
        var indice = EnviosEnMemoria.FindIndex(e => e.Id == envio.Id);
        if (indice >= 0)
        {
            EnviosEnMemoria[indice] = envio;
        }
        else
        {
            EnviosEnMemoria.Add(envio);
        }
    }

    private static int _contadorSecuencial = 8;

    public Task<ResultadoPaginadoDto<EnvioDto>> ListarEnviosAsync(FiltroEnviosDto filtro, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = EnviosEnMemoria.AsQueryable();

        // Aislamiento por comercio (UC-28): el portal del comercio muestra únicamente los envíos propios
        if (filtro.SoloEnviosPropiosComercio)
        {
            consulta = consulta.Where(e =>
                string.Equals(e.ComercioNombre, ComercioAutenticadoPortal, StringComparison.OrdinalIgnoreCase));
        }

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
                e.DireccionDestino.ToLowerInvariant().Contains(termino) ||
                (e.ComercioNombre != null && e.ComercioNombre.ToLowerInvariant().Contains(termino)) ||
                (e.HojaDeRutaNumero != null && e.HojaDeRutaNumero.ToLowerInvariant().Contains(termino)));
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

        var totalBultos = nuevoEnvio.Bultos?.Count > 0 ? nuevoEnvio.Bultos.Count : 1;
        var totalPeso = nuevoEnvio.PesoKg.HasValue && nuevoEnvio.PesoKg > 0 ? nuevoEnvio.PesoKg.Value : 1.0m;
        var esExpress = nuevoEnvio.ModalidadServicio?.Equals("Express", StringComparison.OrdinalIgnoreCase) == true;

        // Cálculo de tarifa según cuadro tarifario (base de modalidad + tramos de peso + recargo por bultos adicionales)
        var baseTarifa = esExpress ? 250m : 160m;
        var tarifaCalculada = baseTarifa + (totalPeso * 25m) + ((totalBultos - 1) * 35m);

        var identificacionesBultos = Enumerable.Range(1, totalBultos)
            .Select(i => $"{codigoSeguimiento}-B{i:D2}")
            .ToList();

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
            PesoKg = totalPeso,
            VolumenM3 = nuevoEnvio.VolumenM3,
            Tarifa = tarifaCalculada,
            ModalidadServicio = nuevoEnvio.ModalidadServicio,
            BultosIdentificaciones = identificacionesBultos,
            ComercioNombre = ComercioAutenticadoPortal,
            OperadorNombre = _contextoOperador?.OperadorActivo?.NombreComercial ?? "TransLogística Express",
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        EnviosEnMemoria.Insert(0, entidad);

        RegistrarEventoHistorico(nuevoId, new EventoHistorialEnvioDto
        {
            EnvioId = nuevoId,
            FechaHora = DateTime.UtcNow,
            EstadoAnterior = null,
            NuevoEstado = EstadoEnvioDto.Admitido,
            Usuario = "Portal Comercio",
            DescripcionEvento = $"Orden de envío creada y admitida. Tarifa calculada: ${tarifaCalculada:F2}. Disponible para recepción en depósito."
        });

        return Task.FromResult(entidad);
    }

    public Task<ResultadoDeclaracionExtravioDto> DeclararEnvioExtraviadoAsync(DeclararExtraviadoDto dto, CancellationToken cancelacion = default)
    {
        var index = EnviosEnMemoria.FindIndex(e => e.Id == dto.EnvioId);
        if (index == -1)
        {
            return Task.FromResult(new ResultadoDeclaracionExtravioDto
            {
                Exito = false,
                Mensaje = $"No se encontró el envío con identificador {dto.EnvioId}."
            });
        }

        var envioActual = EnviosEnMemoria[index];

        // Validar Precondición: Un envío 'en depósito' o 'en tránsito' no pudo ser ubicado
        if (envioActual.Estado != EstadoEnvioDto.EnDeposito && envioActual.Estado != EstadoEnvioDto.EnTransito)
        {
            return Task.FromResult(new ResultadoDeclaracionExtravioDto
            {
                Exito = false,
                Mensaje = $"Solo se pueden declarar como extraviados aquellos envíos en estado 'En Depósito' o 'En Tránsito'. El estado actual es '{envioActual.Estado}'."
            });
        }

        var estadoAnterior = envioActual.Estado;
        var ahora = DateTime.UtcNow;
        var numeroIncidencia = !string.IsNullOrWhiteSpace(dto.NumeroIncidenciaExistente) 
            ? dto.NumeroIncidenciaExistente 
            : (dto.CrearIncidenciaOperativa ? $"INC-2026-00{Random.Shared.Next(42, 99)}" : null);

        // Actualizar envío a estado Extraviado
        var envioActualizado = envioActual with
        {
            Estado = EstadoEnvioDto.Extraviado,
            FechaExtravio = ahora,
            MotivoExtravio = dto.MotivoExtravio,
            IncidenciaAsociadaNumero = numeroIncidencia,
            Observaciones = string.IsNullOrEmpty(envioActual.Observaciones) 
                ? $"Extravío: {dto.MotivoExtravio}. {dto.ObservacionesDetalladas}" 
                : $"{envioActual.Observaciones} | Extravío: {dto.MotivoExtravio}. {dto.ObservacionesDetalladas}",
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        EnviosEnMemoria[index] = envioActualizado;

        // Registrar Evento Histórico de Auditoría (RF-11, RF-12)
        var evento = new EventoHistorialEnvioDto
        {
            EnvioId = dto.EnvioId,
            FechaHora = ahora,
            EstadoAnterior = estadoAnterior,
            NuevoEstado = EstadoEnvioDto.Extraviado,
            Usuario = dto.DespachadorUsuario,
            DescripcionEvento = $"Envío declarado como extraviado. Motivo: {dto.MotivoExtravio}. {dto.ObservacionesDetalladas}",
            MotivoDetalle = dto.MotivoExtravio,
            NumeroIncidenciaAsociada = numeroIncidencia,
            NotificacionEnviada = dto.NotificarComercio
        };

        RegistrarEventoHistorico(dto.EnvioId, evento);

        var resultado = new ResultadoDeclaracionExtravioDto
        {
            Exito = true,
            Mensaje = $"El envío {envioActual.CodigoSeguimiento} ha sido declarado como extraviado exitosamente.",
            EnvioId = dto.EnvioId,
            CodigoSeguimiento = envioActual.CodigoSeguimiento,
            NuevoEstado = EstadoEnvioDto.Extraviado,
            NumeroIncidenciaGenerada = numeroIncidencia,
            FechaEvento = ahora,
            NotificacionComercioEnviada = dto.NotificarComercio
        };

        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<EventoHistorialEnvioDto>> ObtenerHistorialEnvioAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        if (HistorialPorEnvio.TryGetValue(envioId, out var eventos))
        {
            return Task.FromResult<IReadOnlyList<EventoHistorialEnvioDto>>(eventos);
        }

        // Si no tenía eventos precargados, inicializamos una línea de vida coherente
        var envio = EnviosEnMemoria.FirstOrDefault(e => e.Id == envioId);
        var lista = new List<EventoHistorialEnvioDto>();

        if (envio != null)
        {
            lista.Add(new EventoHistorialEnvioDto
            {
                EnvioId = envioId,
                FechaHora = envio.FechaCreacion,
                EstadoAnterior = null,
                NuevoEstado = EstadoEnvioDto.Admitido,
                Usuario = "Comercio",
                DescripcionEvento = "Orden de envío ingresada por comercio adherido."
            });

            if (envio.Estado >= EstadoEnvioDto.EnDeposito)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(2),
                    EstadoAnterior = EstadoEnvioDto.Admitido,
                    NuevoEstado = EstadoEnvioDto.EnDeposito,
                    Usuario = "Operario Depósito",
                    DescripcionEvento = "Bulto recibido y clasificado físicamente en depósito central."
                });
            }

            if (envio.Estado >= EstadoEnvioDto.AsignadoARuta)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(4),
                    EstadoAnterior = EstadoEnvioDto.EnDeposito,
                    NuevoEstado = EstadoEnvioDto.AsignadoARuta,
                    Usuario = "Despachador",
                    DescripcionEvento = $"Envío asignado a hoja de ruta {envio.HojaDeRutaNumero ?? "HDR-2026-0081"}."
                });
            }

            if (envio.Estado == EstadoEnvioDto.EnTransito ||
                envio.Estado == EstadoEnvioDto.Entregado ||
                envio.Estado == EstadoEnvioDto.NoEntregado ||
                envio.Estado == EstadoEnvioDto.Reprogramado ||
                envio.Estado == EstadoEnvioDto.EnDevolucion ||
                envio.Estado == EstadoEnvioDto.Devuelto ||
                envio.Estado == EstadoEnvioDto.Extraviado)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(5),
                    EstadoAnterior = EstadoEnvioDto.AsignadoARuta,
                    NuevoEstado = EstadoEnvioDto.EnTransito,
                    Usuario = "Despachador",
                    DescripcionEvento = $"Bulto cargado y despachado en hoja de ruta {envio.HojaDeRutaNumero ?? "HDR-2026-0081"}."
                });
            }

            if (envio.Estado == EstadoEnvioDto.Entregado)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(9),
                    EstadoAnterior = EstadoEnvioDto.EnTransito,
                    NuevoEstado = EstadoEnvioDto.Entregado,
                    Usuario = envio.RepartidorNombre ?? "Repartidor",
                    DescripcionEvento = "Entrega confirmada en domicilio del destinatario con comprobante registrado."
                });
            }

            if (envio.Estado == EstadoEnvioDto.NoEntregado ||
                envio.Estado == EstadoEnvioDto.Reprogramado ||
                envio.Estado == EstadoEnvioDto.EnDevolucion ||
                envio.Estado == EstadoEnvioDto.Devuelto)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(8),
                    EstadoAnterior = EstadoEnvioDto.EnTransito,
                    NuevoEstado = EstadoEnvioDto.NoEntregado,
                    Usuario = envio.RepartidorNombre ?? "Repartidor",
                    DescripcionEvento = string.IsNullOrWhiteSpace(envio.Observaciones)
                        ? "Intento de entrega fallido registrado en ruta."
                        : $"Intento de entrega sin éxito: {envio.Observaciones}"
                });
            }

            if (envio.Estado == EstadoEnvioDto.Extraviado)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaExtravio ?? DateTime.UtcNow,
                    EstadoAnterior = EstadoEnvioDto.EnTransito,
                    NuevoEstado = EstadoEnvioDto.Extraviado,
                    Usuario = "Despachador",
                    DescripcionEvento = $"Envío declarado extraviado tras gestión de búsqueda: {envio.MotivoExtravio}",
                    MotivoDetalle = envio.MotivoExtravio,
                    NumeroIncidenciaAsociada = envio.IncidenciaAsociadaNumero,
                    NotificacionEnviada = true
                });
            }

            if (envio.Estado == EstadoEnvioDto.EnDevolucion || envio.Estado == EstadoEnvioDto.Devuelto)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(14),
                    EstadoAnterior = EstadoEnvioDto.NoEntregado,
                    NuevoEstado = EstadoEnvioDto.EnDevolucion,
                    Usuario = "Operador Depósito",
                    DescripcionEvento = "Bulto devuelto al centro de distribución para su retorno hacia el comercio remitente."
                });
            }

            if (envio.Estado == EstadoEnvioDto.Devuelto)
            {
                lista.Add(new EventoHistorialEnvioDto
                {
                    EnvioId = envioId,
                    FechaHora = envio.FechaCreacion.AddHours(24),
                    EstadoAnterior = EstadoEnvioDto.EnDevolucion,
                    NuevoEstado = EstadoEnvioDto.Devuelto,
                    Usuario = "Portal Comercio",
                    DescripcionEvento = "Devolución recibida y confirmada por el comercio. Bulto reintegrado al stock."
                });
            }
        }

        HistorialPorEnvio[envioId] = lista;
        return Task.FromResult<IReadOnlyList<EventoHistorialEnvioDto>>(lista);
    }

    private static void RegistrarEventoHistorico(Guid envioId, EventoHistorialEnvioDto evento)
    {
        if (!HistorialPorEnvio.TryGetValue(envioId, out var lista))
        {
            lista = new List<EventoHistorialEnvioDto>();
            HistorialPorEnvio[envioId] = lista;
        }

        lista.Add(evento);
    }

    private static readonly Dictionary<string, ResultadoImportacionDto> LotesPorClave = new();

    /// <summary>
    /// Simula la importación masiva de envíos desde archivo CSV con cálculo de tarifas e idempotencia (UC-25, RF-07).
    /// </summary>
    public Task<ResultadoImportacionDto> ImportarEnviosPorArchivoAsync(
        byte[] contenidoArchivo,
        string nombreArchivo,
        string? claveIdempotencia = null,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenidoArchivo);

        if (contenidoArchivo.Length == 0)
        {
            throw new ArgumentException("El archivo seleccionado está vacío.", nameof(contenidoArchivo));
        }

        if (contenidoArchivo.Length > 2 * 1024 * 1024)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El archivo supera el tamaño máximo de 2 MB.");
        }

        var clave = string.IsNullOrWhiteSpace(claveIdempotencia)
            ? "sha256:" + Convert.ToHexString(SHA256.HashData(contenidoArchivo))
            : claveIdempotencia.Trim();

        // Flujo alternativo: Reimportación idempotente
        if (LotesPorClave.TryGetValue(clave, out var loteExistente))
        {
            var filasDuplicadas = loteExistente.Filas.Select(f =>
                f.Estado == ResultadoFilaImportacionDto.Creado
                    ? f with { Estado = ResultadoFilaImportacionDto.Duplicado }
                    : f).ToList();

            var resultadoReimportado = new ResultadoImportacionDto(
                loteExistente.LoteId,
                clave,
                Reimportacion: true,
                loteExistente.TotalRegistros,
                Creados: 0,
                Duplicados: loteExistente.Creados + loteExistente.Duplicados,
                loteExistente.ConError,
                filasDuplicadas);

            return Task.FromResult(resultadoReimportado);
        }

        var textoCsv = Encoding.UTF8.GetString(contenidoArchivo).TrimStart('\uFEFF');
        var lineas = textoCsv
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Select((texto, indice) => (Texto: texto, Numero: indice + 1))
            .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
            .ToList();

        if (lineas.Count == 0)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El archivo no contiene registros para importar.");
        }

        var encabezado = lineas[0].Texto;
        var separador = encabezado.Count(c => c == ';') > encabezado.Count(c => c == ',') ? ';' : ',';
        var columnas = encabezado.Split(separador).Select(c => c.Trim().ToLowerInvariant()).ToList();

        var columnasObligatorias = new[] { "referencia", "modalidad", "destinatario", "calle", "numero", "ciudad", "peso", "alto", "ancho", "profundo" };
        var faltantes = columnasObligatorias.Where(c => !columnas.Contains(c)).ToList();
        if (faltantes.Count > 0)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, $"Faltan columnas obligatorias en el archivo CSV: {string.Join(", ", faltantes)}.");
        }

        var filasResultado = new List<ResultadoFilaImportacionDto>();
        var referenciasVistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loteId = Guid.NewGuid();

        foreach (var (texto, numero) in lineas.Skip(1))
        {
            var celdas = texto.Split(separador);
            if (celdas.Length != columnas.Count)
            {
                filasResultado.Add(new ResultadoFilaImportacionDto(numero, ResultadoFilaImportacionDto.ConError, Error: $"La fila tiene {celdas.Length} columnas y se esperaban {columnas.Count}."));
                continue;
            }

            string ObtenerValor(string col)
            {
                var idx = columnas.IndexOf(col);
                return idx >= 0 && idx < celdas.Length ? celdas[idx].Trim() : string.Empty;
            }

            var referencia = ObtenerValor("referencia");
            var destinatario = ObtenerValor("destinatario");
            var calle = ObtenerValor("calle");
            var num = ObtenerValor("numero");
            var ciudad = ObtenerValor("ciudad");
            var pesoTexto = ObtenerValor("peso");

            if (string.IsNullOrWhiteSpace(referencia))
            {
                filasResultado.Add(new ResultadoFilaImportacionDto(numero, ResultadoFilaImportacionDto.ConError, Error: "La referencia es obligatoria para garantizar la idempotencia."));
                continue;
            }

            if (!referenciasVistas.Add(referencia))
            {
                filasResultado.Add(new ResultadoFilaImportacionDto(numero, ResultadoFilaImportacionDto.ConError, Error: $"Referencia '{referencia}' duplicada dentro del mismo lote."));
                continue;
            }

            if (!decimal.TryParse(pesoTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out var peso) || peso <= 0)
            {
                filasResultado.Add(new ResultadoFilaImportacionDto(numero, ResultadoFilaImportacionDto.ConError, Error: "El peso debe ser un número decimal positivo (ej: 2.5)."));
                continue;
            }

            // Cálculo simulado de tarifa: Base $120 + $25 por kg
            var tarifaCalculada = 120m + Math.Round(peso * 25m, 2);
            var codigoSeguimiento = $"ENV-2026-{Random.Shared.Next(10000, 99999)}";

            var nuevoEnvio = new EnvioDto
            {
                Id = Guid.NewGuid(),
                CodigoSeguimiento = codigoSeguimiento,
                NombreDestinatario = destinatario,
                DireccionDestino = $"{calle} {num}, {ciudad}",
                FechaCreacion = DateTime.UtcNow,
                Estado = EstadoEnvioDto.Admitido,
                PesoKg = peso,
                Tarifa = tarifaCalculada,
                ModalidadServicio = "Estandar",
                ComercioNombre = ComercioAutenticadoPortal,
                OperadorNombre = _contextoOperador?.OperadorActivo?.NombreComercial ?? "TransLogística Express",
                Observaciones = $"Lote {loteId.ToString()[..8]} - Ref: {referencia}"
            };

            lock (EnviosEnMemoria)
            {
                EnviosEnMemoria.Insert(0, nuevoEnvio);
            }

            filasResultado.Add(new ResultadoFilaImportacionDto(
                numero,
                ResultadoFilaImportacionDto.Creado,
                codigoSeguimiento,
                tarifaCalculada));
        }

        var creados = filasResultado.Count(r => r.Estado == ResultadoFilaImportacionDto.Creado);
        var duplicados = filasResultado.Count(r => r.Estado == ResultadoFilaImportacionDto.Duplicado);
        var conError = filasResultado.Count(r => r.Estado == ResultadoFilaImportacionDto.ConError);

        var resultadoFinal = new ResultadoImportacionDto(
            loteId,
            clave,
            Reimportacion: false,
            filasResultado.Count,
            creados,
            duplicados,
            conError,
            filasResultado);

        LotesPorClave[clave] = resultadoFinal;
        return Task.FromResult(resultadoFinal);
    }

    private const string ClaveApiDemoSembrada = "dev-api-key-megastore-2026";
    private static string _claveApiVigente = "um_k9Xz2Lm8Qp4Vn7Rt1Yw5Bc3Df6Gh0Jk2";
    private static readonly Dictionary<string, RespuestaAltaEnvioApiDto> EnviosPorReferenciaApi = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Obtiene la clave de API vigente del comercio para el operador actual.
    /// </summary>
    public Task<string> ObtenerClaveApiActivaAsync(CancellationToken cancelacion = default)
    {
        return Task.FromResult(_claveApiVigente);
    }

    /// <summary>
    /// Genera una nueva clave de API e invalida la anterior.
    /// </summary>
    public Task<ClaveApiComercioDto> RegenerarClaveApiAsync(CancellationToken cancelacion = default)
    {
        var nuevaClave = "um_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _claveApiVigente = nuevaClave;

        var resultado = new ClaveApiComercioDto(
            nuevaClave,
            "Guarde esta clave ahora: no volverá a mostrarse. La clave anterior ya no es válida.",
            DateTime.UtcNow);

        return Task.FromResult(resultado);
    }

    /// <summary>
    /// Procesa el alta de un envío invocada desde el sistema del comercio vía API con validación de credenciales, tarifa e idempotencia.
    /// </summary>
    public Task<RespuestaAltaEnvioApiDto> CrearEnvioPorApiIntegracionAsync(
        SolicitudAltaEnvioApiDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (string.IsNullOrWhiteSpace(solicitud.ClaveApi) ||
            (!string.Equals(solicitud.ClaveApi.Trim(), _claveApiVigente, StringComparison.Ordinal) &&
             !string.Equals(solicitud.ClaveApi.Trim(), ClaveApiDemoSembrada, StringComparison.Ordinal)))
        {
            throw new ApiExcepcion(HttpStatusCode.Unauthorized, "Clave de API inválida.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.NombreDestinatario))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El nombre del destinatario es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.Calle) ||
            string.IsNullOrWhiteSpace(solicitud.Numero) ||
            string.IsNullOrWhiteSpace(solicitud.Ciudad))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "La dirección completa (calle, número y ciudad) es obligatoria.");
        }

        if (solicitud.Bultos is null || solicitud.Bultos.Count == 0)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El envío debe incluir al menos un bulto.");
        }

        if (solicitud.Bultos.Any(b => b.Peso <= 0 || b.Alto <= 0 || b.Ancho <= 0 || b.Profundo <= 0))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "Cada bulto debe tener peso y dimensiones mayores a cero.");
        }

        var referenciaExternaLimpia = string.IsNullOrWhiteSpace(solicitud.ReferenciaExterna)
            ? null
            : solicitud.ReferenciaExterna.Trim();

        if (referenciaExternaLimpia is not null &&
            EnviosPorReferenciaApi.TryGetValue(referenciaExternaLimpia, out var envioPrevio))
        {
            return Task.FromResult(envioPrevio with { Duplicado = true });
        }

        var nuevoId = Guid.NewGuid();
        var codigoSeguimiento = $"TRK-2026-{Interlocked.Increment(ref _contadorSecuencial):D4}";

        var totalBultos = solicitud.Bultos.Count;
        var totalPeso = solicitud.Bultos.Sum(b => b.Peso);
        var totalVolumen = solicitud.Bultos.Sum(b => b.VolumenM3);
        var esExpress = solicitud.ModalidadServicio.Equals("Express", StringComparison.OrdinalIgnoreCase);

        var baseTarifa = esExpress ? 250m : 160m;
        var tarifaCalculada = Math.Round(baseTarifa + (totalPeso * 25m) + ((totalBultos - 1) * 35m), 2);

        var identificacionesBultos = Enumerable.Range(1, totalBultos)
            .Select(i => $"{codigoSeguimiento}-B{i:D2}")
            .ToList();

        var direccionDestino = $"{solicitud.Calle.Trim()} {solicitud.Numero.Trim()}, {solicitud.Ciudad.Trim()}";

        var entidadEnvio = new EnvioDto
        {
            Id = nuevoId,
            CodigoSeguimiento = codigoSeguimiento,
            NombreDestinatario = solicitud.NombreDestinatario.Trim(),
            DireccionDestino = direccionDestino,
            TelefonoDestinatario = solicitud.Telefono?.Trim(),
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoEnvioDto.Admitido,
            Observaciones = solicitud.Referencia?.Trim(),
            PesoKg = totalPeso,
            VolumenM3 = totalVolumen,
            Tarifa = tarifaCalculada,
            ModalidadServicio = esExpress ? "Express" : "Estandar",
            BultosIdentificaciones = identificacionesBultos,
            ComercioNombre = ComercioAutenticadoPortal,
            OperadorNombre = _contextoOperador?.OperadorActivo?.NombreComercial ?? "TransLogística Express",
            SelloModificacion = Guid.NewGuid().ToString("N")
        };

        lock (EnviosEnMemoria)
        {
            EnviosEnMemoria.Insert(0, entidadEnvio);
        }

        RegistrarEventoHistorico(nuevoId, new EventoHistorialEnvioDto
        {
            EnvioId = nuevoId,
            FechaHora = DateTime.UtcNow,
            EstadoAnterior = null,
            NuevoEstado = EstadoEnvioDto.Admitido,
            Usuario = "Integración API",
            DescripcionEvento = $"Envío admitido por API del comercio{(referenciaExternaLimpia is not null ? $" (Ref: {referenciaExternaLimpia})" : "")}. Tarifa calculada: ${tarifaCalculada:F2}."
        });

        var respuesta = new RespuestaAltaEnvioApiDto(
            nuevoId,
            codigoSeguimiento,
            "Admitido",
            tarifaCalculada,
            identificacionesBultos,
            Duplicado: false);

        if (referenciaExternaLimpia is not null)
        {
            EnviosPorReferenciaApi[referenciaExternaLimpia] = respuesta;
        }

        return Task.FromResult(respuesta);
    }

    /// <summary>
    /// Genera las etiquetas físicas e imprimibles para uno o más envíos seleccionados en estado Admitido (UC-27).
    /// </summary>
    public Task<ResultadoGeneracionEtiquetasDto> GenerarEtiquetasEnviosAsync(
        IReadOnlyList<Guid> envioIds,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(envioIds);

        var idsUnicos = envioIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idsUnicos.Count == 0)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "Debe seleccionar al menos un envío válido para generar etiquetas.");
        }

        var etiquetas = new List<EtiquetaBultoDto>();

        lock (EnviosEnMemoria)
        {
            foreach (var envioId in idsUnicos)
            {
                var envio = EnviosEnMemoria.FirstOrDefault(e => e.Id == envioId);
                if (envio is null)
                {
                    throw new ApiExcepcion(HttpStatusCode.NotFound, $"No se encontró el envío con identificador '{envioId}'.");
                }

                // Precondición de UC-27: el envío debe estar en estado Admitido
                if (envio.Estado != EstadoEnvioDto.Admitido)
                {
                    throw new ApiExcepcion(
                        HttpStatusCode.BadRequest,
                        $"El envío '{envio.CodigoSeguimiento}' se encuentra en estado '{envio.Estado}'. Solo se pueden generar etiquetas para envíos en estado Admitido.");
                }

                var identificacionesBultos = (envio.BultosIdentificaciones != null && envio.BultosIdentificaciones.Count > 0)
                    ? envio.BultosIdentificaciones
                    : new[] { $"{envio.CodigoSeguimiento}-B01" };

                var totalBultos = identificacionesBultos.Count;
                var pesoTotal = envio.PesoKg.HasValue && envio.PesoKg.Value > 0 ? envio.PesoKg.Value : 1.0m;
                var pesoPorBulto = Math.Round(pesoTotal / totalBultos, 2);

                var operadorNombre = !string.IsNullOrWhiteSpace(_contextoOperador?.OperadorActivo?.NombreComercial)
                    ? _contextoOperador.OperadorActivo.NombreComercial
                    : (!string.IsNullOrWhiteSpace(envio.OperadorNombre)
                        ? envio.OperadorNombre
                        : "TransLogística Express");

                for (var i = 0; i < totalBultos; i++)
                {
                    etiquetas.Add(new EtiquetaBultoDto(
                        envio.Id,
                        envio.CodigoSeguimiento,
                        identificacionesBultos[i],
                        i + 1,
                        totalBultos,
                        string.IsNullOrWhiteSpace(envio.ComercioNombre) ? "MegaStore Uruguay" : envio.ComercioNombre,
                        operadorNombre,
                        envio.NombreDestinatario,
                        envio.DireccionDestino,
                        envio.TelefonoDestinatario,
                        string.IsNullOrWhiteSpace(envio.ModalidadServicio) ? "Estandar" : envio.ModalidadServicio,
                        pesoPorBulto,
                        envio.Observaciones,
                        envio.FechaCreacion));
                }
            }
        }

        var nombreArchivo = idsUnicos.Count == 1
            ? $"etiqueta-{etiquetas[0].CodigoSeguimiento.ToLowerInvariant()}.html"
            : $"etiquetas-envios-{DateTime.UtcNow:yyyyMMdd-HHmm}.html";

        var htmlDescargable = GeneradorDocumentoEtiquetas.ConstruirHtmlDescargable(etiquetas);

        return Task.FromResult(new ResultadoGeneracionEtiquetasDto(
            etiquetas,
            idsUnicos.Count,
            etiquetas.Count,
            nombreArchivo,
            htmlDescargable));
    }

    /// <summary>
    /// Confirma la recepción del bulto devuelto por parte del comercio, cerrando la devolución (UC-32).
    /// </summary>
    public Task<ConfirmarDevolucionResultDto> ConfirmarDevolucionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        if (envioId == Guid.Empty)
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El identificador del envío es inválido.");

        var envio = EnviosEnMemoria.FirstOrDefault(e => e.Id == envioId);
        if (envio is null)
            throw new ApiExcepcion(HttpStatusCode.NotFound, $"Envío '{envioId}' no encontrado.");

        // Idempotencia: si ya está devuelto, confirma sin duplicar
        if (envio.Estado == EstadoEnvioDto.Devuelto)
        {
            return Task.FromResult(new ConfirmarDevolucionResultDto(
                EnvioId: envio.Id,
                CodigoSeguimiento: envio.CodigoSeguimiento,
                EstadoEnvio: "Devuelto",
                EstadoDevolucion: "Rendida",
                Idempotente: true,
                Mensaje: "La devolución ya había sido confirmada previamente (operación idempotente)."));
        }

        if (envio.Estado != EstadoEnvioDto.EnDevolucion)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, "El envío no se encuentra en estado 'en devolución'.");
        }

        var indice = EnviosEnMemoria.FindIndex(e => e.Id == envioId);
        var envioActualizado = envio with
        {
            Estado = EstadoEnvioDto.Devuelto,
            SelloModificacion = Guid.NewGuid().ToString()
        };
        EnviosEnMemoria[indice] = envioActualizado;

        RegistrarEventoHistorico(envioId, new EventoHistorialEnvioDto
        {
            EnvioId = envioId,
            FechaHora = DateTime.UtcNow,
            EstadoAnterior = EstadoEnvioDto.EnDevolucion,
            NuevoEstado = EstadoEnvioDto.Devuelto,
            Usuario = "Portal Comercio",
            DescripcionEvento = "Recepción del bulto devuelto confirmada por el comercio. La devolución quedó cerrada exitosamente."
        });

        return Task.FromResult(new ConfirmarDevolucionResultDto(
            EnvioId: envio.Id,
            CodigoSeguimiento: envio.CodigoSeguimiento,
            EstadoEnvio: "Devuelto",
            EstadoDevolucion: "Rendida",
            Idempotente: false,
            Mensaje: "Recepción confirmada: la devolución quedó cerrada exitosamente."));
    }
}
