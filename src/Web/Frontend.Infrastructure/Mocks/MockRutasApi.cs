using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para el armado, validación y liberación de hojas de ruta (UC-08).
/// </summary>
public class MockRutasApi : IRutasApi
{
    private readonly IFlotaApi _flotaApi;
    private static readonly object _bloqueo = new();

    // Contador secuencial para numeración de hojas de ruta
    private static int _contadorRutas = 101;

    // Repositorio en memoria de hojas de ruta
    private static readonly List<HojaDeRutaDto> _hojasDeRuta = new();

    // Catálogo de envíos en depósito pendientes de asignar
    private static readonly List<EnvioPendienteRutaDto> _enviosEnDeposito = new();

    // Registro histórico de eventos de reasignación e incidencias en ruta (UC-10, RF-12, RF-15)
    private static readonly List<EventoReasignacionDto> _historialReasignaciones = new();

    internal static object BloqueoCompartido => _bloqueo;
    internal static List<HojaDeRutaDto> RepositorioHojasDeRuta => _hojasDeRuta;
    internal static List<EnvioPendienteRutaDto> RepositorioEnviosEnDeposito => _enviosEnDeposito;
    internal static List<EventoReasignacionDto> RepositorioHistorialReasignaciones => _historialReasignaciones;

    public MockRutasApi(IFlotaApi flotaApi)
    {
        _flotaApi = flotaApi;
        AsegurarInicializacion();
    }

    internal static void AsegurarInicializacion()
    {
        lock (_bloqueo)
        {
            if (_enviosEnDeposito.Count > 0 || _hojasDeRuta.Count > 0)
                return;

            // 1. Envíos iniciales en depósito pendientes de asignar
            _enviosEnDeposito.AddRange(new List<EnvioPendienteRutaDto>
            {
                new()
                {
                    Id = Guid.Parse("11111111-0001-0000-0000-000000000001"),
                    CodigoSeguimiento = "TRK-2026-1001",
                    Destinatario = "Lucía Fernández",
                    Direccion = "Bvar. Artigas 1234 Apto 302",
                    Zona = "Centro / Cordón",
                    FranjaHoraria = "09:00 - 13:00",
                    Turno = "Mañana",
                    PesoKg = 4.5m,
                    VolumenM3 = 0.025m,
                    Observaciones = "Portería de 8 a 14hs",
                    SelloModificacion = "sello-envio-1001"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0002-0000-0000-000000000002"),
                    CodigoSeguimiento = "TRK-2026-1002",
                    Destinatario = "Carlos Rodríguez",
                    Direccion = "Av. Rivera 2890",
                    Zona = "Pocitos / Punta Carretas",
                    FranjaHoraria = "09:00 - 13:00",
                    Turno = "Mañana",
                    PesoKg = 12.0m,
                    VolumenM3 = 0.080m,
                    Observaciones = "Timbre no funciona, llamar al llegar",
                    SelloModificacion = "sello-envio-1002"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0003-0000-0000-000000000003"),
                    CodigoSeguimiento = "TRK-2026-1003",
                    Destinatario = "Mariana González",
                    Direccion = "Colonia 1540 Apto 7",
                    Zona = "Centro / Cordón",
                    FranjaHoraria = "09:00 - 18:00",
                    Turno = "Todo el día",
                    PesoKg = 2.2m,
                    VolumenM3 = 0.015m,
                    Observaciones = "Dejar con vecino en puerta 8 si no contesta",
                    SelloModificacion = "sello-envio-1003"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0004-0000-0000-000000000004"),
                    CodigoSeguimiento = "TRK-2026-1004",
                    Destinatario = "Andrés Méndez",
                    Direccion = "Camino Carrasco 6120",
                    Zona = "Carrasco / Paso Carrasco",
                    FranjaHoraria = "14:00 - 18:00",
                    Turno = "Tarde",
                    PesoKg = 18.5m,
                    VolumenM3 = 0.120m,
                    Observaciones = "Paquete frágil de cristalería",
                    SelloModificacion = "sello-envio-1004"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0005-0000-0000-000000000005"),
                    CodigoSeguimiento = "TRK-2026-1005",
                    Destinatario = "Florencia Silva",
                    Direccion = "Sarandí 450",
                    Zona = "Ciudad Vieja",
                    FranjaHoraria = "09:00 - 13:00",
                    Turno = "Mañana",
                    PesoKg = 3.8m,
                    VolumenM3 = 0.018m,
                    Observaciones = "Zona peatonal, estacionar en Pérez Castellano",
                    SelloModificacion = "sello-envio-1005"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0006-0000-0000-000000000006"),
                    CodigoSeguimiento = "TRK-2026-1006",
                    Destinatario = "Martín Olivera",
                    Direccion = "Av. Giannattasio km 21",
                    Zona = "Costa de Oro / Canelones",
                    FranjaHoraria = "14:00 - 18:00",
                    Turno = "Tarde",
                    PesoKg = 28.0m,
                    VolumenM3 = 0.190m,
                    Observaciones = "Portón negro al fondo",
                    SelloModificacion = "sello-envio-1006"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0007-0000-0000-000000000007"),
                    CodigoSeguimiento = "TRK-2026-1007",
                    Destinatario = "Sofía Ramos",
                    Direccion = "21 de Setiembre 2714",
                    Zona = "Pocitos / Punta Carretas",
                    FranjaHoraria = "09:00 - 18:00",
                    Turno = "Todo el día",
                    PesoKg = 1.5m,
                    VolumenM3 = 0.010m,
                    Observaciones = null,
                    SelloModificacion = "sello-envio-1007"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0008-0000-0000-000000000008"),
                    CodigoSeguimiento = "TRK-2026-1008",
                    Destinatario = "Joaquín Pereyra",
                    Direccion = "Agraciada 3105",
                    Zona = "Paso Molino / Prado",
                    FranjaHoraria = "14:00 - 18:00",
                    Turno = "Tarde",
                    PesoKg = 15.0m,
                    VolumenM3 = 0.095m,
                    Observaciones = "Local comercial de repuestos",
                    SelloModificacion = "sello-envio-1008"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0009-0000-0000-000000000009"),
                    CodigoSeguimiento = "TRK-2026-1009",
                    Destinatario = "Valeria Castro",
                    Direccion = "Ellauri 350 Apto 901",
                    Zona = "Pocitos / Punta Carretas",
                    FranjaHoraria = "09:00 - 13:00",
                    Turno = "Mañana",
                    PesoKg = 8.2m,
                    VolumenM3 = 0.045m,
                    Observaciones = "Subir por ascensor principal",
                    SelloModificacion = "sello-envio-1009"
                },
                new()
                {
                    Id = Guid.Parse("11111111-0010-0000-0000-000000000010"),
                    CodigoSeguimiento = "TRK-2026-1010",
                    Destinatario = "Enzo Morales",
                    Direccion = "8 de Octubre 3812",
                    Zona = "Unión / Maroñas",
                    FranjaHoraria = "09:00 - 18:00",
                    Turno = "Todo el día",
                    PesoKg = 35.0m,
                    VolumenM3 = 0.220m,
                    Observaciones = "Caja pesada electrodoméstico",
                    SelloModificacion = "sello-envio-1010"
                }
            });

            // 2. Hojas de ruta preexistentes para pruebas de despacho y reasignación en ruta (UC-09, UC-10)
            var fechaHoy = DateOnly.FromDateTime(DateTime.UtcNow);

            // Ruta A: Liberada (sin despachar aún) - Lista para reasignación directa o despacho
            _hojasDeRuta.Add(new HojaDeRutaDto
            {
                Id = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
                NumeroRuta = "HR-2026-0100",
                Fecha = fechaHoy,
                ViajeNumero = 1,
                Turno = "Mañana",
                Estado = "Liberada",
                RepartidorId = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                RepartidorNombre = "Martín Cáceres",
                VehiculoId = Guid.Parse("22222222-2222-2222-2222-222222222201"),
                VehiculoMatricula = "STX-1204",
                VehiculoModelo = "Peugeot Partner",
                CapacidadPesoMaxKg = 650m,
                CapacidadVolumenMaxM3 = 3.3m,
                PesoTotalKg = 84.5m,
                VolumenTotalM3 = 0.54m,
                CantidadParadas = 3,
                FechaCreacion = DateTime.UtcNow.AddHours(-3),
                FechaLiberacion = DateTime.UtcNow.AddHours(-2).AddMinutes(-40),
                CriterioOrdenamiento = "FranjaHoraria",
                SelloModificacion = "sello-hr-100",
                Paradas = new List<ParadaRutaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 1,
                        EnvioId = Guid.Parse("99999999-0001-0000-0000-000000000001"),
                        CodigoSeguimiento = "TRK-2026-0920",
                        Destinatario = "Gonzalo Silva",
                        Direccion = "18 de Julio 1420",
                        Zona = "Centro",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 15.0m,
                        VolumenM3 = 0.08m,
                        Estado = "Pendiente"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 2,
                        EnvioId = Guid.Parse("99999999-0002-0000-0000-000000000002"),
                        CodigoSeguimiento = "TRK-2026-0921",
                        Destinatario = "Romina Cabrera",
                        Direccion = "San José 1110",
                        Zona = "Centro",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 32.5m,
                        VolumenM3 = 0.22m,
                        Estado = "Pendiente"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 3,
                        EnvioId = Guid.Parse("99999999-0003-0000-0000-000000000003"),
                        CodigoSeguimiento = "TRK-2026-0922",
                        Destinatario = "Federico Rossi",
                        Direccion = "Canelones 890",
                        Zona = "Cordón",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 37.0m,
                        VolumenM3 = 0.24m,
                        Estado = "Pendiente"
                    }
                }
            });

            // Ruta B: Despachada / En Tránsito (Para probar flujo alternativo de incidencia con vehículo en calle)
            _hojasDeRuta.Add(new HojaDeRutaDto
            {
                Id = Guid.Parse("aaaaaaaa-0002-0000-0000-000000000002"),
                NumeroRuta = "HR-2026-0099",
                Fecha = fechaHoy,
                ViajeNumero = 1,
                Turno = "Mañana",
                Estado = "Despachada",
                RepartidorId = Guid.Parse("33333333-3333-3333-3333-333333333302"),
                RepartidorNombre = "Matías Silvera",
                VehiculoId = Guid.Parse("22222222-2222-2222-2222-222222222202"),
                VehiculoMatricula = "STX 3190",
                VehiculoModelo = "Utilitario Ligero",
                CapacidadPesoMaxKg = 450m,
                CapacidadVolumenMaxM3 = 3.5m,
                PesoTotalKg = 61.0m,
                VolumenTotalM3 = 0.38m,
                CantidadParadas = 2,
                FechaCreacion = DateTime.UtcNow.AddHours(-4),
                FechaLiberacion = DateTime.UtcNow.AddHours(-3).AddMinutes(-30),
                FechaDespacho = DateTime.UtcNow.AddHours(-1).AddMinutes(-15),
                OperarioDespacho = "operario.deposito",
                CriterioOrdenamiento = "FranjaHoraria",
                SelloModificacion = "sello-hr-099",
                Paradas = new List<ParadaRutaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 1,
                        EnvioId = Guid.Parse("99999999-0004-0000-0000-000000000004"),
                        CodigoSeguimiento = "TRK-2026-0810",
                        Destinatario = "Lorena Barboza",
                        Direccion = "Bv. España 2130",
                        Zona = "Pocitos",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 21.0m,
                        VolumenM3 = 0.15m,
                        Estado = "EnTransito"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 2,
                        EnvioId = Guid.Parse("99999999-0005-0000-0000-000000000005"),
                        CodigoSeguimiento = "TRK-2026-0811",
                        Destinatario = "Sebastián Varela",
                        Direccion = "Benito Blanco 1020",
                        Zona = "Pocitos",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 40.0m,
                        VolumenM3 = 0.23m,
                        Estado = "EnTransito"
                    }
                }
            });

            // Ruta C: Liberada alternativa (Destino disponible para recibir reasignaciones)
            _hojasDeRuta.Add(new HojaDeRutaDto
            {
                Id = Guid.Parse("aaaaaaaa-0003-0000-0000-000000000003"),
                NumeroRuta = "HR-2026-0101",
                Fecha = fechaHoy,
                ViajeNumero = 1,
                Turno = "Mañana",
                Estado = "Liberada",
                RepartidorId = Guid.Parse("33333333-3333-3333-3333-333333333303"),
                RepartidorNombre = "Lucía Fernández",
                VehiculoId = Guid.Parse("22222222-2222-2222-2222-222222222204"),
                VehiculoMatricula = "STX 9088",
                VehiculoModelo = "Camioneta Pick-up",
                CapacidadPesoMaxKg = 750m,
                CapacidadVolumenMaxM3 = 4.8m,
                PesoTotalKg = 48.0m,
                VolumenTotalM3 = 0.31m,
                CantidadParadas = 2,
                FechaCreacion = DateTime.UtcNow.AddHours(-2),
                FechaLiberacion = DateTime.UtcNow.AddHours(-1).AddMinutes(-10),
                CriterioOrdenamiento = "FranjaHoraria",
                SelloModificacion = "sello-hr-101",
                Paradas = new List<ParadaRutaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 1,
                        EnvioId = Guid.Parse("99999999-0006-0000-0000-000000000006"),
                        CodigoSeguimiento = "TRK-2026-0930",
                        Destinatario = "Carla Domínguez",
                        Direccion = "Av. Brasil 2670",
                        Zona = "Pocitos",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 18.0m,
                        VolumenM3 = 0.11m,
                        Estado = "Pendiente"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Orden = 2,
                        EnvioId = Guid.Parse("99999999-0007-0000-0000-000000000007"),
                        CodigoSeguimiento = "TRK-2026-0931",
                        Destinatario = "Gabriel Suárez",
                        Direccion = "Libertad 2410",
                        Zona = "Punta Carretas",
                        FranjaHoraria = "09:00 - 13:00",
                        PesoKg = 30.0m,
                        VolumenM3 = 0.20m,
                        Estado = "Pendiente"
                    }
                }
            });

            // 3. Evento histórico inicial para log de auditoría (RF-12, RF-15)
            _historialReasignaciones.Add(new EventoReasignacionDto
            {
                Id = Guid.NewGuid(),
                FechaHora = DateTime.UtcNow.AddMinutes(-40),
                HojaDeRutaId = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
                HojaDeRutaNumero = "HR-2026-0100",
                EnvioId = Guid.NewGuid(),
                CodigoSeguimiento = "TRK-2026-0919",
                Destinatario = "Esteban Morales",
                Motivo = "Avería mecánica del vehículo (Ajuste preventivo)",
                Detalle = "Retirado preventivamente antes de la salida para equilibrar tara",
                AccionRealizada = "Retiro y Devolución a Depósito",
                DestinoNuevo = "En Depósito (Disponible para armado)",
                DespachadorUsuario = "despachador@translogistica.uy",
                NotificacionRepartidor = "Aviso emitido a App Repartidor (Martín Cáceres): Se retiró la parada TRK-2026-0919."
            });
        }
    }

    public Task<IReadOnlyList<HojaDeRutaDto>> ListarHojasDeRutaAsync(FiltroRutasDto? filtro = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var consulta = _hojasDeRuta.AsEnumerable();

            if (filtro != null)
            {
                if (filtro.Fecha.HasValue)
                {
                    consulta = consulta.Where(r => r.Fecha == filtro.Fecha.Value);
                }

                if (filtro.RepartidorId.HasValue && filtro.RepartidorId.Value != Guid.Empty)
                {
                    consulta = consulta.Where(r => r.RepartidorId == filtro.RepartidorId.Value);
                }

                if (!string.IsNullOrWhiteSpace(filtro.Estado) && filtro.Estado != "todos")
                {
                    consulta = consulta.Where(r => r.Estado.Equals(filtro.Estado.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(filtro.TerminoBusqueda))
                {
                    var termino = filtro.TerminoBusqueda.Trim().ToLowerInvariant();
                    consulta = consulta.Where(r =>
                        r.NumeroRuta.ToLowerInvariant().Contains(termino) ||
                        r.RepartidorNombre.ToLowerInvariant().Contains(termino) ||
                        r.VehiculoMatricula.ToLowerInvariant().Contains(termino));
                }
            }

            var resultado = consulta
                .OrderByDescending(r => r.Fecha)
                .ThenByDescending(r => r.ViajeNumero)
                .ThenByDescending(r => r.FechaCreacion)
                .ToList();

            return Task.FromResult<IReadOnlyList<HojaDeRutaDto>>(resultado);
        }
    }

    public Task<HojaDeRutaDto?> ObtenerHojaDeRutaPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var ruta = _hojasDeRuta.FirstOrDefault(r => r.Id == id);
            return Task.FromResult(ruta);
        }
    }

    public Task<IReadOnlyList<EnvioPendienteRutaDto>> ListarEnviosPendientesAsync(CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            return Task.FromResult<IReadOnlyList<EnvioPendienteRutaDto>>(_enviosEnDeposito.ToList());
        }
    }

    public async Task<ResultadoArmadoRutaDto> ValidarArmadoRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default)
    {
        var errores = new List<string>();
        var advertencias = new List<string>();

        // 1. Obtener Repartidor y Vehículo
        var repartidor = await _flotaApi.ObtenerRepartidorPorIdAsync(dto.RepartidorId, cancelacion);
        if (repartidor == null)
        {
            errores.Add("El repartidor seleccionado no existe o no está registrado en el sistema.");
        }

        var vehiculo = await _flotaApi.ObtenerVehiculoPorIdAsync(dto.VehiculoId, cancelacion);
        if (vehiculo == null)
        {
            errores.Add("El vehículo seleccionado no existe en la flota.");
        }
        else if (!vehiculo.Activo)
        {
            errores.Add($"El vehículo con patente {vehiculo.Patente} se encuentra inactivo.");
        }

        if (dto.EnviosIds == null || dto.EnviosIds.Count == 0)
        {
            errores.Add("Debe seleccionar al menos un envío en depósito para armar la hoja de ruta.");
            return new ResultadoArmadoRutaDto
            {
                Exitoso = false,
                Mensaje = "No se seleccionaron envíos.",
                ErroresValidacion = errores
            };
        }

        // 2. Límite máximo de paradas permitidas por ruta (Regla operativa RF-13)
        const int maximoParadas = 25;
        if (dto.EnviosIds.Count > maximoParadas)
        {
            errores.Add($"La hoja de ruta no puede superar el límite máximo de {maximoParadas} paradas (seleccionó {dto.EnviosIds.Count}).");
        }

        // 3. Simulación de Colisión de asignación concurrente (Flujo alternativo UC-08)
        if (dto.SimularColision)
        {
            return new ResultadoArmadoRutaDto
            {
                Exitoso = false,
                ColisionDetectada = true,
                Mensaje = "Colisión de asignación detectada: Otro despachador acaba de asignar uno de los envíos seleccionados a una ruta activa.",
                ErroresValidacion = new List<string>
                {
                    "El envío TRK-2026-1002 ya fue asignado a otra ruta activa de forma simultánea. Se canceló la operación para evitar duplicación de despacho."
                }
            };
        }

        // 4. Calcular pesos, volúmenes y verificar franjas horarias
        decimal pesoTotal = 0;
        decimal volumenTotal = 0;

        lock (_bloqueo)
        {
            foreach (var envioId in dto.EnviosIds)
            {
                var envio = _enviosEnDeposito.FirstOrDefault(e => e.Id == envioId);
                if (envio == null)
                {
                    errores.Add($"El envío con ID '{envioId}' ya no se encuentra en depósito o fue asignado previamente.");
                    continue;
                }

                pesoTotal += envio.PesoKg;
                volumenTotal += envio.VolumenM3;

                // Validación de compatibilidad de franja horaria con el turno de la ruta (RF-14)
                if (dto.Turno == "Mañana" && envio.Turno == "Tarde")
                {
                    errores.Add($"Incompatibilidad horaria: El envío {envio.CodigoSeguimiento} tiene franja comprometida '{envio.FranjaHoraria}' (Tarde) y no puede incluirse en el turno Mañana.");
                }
                else if (dto.Turno == "Tarde" && envio.Turno == "Mañana")
                {
                    errores.Add($"Incompatibilidad horaria: El envío {envio.CodigoSeguimiento} tiene franja comprometida '{envio.FranjaHoraria}' (Mañana) y no puede incluirse en el turno Tarde.");
                }
            }
        }

        // 5. Validación de capacidad de peso y volumen del vehículo (RF-15)
        if (vehiculo != null)
        {
            if (pesoTotal > vehiculo.CapacidadPeso)
            {
                var excesoPeso = pesoTotal - vehiculo.CapacidadPeso;
                errores.Add($"Capacidad de peso excedida: El peso total de los paquetes ({pesoTotal:N1} kg) supera la capacidad máxima del vehículo ({vehiculo.CapacidadPeso:N1} kg) por {excesoPeso:N1} kg.");
            }

            if (volumenTotal > vehiculo.CapacidadVolumen)
            {
                var excesoVolumen = volumenTotal - vehiculo.CapacidadVolumen;
                errores.Add($"Capacidad de volumen excedida: El volumen total ({volumenTotal:N3} m³) supera la capacidad máxima del vehículo ({vehiculo.CapacidadVolumen:N3} m³) por {excesoVolumen:N3} m³.");
            }
        }

        // Advertencia si la carga supera el 90%
        if (vehiculo != null && errores.Count == 0)
        {
            if (pesoTotal >= vehiculo.CapacidadPeso * 0.9m)
            {
                advertencias.Add("Atención: La carga del vehículo se encuentra al límite operativo (>90% de peso).");
            }
        }

        var exitoso = errores.Count == 0;
        return new ResultadoArmadoRutaDto
        {
            Exitoso = exitoso,
            Mensaje = exitoso ? "Validación satisfactoria de capacidad y horarios." : "No se puede armar la hoja de ruta debido a restricciones no cumplidas.",
            ErroresValidacion = errores,
            Advertencias = advertencias
        };
    }

    public async Task<ResultadoArmadoRutaDto> ArmarYConfirmarRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default)
    {
        // 1. Ejecutar validación previa completa
        var validacion = await ValidarArmadoRutaAsync(dto, cancelacion);
        if (!validacion.Exitoso)
        {
            return validacion;
        }

        var repartidor = await _flotaApi.ObtenerRepartidorPorIdAsync(dto.RepartidorId, cancelacion);
        var vehiculo = await _flotaApi.ObtenerVehiculoPorIdAsync(dto.VehiculoId, cancelacion);

        lock (_bloqueo)
        {
            // Extraer y retirar los envíos asignados de la lista de depósito
            var enviosAsignar = new List<EnvioPendienteRutaDto>();
            foreach (var id in dto.EnviosIds)
            {
                var envio = _enviosEnDeposito.FirstOrDefault(e => e.Id == id);
                if (envio != null)
                {
                    enviosAsignar.Add(envio);
                }
            }

            // Ordenamiento de paradas según criterio adoptado (RF-16)
            List<EnvioPendienteRutaDto> enviosOrdenados;
            if (dto.CriterioOrdenamiento == "FranjaHoraria")
            {
                // Primero Mañana, luego Todo el día, luego Tarde
                enviosOrdenados = enviosAsignar
                    .OrderBy(e => e.Turno switch
                    {
                        "Mañana" => 1,
                        "Todo el día" => 2,
                        "Tarde" => 3,
                        _ => 4
                    })
                    .ThenBy(e => e.Zona)
                    .ThenBy(e => e.Direccion)
                    .ToList();
            }
            else if (dto.CriterioOrdenamiento == "OptimoGeografico")
            {
                // Agrupado por zona geográfica para minimizar recorrido
                enviosOrdenados = enviosAsignar
                    .OrderBy(e => e.Zona)
                    .ThenBy(e => e.Direccion)
                    .ToList();
            }
            else
            {
                // Orden manual provisto
                enviosOrdenados = enviosAsignar.ToList();
            }

            // Crear las paradas
            var paradas = new List<ParadaRutaDto>();
            int secuencia = 1;
            decimal pesoTotal = 0;
            decimal volumenTotal = 0;

            foreach (var envio in enviosOrdenados)
            {
                paradas.Add(new ParadaRutaDto
                {
                    Id = Guid.NewGuid(),
                    Orden = secuencia++,
                    EnvioId = envio.Id,
                    CodigoSeguimiento = envio.CodigoSeguimiento,
                    Destinatario = envio.Destinatario,
                    Direccion = envio.Direccion,
                    Zona = envio.Zona,
                    FranjaHoraria = envio.FranjaHoraria,
                    PesoKg = envio.PesoKg,
                    VolumenM3 = envio.VolumenM3,
                    Estado = "Pendiente"
                });

                pesoTotal += envio.PesoKg;
                volumenTotal += envio.VolumenM3;

                // Retirar del depósito pendiente
                _enviosEnDeposito.Remove(envio);
            }

            var numeroRuta = $"HR-{dto.Fecha:yyyy}-{Interlocked.Increment(ref _contadorRutas):D4}";

            // Construir la nueva Hoja de Ruta Liberada (Postcondición UC-08)
            var nuevaRuta = new HojaDeRutaDto
            {
                Id = Guid.NewGuid(),
                NumeroRuta = numeroRuta,
                Fecha = dto.Fecha,
                ViajeNumero = dto.ViajeNumero,
                Turno = dto.Turno,
                Estado = "Liberada", // Liberada y disponible para la app móvil
                RepartidorId = repartidor!.Id,
                RepartidorNombre = repartidor.NombreCompleto,
                VehiculoId = vehiculo!.Id,
                VehiculoMatricula = vehiculo.Patente,
                VehiculoModelo = vehiculo.Tipo,
                CapacidadPesoMaxKg = vehiculo.CapacidadPeso,
                CapacidadVolumenMaxM3 = vehiculo.CapacidadVolumen,
                PesoTotalKg = pesoTotal,
                VolumenTotalM3 = volumenTotal,
                CantidadParadas = paradas.Count,
                Paradas = paradas,
                FechaCreacion = DateTime.UtcNow,
                FechaLiberacion = DateTime.UtcNow,
                CriterioOrdenamiento = dto.CriterioOrdenamiento,
                SelloModificacion = Guid.NewGuid().ToString("N")
            };

            _hojasDeRuta.Insert(0, nuevaRuta);

            return new ResultadoArmadoRutaDto
            {
                Exitoso = true,
                Mensaje = $"Hoja de ruta '{numeroRuta}' armada y liberada con éxito. Quedó disponible para la descarga en la app del repartidor.",
                HojaDeRuta = nuevaRuta
            };
        }
    }

    public Task<bool> LiberarHojaDeRutaAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var ruta = _hojasDeRuta.FirstOrDefault(r => r.Id == id);
            if (ruta == null)
                return Task.FromResult(false);

            if (ruta.Estado != "Liberada")
            {
                var indice = _hojasDeRuta.IndexOf(ruta);
                _hojasDeRuta[indice] = ruta with
                {
                    Estado = "Liberada",
                    FechaLiberacion = DateTime.UtcNow
                };
            }

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Ejecuta la reasignación, reprogramación o registro de no entrega de un envío en ruta (UC-10, RF-13, RF-15).
    /// </summary>
    public Task<ResultadoReasignacionDto> ReasignarOReprogramarEnvioAsync(ReasignarEnvioDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var rutaActual = _hojasDeRuta.FirstOrDefault(r => r.Id == dto.HojaDeRutaActualId);
            if (rutaActual == null)
            {
                return Task.FromResult(new ResultadoReasignacionDto
                {
                    Exitoso = false,
                    Mensaje = "La hoja de ruta de origen no fue encontrada en el sistema.",
                    Errores = new() { "Hoja de ruta origen inexistente." }
                });
            }

            var parada = rutaActual.Paradas.FirstOrDefault(p => p.EnvioId == dto.EnvioId || p.CodigoSeguimiento == dto.CodigoSeguimiento);
            if (parada == null)
            {
                return Task.FromResult(new ResultadoReasignacionDto
                {
                    Exitoso = false,
                    Mensaje = $"El envío '{dto.CodigoSeguimiento}' no se encuentra en la hoja de ruta '{rutaActual.NumeroRuta}'.",
                    Errores = new() { "Parada no localizada en la hoja de ruta indicada." }
                });
            }

            var motivoLegible = dto.MotivoIncidencia switch
            {
                "AveriaVehiculo" => "Avería mecánica del vehículo",
                "AusenciaRepartidor" => "Ausencia imprevista del repartidor",
                "IncidenciaVial" => "Incidencia vial o corte de tránsito",
                "SolicitudCliente" => "Solicitud directa del cliente / destinatario",
                "AjusteItinerario" => "Ajuste operativo de itinerario por el despachador",
                _ => dto.MotivoIncidencia
            };

            if (!string.IsNullOrWhiteSpace(dto.DetalleMotivo))
            {
                motivoLegible = $"{motivoLegible} ({dto.DetalleMotivo.Trim()})";
            }

            // Verificar si la ruta ya fue despachada hacia la calle
            var estaDespachada = rutaActual.Estado.Equals("Despachada", StringComparison.OrdinalIgnoreCase) ||
                                 rutaActual.Estado.Equals("EnTransito", StringComparison.OrdinalIgnoreCase);

            if (estaDespachada)
            {
                // Flujo alternativo (UC-10): Ruta ya despachada y en tránsito
                // Se registra como "No Entregado" con motivo interno del catálogo y vuelve a depósito en la rendición (UC-16).
                var codigoMotivo = string.IsNullOrWhiteSpace(dto.CodigoMotivoNoEntrega) ? "INCIDENCIA_DESPACHADOR" : dto.CodigoMotivoNoEntrega;

                var paradasModificadas = rutaActual.Paradas.Select(p =>
                {
                    if (p.EnvioId == parada.EnvioId)
                    {
                        return p with
                        {
                            Estado = "NoEntregado",
                            MotivoNoEntrega = codigoMotivo,
                            ObservacionesIncidencia = $"Cancelado en tránsito por despachador ({despachadorUsuario}): {motivoLegible}",
                            FechaIncidencia = DateTime.UtcNow,
                            HistorialReasignacion = $"Registrado como No Entregado ({codigoMotivo}). Bulto en retorno para rendición en depósito (UC-16)."
                        };
                    }
                    return p;
                }).ToList();

                var indiceRuta = _hojasDeRuta.IndexOf(rutaActual);
                _hojasDeRuta[indiceRuta] = rutaActual with
                {
                    Paradas = paradasModificadas,
                    SelloModificacion = Guid.NewGuid().ToString("N")
                };

                var notificacionRepartidor = $"ALERTA PRIORITARIA APP REPARTIDOR ({rutaActual.RepartidorNombre}): Omitir entrega de {parada.CodigoSeguimiento} en {parada.Direccion}. Motivo: {motivoLegible}. Retener paquete en vehículo para rendición en depósito (UC-16).";

                var eventoAuditoria = new EventoReasignacionDto
                {
                    HojaDeRutaId = rutaActual.Id,
                    HojaDeRutaNumero = rutaActual.NumeroRuta,
                    EnvioId = parada.EnvioId,
                    CodigoSeguimiento = parada.CodigoSeguimiento,
                    Destinatario = parada.Destinatario,
                    Motivo = motivoLegible,
                    Detalle = dto.DetalleMotivo,
                    AccionRealizada = "Registro No Entregado en Tránsito (Catálogo interno)",
                    DestinoNuevo = "Pendiente de Rendición de Depósito (UC-16)",
                    DespachadorUsuario = despachadorUsuario,
                    NotificacionRepartidor = notificacionRepartidor
                };
                _historialReasignaciones.Insert(0, eventoAuditoria);

                return Task.FromResult(new ResultadoReasignacionDto
                {
                    Exitoso = true,
                    Mensaje = $"Envío '{parada.CodigoSeguimiento}' registrado como 'No Entregado' en ruta despachada por {motivoLegible}. Se emitió alerta prioritaria al repartidor.",
                    NuevoEstadoEnvio = "No Entregado (Pendiente Rendición UC-16)",
                    NotificacionRepartidor = notificacionRepartidor,
                    RegistroAuditoria = $"Evento de auditoría registrado por el despachador {despachadorUsuario}."
                });
            }
            else
            {
                // Flujo principal (UC-10): Ruta aún no despachada (Borrador o Liberada)
                // Se retira la parada de la ruta actual
                var paradasRestantes = rutaActual.Paradas
                    .Where(p => p.EnvioId != parada.EnvioId)
                    .Select((p, idx) => p with { Orden = idx + 1 })
                    .ToList();

                var pesoActualizado = paradasRestantes.Sum(p => p.PesoKg);
                var volumenActualizado = paradasRestantes.Sum(p => p.VolumenM3);

                var indiceRutaActual = _hojasDeRuta.IndexOf(rutaActual);
                _hojasDeRuta[indiceRutaActual] = rutaActual with
                {
                    Paradas = paradasRestantes,
                    CantidadParadas = paradasRestantes.Count,
                    PesoTotalKg = pesoActualizado,
                    VolumenTotalM3 = volumenActualizado,
                    SelloModificacion = Guid.NewGuid().ToString("N")
                };

                if (dto.AccionDestino == "ReasignarOtraRuta" && dto.HojaDeRutaDestinoId.HasValue && dto.HojaDeRutaDestinoId.Value != Guid.Empty)
                {
                    var rutaDestino = _hojasDeRuta.FirstOrDefault(r => r.Id == dto.HojaDeRutaDestinoId.Value);
                    if (rutaDestino == null)
                    {
                        return Task.FromResult(new ResultadoReasignacionDto
                        {
                            Exitoso = false,
                            Mensaje = "La hoja de ruta de destino seleccionada no existe.",
                            Errores = new() { "Ruta destino inexistente." }
                        });
                    }

                    if (rutaDestino.Estado == "Despachada" || rutaDestino.Estado == "Finalizada")
                    {
                        return Task.FromResult(new ResultadoReasignacionDto
                        {
                            Exitoso = false,
                            Mensaje = $"No es posible reasignar a la ruta '{rutaDestino.NumeroRuta}' porque ya está {rutaDestino.Estado}.",
                            Errores = new() { "La ruta de destino no admite nuevas paradas." }
                        });
                    }

                    var pesoFinalDestino = rutaDestino.PesoTotalKg + parada.PesoKg;
                    var volumenFinalDestino = rutaDestino.VolumenTotalM3 + parada.VolumenM3;

                    if (pesoFinalDestino > rutaDestino.CapacidadPesoMaxKg)
                    {
                        return Task.FromResult(new ResultadoReasignacionDto
                        {
                            Exitoso = false,
                            Mensaje = $"Capacidad de peso superada en '{rutaDestino.NumeroRuta}': requeriría {pesoFinalDestino:N1} kg pero el límite es {rutaDestino.CapacidadPesoMaxKg:N0} kg.",
                            Errores = new() { "Capacidad de peso del vehículo destino excedida." }
                        });
                    }

                    if (volumenFinalDestino > rutaDestino.CapacidadVolumenMaxM3)
                    {
                        return Task.FromResult(new ResultadoReasignacionDto
                        {
                            Exitoso = false,
                            Mensaje = $"Capacidad de volumen superada en '{rutaDestino.NumeroRuta}': requeriría {volumenFinalDestino:N2} m³ pero el límite es {rutaDestino.CapacidadVolumenMaxM3:N1} m³.",
                            Errores = new() { "Capacidad de volumen del vehículo destino excedida." }
                        });
                    }

                    var paradasDestino = rutaDestino.Paradas.ToList();
                    paradasDestino.Add(parada with
                    {
                        Id = Guid.NewGuid(),
                        Orden = paradasDestino.Count + 1,
                        Estado = "Pendiente",
                        HistorialReasignacion = $"Reasignado desde {rutaActual.NumeroRuta} por {motivoLegible} ({DateTime.UtcNow:dd/MM/yyyy HH:mm})."
                    });

                    var indiceDestino = _hojasDeRuta.IndexOf(rutaDestino);
                    _hojasDeRuta[indiceDestino] = rutaDestino with
                    {
                        Paradas = paradasDestino,
                        CantidadParadas = paradasDestino.Count,
                        PesoTotalKg = pesoFinalDestino,
                        VolumenTotalM3 = volumenFinalDestino,
                        SelloModificacion = Guid.NewGuid().ToString("N")
                    };

                    var notificacion = $"Sincronización de apps móviles: Se retiró el envío {parada.CodigoSeguimiento} de la hoja de {rutaActual.RepartidorNombre} ({rutaActual.NumeroRuta}) y se asignó a {rutaDestino.RepartidorNombre} ({rutaDestino.NumeroRuta}).";

                    var eventoAuditoria = new EventoReasignacionDto
                    {
                        HojaDeRutaId = rutaActual.Id,
                        HojaDeRutaNumero = rutaActual.NumeroRuta,
                        EnvioId = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Motivo = motivoLegible,
                        Detalle = dto.DetalleMotivo,
                        AccionRealizada = "Reasignación a otra Hoja de Ruta",
                        DestinoNuevo = $"{rutaDestino.NumeroRuta} ({rutaDestino.RepartidorNombre})",
                        DespachadorUsuario = despachadorUsuario,
                        NotificacionRepartidor = notificacion
                    };
                    _historialReasignaciones.Insert(0, eventoAuditoria);

                    return Task.FromResult(new ResultadoReasignacionDto
                    {
                        Exitoso = true,
                        Mensaje = $"Envío '{parada.CodigoSeguimiento}' retirado de '{rutaActual.NumeroRuta}' y reasignado a '{rutaDestino.NumeroRuta}'.",
                        NuevoEstadoEnvio = "Asignado a Ruta",
                        NotificacionRepartidor = notificacion,
                        RegistroAuditoria = $"Evento histórico registrado por el despachador {despachadorUsuario}."
                    });
                }
                else if (dto.AccionDestino == "ReprogramarFranja")
                {
                    var franjaNueva = !string.IsNullOrWhiteSpace(dto.NuevaFranjaHoraria) ? dto.NuevaFranjaHoraria : parada.FranjaHoraria;
                    var turnoNuevo = !string.IsNullOrWhiteSpace(dto.NuevoTurno) ? dto.NuevoTurno : rutaActual.Turno;
                    var fechaTexto = dto.NuevaFecha.HasValue ? dto.NuevaFecha.Value.ToString("dd/MM/yyyy") : "Próxima jornada";

                    _enviosEnDeposito.Add(new EnvioPendienteRutaDto
                    {
                        Id = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Direccion = parada.Direccion,
                        Zona = parada.Zona,
                        FranjaHoraria = franjaNueva,
                        Turno = turnoNuevo,
                        PesoKg = parada.PesoKg,
                        VolumenM3 = parada.VolumenM3,
                        Observaciones = $"Reprogramado ({fechaTexto} - {franjaNueva}): {motivoLegible}",
                        SelloModificacion = Guid.NewGuid().ToString("N")
                    });

                    var notificacion = $"Aviso enviado a la app del repartidor ({rutaActual.RepartidorNombre}): Se retiró la parada {parada.CodigoSeguimiento} por reprogramación de franja ({fechaTexto} {franjaNueva}).";

                    var eventoAuditoria = new EventoReasignacionDto
                    {
                        HojaDeRutaId = rutaActual.Id,
                        HojaDeRutaNumero = rutaActual.NumeroRuta,
                        EnvioId = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Motivo = motivoLegible,
                        Detalle = dto.DetalleMotivo,
                        AccionRealizada = "Reprogramación de Franja Horaria",
                        DestinoNuevo = $"En Depósito (Reprogramado para {fechaTexto} en {franjaNueva})",
                        DespachadorUsuario = despachadorUsuario,
                        NotificacionRepartidor = notificacion
                    };
                    _historialReasignaciones.Insert(0, eventoAuditoria);

                    return Task.FromResult(new ResultadoReasignacionDto
                    {
                        Exitoso = true,
                        Mensaje = $"Envío '{parada.CodigoSeguimiento}' reprogramado para {fechaTexto} ({franjaNueva}). Reintegrado a depósito para planificación.",
                        NuevoEstadoEnvio = "En Depósito",
                        NotificacionRepartidor = notificacion,
                        RegistroAuditoria = $"Evento histórico registrado por el despachador {despachadorUsuario}."
                    });
                }
                else
                {
                    // VolverADeposito
                    _enviosEnDeposito.Add(new EnvioPendienteRutaDto
                    {
                        Id = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Direccion = parada.Direccion,
                        Zona = parada.Zona,
                        FranjaHoraria = parada.FranjaHoraria,
                        Turno = rutaActual.Turno,
                        PesoKg = parada.PesoKg,
                        VolumenM3 = parada.VolumenM3,
                        Observaciones = $"Retirado de {rutaActual.NumeroRuta} por incidencia: {motivoLegible}",
                        SelloModificacion = Guid.NewGuid().ToString("N")
                    });

                    var notificacion = $"Aviso enviado a la app del repartidor ({rutaActual.RepartidorNombre}): Se retiró la parada {parada.CodigoSeguimiento}. El bulto permanece en depósito.";

                    var eventoAuditoria = new EventoReasignacionDto
                    {
                        HojaDeRutaId = rutaActual.Id,
                        HojaDeRutaNumero = rutaActual.NumeroRuta,
                        EnvioId = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Motivo = motivoLegible,
                        Detalle = dto.DetalleMotivo,
                        AccionRealizada = "Retiro y Devolución a Depósito",
                        DestinoNuevo = "En Depósito (Disponible para armado)",
                        DespachadorUsuario = despachadorUsuario,
                        NotificacionRepartidor = notificacion
                    };
                    _historialReasignaciones.Insert(0, eventoAuditoria);

                    return Task.FromResult(new ResultadoReasignacionDto
                    {
                        Exitoso = true,
                        Mensaje = $"Envío '{parada.CodigoSeguimiento}' retirado de '{rutaActual.NumeroRuta}' y devuelto a 'En Depósito'.",
                        NuevoEstadoEnvio = "En Depósito",
                        NotificacionRepartidor = notificacion,
                        RegistroAuditoria = $"Evento histórico registrado por el despachador {despachadorUsuario}."
                    });
                }
            }
        }
    }

    /// <summary>
    /// Consulta el log de eventos de reasignaciones e incidencias en ruta (RF-12, RF-15).
    /// </summary>
    public Task<IReadOnlyList<EventoReasignacionDto>> ListarEventosReasignacionAsync(Guid? hojaDeRutaId = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var consulta = _historialReasignaciones.AsEnumerable();
            if (hojaDeRutaId.HasValue && hojaDeRutaId.Value != Guid.Empty)
            {
                consulta = consulta.Where(e => e.HojaDeRutaId == hojaDeRutaId.Value);
            }

            var resultado = consulta.OrderByDescending(e => e.FechaHora).ToList();
            return Task.FromResult<IReadOnlyList<EventoReasignacionDto>>(resultado);
        }
    }
}
