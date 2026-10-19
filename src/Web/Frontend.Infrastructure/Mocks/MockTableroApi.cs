using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del monitoreo del tablero de operaciones en vivo (UC-12, RF-29).
/// </summary>
public class MockTableroApi : ITableroApi
{
    private readonly List<VehiculoEnVivoDto> _vehiculos;
    private readonly List<string> _registroInstrucciones = new();

    public MockTableroApi()
    {
        var ahora = DateTime.UtcNow;

        var enviosRiesgoRuta1 = new List<EnvioRiesgoDto>
        {
            new()
            {
                EnvioId = Guid.Parse("aa111111-1111-1111-1111-111111111111"),
                HojaDeRutaId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                NumeroRuta = "HDR-2026-0081",
                CodigoSeguimiento = "ENV-2026-1011",
                Destinatario = "Farmacia San Roque Pocitos",
                DestinatarioTelefono = "099 876 543",
                Direccion = "Av. Brasil 2890 esq. Benito Blanco",
                Zona = "Pocitos & Punta Carretas",
                FranjaHoraria = "09:00 - 12:00",
                HoraLimiteVentana = ahora.Date.AddHours(12),
                HoraEstimadaLlegada = ahora.Date.AddHours(12).AddMinutes(35),
                MinutosRetrasoEstimado = 35,
                NivelRiesgo = "Critico",
                CausaRiesgo = "Tráfico detenido por obras en Bulevar España.",
                RepartidorNombre = "Juan Pérez",
                RepartidorTelefono = "099 123 456",
                VehiculoMatricula = "STX-1021",
                Latitud = -34.9165,
                Longitud = -56.1510
            }
        };

        var enviosRiesgoRuta2 = new List<EnvioRiesgoDto>
        {
            new()
            {
                EnvioId = Guid.Parse("bb222222-2222-2222-2222-222222222222"),
                HojaDeRutaId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                NumeroRuta = "HDR-2026-0082",
                CodigoSeguimiento = "ENV-2026-1014",
                Destinatario = "Dra. Valeria Ramos",
                DestinatarioTelefono = "098 765 432",
                Direccion = "Av. Agraciada 3100",
                Zona = "Prado & Bella Vista",
                FranjaHoraria = "10:00 - 13:00",
                HoraLimiteVentana = ahora.Date.AddHours(13),
                HoraEstimadaLlegada = ahora.Date.AddHours(13).AddMinutes(40),
                MinutosRetrasoEstimado = 40,
                NivelRiesgo = "Critico",
                CausaRiesgo = "Vehículo inmovilizado por falla de frenos (Incidencia INC-2026-0041).",
                RepartidorNombre = "Lucía Fernández",
                RepartidorTelefono = "098 456 789",
                VehiculoMatricula = "STX-1029",
                Latitud = -34.8690,
                Longitud = -56.2045
            },
            new()
            {
                EnvioId = Guid.Parse("bb333333-3333-3333-3333-333333333333"),
                HojaDeRutaId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                NumeroRuta = "HDR-2026-0082",
                CodigoSeguimiento = "ENV-2026-1015",
                Destinatario = "Óptica Bella Vista",
                DestinatarioTelefono = "094 321 987",
                Direccion = "Lucas Obes 1240",
                Zona = "Prado & Bella Vista",
                FranjaHoraria = "10:00 - 13:00",
                HoraLimiteVentana = ahora.Date.AddHours(13),
                HoraEstimadaLlegada = ahora.Date.AddHours(13).AddMinutes(50),
                MinutosRetrasoEstimado = 50,
                NivelRiesgo = "Critico",
                CausaRiesgo = "Pendiente de trasbordo a vehículo sustituto.",
                RepartidorNombre = "Lucía Fernández",
                RepartidorTelefono = "098 456 789",
                VehiculoMatricula = "STX-1029",
                Latitud = -34.8640,
                Longitud = -56.2090
            }
        };

        var enviosRiesgoRuta4 = new List<EnvioRiesgoDto>
        {
            new()
            {
                EnvioId = Guid.Parse("dd444444-4444-4444-4444-444444444444"),
                HojaDeRutaId = Guid.Parse("d4e5f6a7-8901-2345-defa-123456789012"),
                NumeroRuta = "HDR-2026-0084",
                CodigoSeguimiento = "ENV-2026-1022",
                Destinatario = "Veterinaria Malvín",
                DestinatarioTelefono = "092 112 233",
                Direccion = "Aconcagua 4810 esq. Michigan",
                Zona = "Malvín & Punta Gorda",
                FranjaHoraria = "13:00 - 16:00",
                HoraLimiteVentana = ahora.Date.AddHours(16),
                HoraEstimadaLlegada = ahora.Date.AddHours(16).AddMinutes(20),
                MinutosRetrasoEstimado = 20,
                NivelRiesgo = "Moderado",
                CausaRiesgo = "Demora acumulada en descarga de bultos voluminosos.",
                RepartidorNombre = "Martín Silva",
                RepartidorTelefono = "092 334 455",
                VehiculoMatricula = "STX-1077",
                Latitud = -34.8940,
                Longitud = -56.0950
            }
        };

        _vehiculos = new List<VehiculoEnVivoDto>
        {
            new()
            {
                HojaDeRutaId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                NumeroRuta = "HDR-2026-0081",
                RepartidorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                RepartidorNombre = "Juan Pérez",
                RepartidorTelefono = "099 123 456",
                VehiculoId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehiculoMatricula = "STX-1021",
                VehiculoModelo = "Renault Kangoo Maxi",
                Latitud = -34.9150,
                Longitud = -56.1520,
                DireccionAproximada = "Bvar. España y Libertad, Pocitos",
                ZonaActual = "Pocitos & Punta Carretas",
                NivelBateriaPct = 84,
                VelocidadKmh = 26,
                UltimaActualizacion = ahora.AddMinutes(-2),
                EstadoOperativo = "EnMovimiento",
                ParadasCompletadas = 4,
                TotalParadas = 8,
                ParadaActualCodigo = "ENV-2026-1011",
                ParadaActualDireccion = "Av. Brasil 2890",
                TieneAlertasRiesgo = true,
                CantidadEnviosEnRiesgo = 1,
                EnviosEnRiesgo = enviosRiesgoRuta1,
                Paradas = new List<ParadaEnVivoDto>
                {
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1008", Orden = 1, Destinatario = "Comercio Alfa", Direccion = "Ellauri 350", Latitud = -34.9210, Longitud = -56.1580, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1009", Orden = 2, Destinatario = "Estudio Jurídico", Direccion = "Sarmiento 2210", Latitud = -34.9190, Longitud = -56.1540, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1010", Orden = 3, Destinatario = "Librería del Parque", Direccion = "21 de Setiembre 2700", Latitud = -34.9170, Longitud = -56.1530, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1007", Orden = 4, Destinatario = "Residencia Particular", Direccion = "Libertad 2500", Latitud = -34.9155, Longitud = -56.1525, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = enviosRiesgoRuta1[0].EnvioId, CodigoSeguimiento = "ENV-2026-1011", Orden = 5, Destinatario = "Farmacia San Roque", Direccion = "Av. Brasil 2890", Latitud = -34.9165, Longitud = -56.1510, FranjaHoraria = "09:00 - 12:00", Estado = "EnCurso", EnRiesgo = true, MotivoRiesgo = "Tráfico en Bvar. España" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1012", Orden = 6, Destinatario = "Consultorio Odontológico", Direccion = "Benito Blanco 1120", Latitud = -34.9140, Longitud = -56.1490, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1013", Orden = 7, Destinatario = "Boutique Pocitos", Direccion = "Gabriel Pereira 3120", Latitud = -34.9120, Longitud = -56.1500, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1016", Orden = 8, Destinatario = "Café Gourmet", Direccion = "Scoseria 2540", Latitud = -34.9180, Longitud = -56.1560, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" }
                }
            },
            new()
            {
                HojaDeRutaId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                NumeroRuta = "HDR-2026-0082",
                RepartidorId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                RepartidorNombre = "Lucía Fernández",
                RepartidorTelefono = "098 456 789",
                VehiculoId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                VehiculoMatricula = "STX-1029",
                VehiculoModelo = "Peugeot Partner HDi",
                Latitud = -34.8680,
                Longitud = -56.2050,
                DireccionAproximada = "Agraciada y Suárez, Bella Vista",
                ZonaActual = "Prado & Bella Vista",
                NivelBateriaPct = 62,
                VelocidadKmh = 0,
                UltimaActualizacion = ahora.AddMinutes(-1),
                EstadoOperativo = "DetenidoAlerta",
                ParadasCompletadas = 2,
                TotalParadas = 7,
                ParadaActualCodigo = "ENV-2026-1014",
                ParadaActualDireccion = "Av. Agraciada 3100",
                TieneAlertasRiesgo = true,
                CantidadEnviosEnRiesgo = 2,
                EnviosEnRiesgo = enviosRiesgoRuta2,
                Paradas = new List<ParadaEnVivoDto>
                {
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1002", Orden = 1, Destinatario = "Taller Central", Direccion = "San Fructuoso 1040", Latitud = -34.8720, Longitud = -56.2010, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1003", Orden = 2, Destinatario = "Ferretería Bella Vista", Direccion = "Zapicán 2500", Latitud = -34.8700, Longitud = -56.2030, FranjaHoraria = "09:00 - 12:00", Estado = "Entregado" },
                    new() { EnvioId = enviosRiesgoRuta2[0].EnvioId, CodigoSeguimiento = "ENV-2026-1014", Orden = 3, Destinatario = "Dra. Valeria Ramos", Direccion = "Av. Agraciada 3100", Latitud = -34.8690, Longitud = -56.2045, FranjaHoraria = "10:00 - 13:00", Estado = "EnCurso", EnRiesgo = true, MotivoRiesgo = "Avería mecánica del vehículo" },
                    new() { EnvioId = enviosRiesgoRuta2[1].EnvioId, CodigoSeguimiento = "ENV-2026-1015", Orden = 4, Destinatario = "Óptica Bella Vista", Direccion = "Lucas Obes 1240", Latitud = -34.8640, Longitud = -56.2090, FranjaHoraria = "10:00 - 13:00", Estado = "Pendiente", EnRiesgo = true, MotivoRiesgo = "Retraso derivado de auxilio" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1017", Orden = 5, Destinatario = "Colegio Prado", Direccion = "Av. Millán 3800", Latitud = -34.8590, Longitud = -56.2070, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1018", Orden = 6, Destinatario = "Club Bella Vista", Direccion = "Av. Agraciada 3400", Latitud = -34.8660, Longitud = -56.2060, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1019", Orden = 7, Destinatario = "Depósito Agraciada", Direccion = "García Morales 1120", Latitud = -34.8630, Longitud = -56.2020, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" }
                }
            },
            new()
            {
                HojaDeRutaId = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
                NumeroRuta = "HDR-2026-0083",
                RepartidorId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                RepartidorNombre = "Carlos Gómez",
                RepartidorTelefono = "091 876 543",
                VehiculoId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                VehiculoMatricula = "STX-1045",
                VehiculoModelo = "Mercedes-Benz Sprinter 313",
                Latitud = -34.9030,
                Longitud = -56.1820,
                DireccionAproximada = "18 de Julio y Ejido, Centro",
                ZonaActual = "Centro & Cordón",
                NivelBateriaPct = 95,
                VelocidadKmh = 22,
                UltimaActualizacion = ahora.AddMinutes(-3),
                EstadoOperativo = "EnMovimiento",
                ParadasCompletadas = 7,
                TotalParadas = 10,
                ParadaActualCodigo = "ENV-2026-1020",
                ParadaActualDireccion = "18 de Julio 1250",
                TieneAlertasRiesgo = false,
                CantidadEnviosEnRiesgo = 0,
                EnviosEnRiesgo = new List<EnvioRiesgoDto>(),
                Paradas = new List<ParadaEnVivoDto>
                {
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1020", Orden = 8, Destinatario = "Librería Central", Direccion = "18 de Julio 1250", Latitud = -34.9035, Longitud = -56.1825, FranjaHoraria = "13:00 - 16:00", Estado = "EnCurso" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1021", Orden = 9, Destinatario = "Farmacia Cordón", Direccion = "Colonia 1640", Latitud = -34.9020, Longitud = -56.1790, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1025", Orden = 10, Destinatario = "Oficina Comercial", Direccion = "Guayabos 1720", Latitud = -34.9040, Longitud = -56.1780, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" }
                }
            },
            new()
            {
                HojaDeRutaId = Guid.Parse("d4e5f6a7-8901-2345-defa-123456789012"),
                NumeroRuta = "HDR-2026-0084",
                RepartidorId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                RepartidorNombre = "Martín Silva",
                RepartidorTelefono = "092 334 455",
                VehiculoId = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                VehiculoMatricula = "STX-1077",
                VehiculoModelo = "Fiat Fiorino 1.4 EVO",
                Latitud = -34.8920,
                Longitud = -56.0980,
                DireccionAproximada = "Av. Rivera y Concepción del Uruguay, Malvín",
                ZonaActual = "Malvín & Punta Gorda",
                NivelBateriaPct = 78,
                VelocidadKmh = 35,
                UltimaActualizacion = ahora.AddMinutes(-1),
                EstadoOperativo = "EnMovimiento",
                ParadasCompletadas = 5,
                TotalParadas = 9,
                ParadaActualCodigo = "ENV-2026-1022",
                ParadaActualDireccion = "Aconcagua 4810",
                TieneAlertasRiesgo = true,
                CantidadEnviosEnRiesgo = 1,
                EnviosEnRiesgo = enviosRiesgoRuta4,
                Paradas = new List<ParadaEnVivoDto>
                {
                    new() { EnvioId = enviosRiesgoRuta4[0].EnvioId, CodigoSeguimiento = "ENV-2026-1022", Orden = 6, Destinatario = "Veterinaria Malvín", Direccion = "Aconcagua 4810", Latitud = -34.8940, Longitud = -56.0950, FranjaHoraria = "13:00 - 16:00", Estado = "EnCurso", EnRiesgo = true, MotivoRiesgo = "Retraso de 20 min" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1023", Orden = 7, Destinatario = "Residencia Malvín", Direccion = "Rambla O'Higgins 5100", Latitud = -34.8960, Longitud = -56.0920, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1024", Orden = 8, Destinatario = "Gimnasio Punta Gorda", Direccion = "General Paz 1420", Latitud = -34.8910, Longitud = -56.0880, FranjaHoraria = "13:00 - 16:00", Estado = "Pendiente" },
                    new() { EnvioId = Guid.NewGuid(), CodigoSeguimiento = "ENV-2026-1026", Orden = 9, Destinatario = "Supermercado Costa", Direccion = "Coimbra 6200", Latitud = -34.8890, Longitud = -56.0850, FranjaHoraria = "16:00 - 19:00", Estado = "Pendiente" }
                }
            }
        };
    }

    public Task<ResumenTableroDto> ObtenerResumenEnVivoAsync(CancellationToken cancelacion = default)
    {
        var todasLasAlertas = _vehiculos
            .SelectMany(v => v.EnviosEnRiesgo)
            .OrderByDescending(a => a.MinutosRetrasoEstimado)
            .ToList();

        var totalParadasCompletadas = _vehiculos.Sum(v => v.ParadasCompletadas);
        var totalParadasPendientes = _vehiculos.Sum(v => v.TotalParadas - v.ParadasCompletadas);

        var resumen = new ResumenTableroDto
        {
            TotalRutasEnEjecucion = _vehiculos.Count,
            TotalRepartidoresEnCalle = _vehiculos.Select(v => v.RepartidorId).Distinct().Count(),
            TotalEnviosEnTransito = totalParadasPendientes,
            TotalEnviosEntregadosHoy = totalParadasCompletadas,
            TotalEnviosEnRiesgo = todasLasAlertas.Count,
            UltimaSincronizacion = DateTime.UtcNow,
            Vehiculos = _vehiculos,
            AlertasRiesgo = todasLasAlertas
        };

        return Task.FromResult(resumen);
    }

    public Task<VehiculoEnVivoDto?> ObtenerDetalleVehiculoEnVivoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        var vehiculo = _vehiculos.FirstOrDefault(v => v.HojaDeRutaId == hojaDeRutaId);
        return Task.FromResult(vehiculo);
    }

    public Task<bool> EnviarInstruccionRepartidorAsync(Guid repartidorId, string mensaje, CancellationToken cancelacion = default)
    {
        var registro = $"[{DateTime.UtcNow:HH:mm:ss}] Para Repartidor {repartidorId}: {mensaje}";
        _registroInstrucciones.Add(registro);
        return Task.FromResult(true);
    }
}
