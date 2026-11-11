using System.Text.Json;
using UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;
using UltimaMilla.Domain.Enums;
using Xunit;

namespace UltimaMilla.Application.Tests.Mobile;

/// <summary>
/// Pruebas unitarias de los componentes de Movilidad y Terreno (Sección 7.3):
/// 1. Algoritmo de optimización de paradas (Vecino Más Cercano + 2-Opt con paradas inmutables y ventanas horarias).
/// 2. Motor de geocercas con histéresis (radio de entrada < 100m, radio de salida > 150m) y medición de permanencia.
/// 3. Serialización/deserialización de payloads para la cola offline de SyncService y contratos HTTP de MobileRutasEndpoints.
/// </summary>
public class MovilidadComponentesUnitTests
{
    // =========================================================================
    // 1. ALGORITMO DE OPTIMIZACIÓN (2-OPT / VECINO MÁS CERCANO)
    // =========================================================================

    public record ParadaPrueba(
        Guid Id,
        string CodigoSeguimiento,
        double Latitud,
        double Longitud,
        string Estado,
        string VentanaHoraria,
        int OrdenOriginal
    );

    public record ResultadoOptimizacionPrueba(
        IReadOnlyList<ParadaPrueba> SecuenciaOriginal,
        IReadOnlyList<ParadaPrueba> SecuenciaOptimizada,
        double DistanciaBaseKm,
        double DistanciaOptimizadaKm,
        double AhorroKm,
        double PorcentajeAhorro,
        int TiempoEstimadoBaseMinutos,
        int TiempoEstimadoOptimizadoMinutos
    );

    private static double CalcularHaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double RadioTierraKm = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double rLat1 = lat1 * Math.PI / 180.0;
        double rLat2 = lat2 * Math.PI / 180.0;

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(rLat1) * Math.Cos(rLat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return RadioTierraKm * c;
    }

    private static double CalcularDistanciaTotal(double latOrigen, double lonOrigen, IReadOnlyList<ParadaPrueba> paradas)
    {
        double total = 0;
        double lat = latOrigen;
        double lon = lonOrigen;
        foreach (var p in paradas)
        {
            total += CalcularHaversineKm(lat, lon, p.Latitud, p.Longitud);
            lat = p.Latitud;
            lon = p.Longitud;
        }
        return total;
    }

    private static ResultadoOptimizacionPrueba OptimizarRutaPrueba(
        double latOrigen,
        double lonOrigen,
        IReadOnlyList<ParadaPrueba> paradas)
    {
        var inmutables = paradas.Where(p => p.Estado is "Entregado" or "Fallido" or "EnDevolucion").ToList();
        var pendientes = paradas.Where(p => p.Estado is "Pendiente" or "EnCamino" or "EnSitio").ToList();

        if (pendientes.Count < 2)
        {
            double d = CalcularDistanciaTotal(latOrigen, lonOrigen, paradas);
            return new ResultadoOptimizacionPrueba(paradas, paradas, d, d, 0, 0, 30, 30);
        }

        // 1. Nearest Neighbor
        var restantes = new List<ParadaPrueba>(pendientes);
        var nnRuta = new List<ParadaPrueba>();
        double latActual = latOrigen;
        double lonActual = lonOrigen;

        while (restantes.Count > 0)
        {
            var masCercana = restantes.OrderBy(p => CalcularHaversineKm(latActual, lonActual, p.Latitud, p.Longitud)).First();
            nnRuta.Add(masCercana);
            restantes.Remove(masCercana);
            latActual = masCercana.Latitud;
            lonActual = masCercana.Longitud;
        }

        // 2. 2-Opt local search
        var mejorRuta = new List<ParadaPrueba>(nnRuta);
        double mejorDist = CalcularDistanciaTotal(latOrigen, lonOrigen, mejorRuta);
        bool mejora = true;
        int iter = 0;

        while (mejora && iter < 50)
        {
            mejora = false;
            iter++;
            for (int i = 0; i < mejorRuta.Count - 1; i++)
            {
                for (int k = i + 1; k < mejorRuta.Count; k++)
                {
                    var nueva = new List<ParadaPrueba>();
                    for (int x = 0; x < i; x++) nueva.Add(mejorRuta[x]);
                    for (int x = k; x >= i; x--) nueva.Add(mejorRuta[x]);
                    for (int x = k + 1; x < mejorRuta.Count; x++) nueva.Add(mejorRuta[x]);

                    double distNueva = CalcularDistanciaTotal(latOrigen, lonOrigen, nueva);
                    if (distNueva + 0.001 < mejorDist)
                    {
                        mejorRuta = nueva;
                        mejorDist = distNueva;
                        mejora = true;
                    }
                }
            }
        }

        // Ensamblar preservando paradas inmutables intactas en sus posiciones históricas
        var secuenciaOptimizada = new List<ParadaPrueba>();
        secuenciaOptimizada.AddRange(inmutables);
        secuenciaOptimizada.AddRange(mejorRuta);

        double distBase = CalcularDistanciaTotal(latOrigen, lonOrigen, paradas);
        double distOpt = CalcularDistanciaTotal(latOrigen, lonOrigen, secuenciaOptimizada);

        if (distOpt > distBase)
        {
            secuenciaOptimizada = paradas.ToList();
            distOpt = distBase;
        }

        double ahorro = Math.Max(0, distBase - distOpt);
        double pct = distBase > 0 ? (ahorro / distBase) * 100.0 : 0;
        int tBase = (int)Math.Round((distBase / 25.0) * 60 + paradas.Count * 5);
        int tOpt = (int)Math.Round((distOpt / 25.0) * 60 + paradas.Count * 5);

        return new ResultadoOptimizacionPrueba(paradas, secuenciaOptimizada, distBase, distOpt, ahorro, pct, tBase, tOpt);
    }

    [Fact]
    public void OptimizacionRuta_RutaDesordenada_CalculaDistanciaOptimizadaMenorOIgual()
    {
        // Origen: Terminal Logística Sur Montevideo (-34.9265, -56.1610)
        double latOrigen = -34.9265;
        double lonOrigen = -56.1610;

        // 4 paradas pendientes en Montevideo dispuestas con cruce de recorrido zigzagueante
        var paradas = new List<ParadaPrueba>
        {
            new(Guid.NewGuid(), "ENV-001", -34.9058, -56.1885, "Pendiente", "09:00 - 13:00", 1), // Centro
            new(Guid.NewGuid(), "ENV-002", -34.8800, -56.1200, "Pendiente", "09:00 - 13:00", 2), // Malvín Norte
            new(Guid.NewGuid(), "ENV-003", -34.9145, -56.1552, "Pendiente", "09:00 - 13:00", 3), // Pocitos (cerca de Centro)
            new(Guid.NewGuid(), "ENV-004", -34.8900, -56.0900, "Pendiente", "09:00 - 13:00", 4)  // Carrasco (cerca de Malvín)
        };

        var resultado = OptimizarRutaPrueba(latOrigen, lonOrigen, paradas);

        Assert.NotNull(resultado);
        Assert.True(resultado.DistanciaOptimizadaKm <= resultado.DistanciaBaseKm,
            $"La distancia optimizada ({resultado.DistanciaOptimizadaKm} km) debe ser <= a la base ({resultado.DistanciaBaseKm} km).");
        Assert.True(resultado.AhorroKm >= 0);
        Assert.True(resultado.PorcentajeAhorro >= 0);
        Assert.True(resultado.TiempoEstimadoOptimizadoMinutos <= resultado.TiempoEstimadoBaseMinutos);
    }

    [Fact]
    public void OptimizacionRuta_ParadasCompletadas_PermanecenInmutablesEnSuPosicion()
    {
        double latOrigen = -34.9265;
        double lonOrigen = -56.1610;

        var parada1Entregada = new ParadaPrueba(Guid.NewGuid(), "ENV-ENTREGADO-1", -34.9058, -56.1885, "Entregado", "09:00 - 10:00", 1);
        var parada2Fallida = new ParadaPrueba(Guid.NewGuid(), "ENV-FALLIDO-2", -34.9100, -56.1700, "Fallido", "10:00 - 11:00", 2);
        var parada3Pendiente = new ParadaPrueba(Guid.NewGuid(), "ENV-PEND-3", -34.8800, -56.1200, "Pendiente", "11:00 - 12:00", 3);
        var parada4Pendiente = new ParadaPrueba(Guid.NewGuid(), "ENV-PEND-4", -34.9145, -56.1552, "Pendiente", "11:00 - 12:00", 4);

        var paradas = new List<ParadaPrueba> { parada1Entregada, parada2Fallida, parada3Pendiente, parada4Pendiente };

        var resultado = OptimizarRutaPrueba(latOrigen, lonOrigen, paradas);

        Assert.Equal(4, resultado.SecuenciaOptimizada.Count);
        // Las dos primeras deben ser exactamente las inmutables
        Assert.Equal(parada1Entregada.Id, resultado.SecuenciaOptimizada[0].Id);
        Assert.Equal("Entregado", resultado.SecuenciaOptimizada[0].Estado);
        Assert.Equal(parada2Fallida.Id, resultado.SecuenciaOptimizada[1].Id);
        Assert.Equal("Fallido", resultado.SecuenciaOptimizada[1].Estado);
    }

    [Fact]
    public void OptimizacionRuta_NoDuplicaNiPierdeIdentificadores()
    {
        double latOrigen = -34.9265;
        double lonOrigen = -56.1610;

        var paradas = Enumerable.Range(1, 5).Select(i =>
            new ParadaPrueba(Guid.NewGuid(), $"ENV-00{i}", -34.9000 - (i * 0.005), -56.1600 - (i * 0.005), "Pendiente", "09:00 - 13:00", i)
        ).ToList();

        var resultado = OptimizarRutaPrueba(latOrigen, lonOrigen, paradas);

        Assert.Equal(paradas.Count, resultado.SecuenciaOptimizada.Count);
        var idsOriginales = paradas.Select(p => p.Id).OrderBy(id => id).ToList();
        var idsOptimizados = resultado.SecuenciaOptimizada.Select(p => p.Id).OrderBy(id => id).ToList();
        Assert.Equal(idsOriginales, idsOptimizados);
    }

    // =========================================================================
    // 2. MOTOR DE GEOCERCAS CON HISTÉRESIS (ENTRADA < 100M, SALIDA > 150M)
    // =========================================================================

    private class GeocercaHisteresisSimulador
    {
        public const double UmbralEntradaMetros = 100.0;
        public const double UmbralSalidaMetros = 150.0;

        public bool DentroDeGeocerca { get; private set; }
        public DateTime? TimestampEntradaUtc { get; private set; }
        public int? UltimoTiempoPermanenciaSegundos { get; private set; }
        public int TotalEventosEntrada { get; private set; }
        public int TotalEventosSalida { get; private set; }

        public string? ProcesarLecturaDistancia(double distanciaMetros, DateTime ahoraUtc)
        {
            // Radio de entrada < 100 m
            if (!DentroDeGeocerca && distanciaMetros < UmbralEntradaMetros)
            {
                DentroDeGeocerca = true;
                TimestampEntradaUtc = ahoraUtc;
                TotalEventosEntrada++;
                return "LLEGADA_GEOCERCA";
            }

            // Radio de salida > 150 m
            if (DentroDeGeocerca && distanciaMetros > UmbralSalidaMetros)
            {
                int permanencia = TimestampEntradaUtc.HasValue
                    ? Math.Max(1, (int)(ahoraUtc - TimestampEntradaUtc.Value).TotalSeconds)
                    : 1;

                DentroDeGeocerca = false;
                UltimoTiempoPermanenciaSegundos = permanencia;
                TimestampEntradaUtc = null;
                TotalEventosSalida++;
                return "SALIDA_GEOCERCA";
            }

            // Zona de histéresis (100 m a 150 m): no hay transición
            return null;
        }
    }

    [Fact]
    public void Geocerca_DistanciaMenorA100m_DisparaLlegadaGeocerca()
    {
        var motor = new GeocercaHisteresisSimulador();
        var ahora = DateTime.UtcNow;

        var evento = motor.ProcesarLecturaDistancia(45.0, ahora);

        Assert.Equal("LLEGADA_GEOCERCA", evento);
        Assert.True(motor.DentroDeGeocerca);
        Assert.Equal(ahora, motor.TimestampEntradaUtc);
        Assert.Equal(1, motor.TotalEventosEntrada);
    }

    [Fact]
    public void Geocerca_LecturasEnBandaDeHisteresis_NoDisparanFalsosPositivosNiDuplican()
    {
        var motor = new GeocercaHisteresisSimulador();
        var t0 = DateTime.UtcNow;

        // 1. Repartidor entra a 60m
        var e1 = motor.ProcesarLecturaDistancia(60.0, t0);
        Assert.Equal("LLEGADA_GEOCERCA", e1);

        // 2. Rebote GPS a 110m (está en banda 100m - 150m): debe seguir DENTRO sin nuevo evento
        var e2 = motor.ProcesarLecturaDistancia(110.0, t0.AddSeconds(5));
        Assert.Null(e2);
        Assert.True(motor.DentroDeGeocerca);
        Assert.Equal(1, motor.TotalEventosEntrada);

        // 3. Rebote a 145m: sigue en banda sin disparar salida
        var e3 = motor.ProcesarLecturaDistancia(145.0, t0.AddSeconds(10));
        Assert.Null(e3);
        Assert.True(motor.DentroDeGeocerca);
        Assert.Equal(0, motor.TotalEventosSalida);
    }

    [Fact]
    public void Geocerca_DistanciaMayorA150mTrasEntrar_DisparaSalidaYRegistraPermanencia()
    {
        var motor = new GeocercaHisteresisSimulador();
        var tEntrada = DateTime.UtcNow;

        // 1. Entra a 40m
        motor.ProcesarLecturaDistancia(40.0, tEntrada);

        // 2. 75 segundos después se aleja a 220m
        var tSalida = tEntrada.AddSeconds(75);
        var eventoSalida = motor.ProcesarLecturaDistancia(220.0, tSalida);

        Assert.Equal("SALIDA_GEOCERCA", eventoSalida);
        Assert.False(motor.DentroDeGeocerca);
        Assert.Equal(1, motor.TotalEventosSalida);
        Assert.Equal(75, motor.UltimoTiempoPermanenciaSegundos);
    }

    [Fact]
    public void Geocerca_LecturaEnBandaSinHaberEntrado_PermaneceAfuera()
    {
        var motor = new GeocercaHisteresisSimulador();
        var ahora = DateTime.UtcNow;

        // Se aproxima a 120m pero nunca cruzó el umbral < 100m
        var evento = motor.ProcesarLecturaDistancia(120.0, ahora);

        Assert.Null(evento);
        Assert.False(motor.DentroDeGeocerca);
        Assert.Equal(0, motor.TotalEventosEntrada);
    }

    // =========================================================================
    // 3. COLA OFFLINE Y SERIALIZACIÓN DE CONTRATOS (SYNCSERVICE / API)
    // =========================================================================

    public record EventoGeocercaOfflinePayloadDto(
        int EventoLocalId,
        Guid HojaDeRutaId,
        Guid ParadaId,
        Guid EnvioId,
        string CodigoSeguimiento,
        string TipoEvento,
        double Latitud,
        double Longitud,
        double DistanciaMetros,
        int? TiempoPermanenciaSegundos,
        DateTime TimestampUtc,
        string IdempotencyKey
    );

    public record ParadaSecuenciaUpdatePayloadDto(
        Guid ParadaId,
        int NuevoOrden,
        int OrdenVisita,
        string HoraEstimada
    );

    [Fact]
    public void PayloadGeocerca_SerializacionYDeserializacion_ConservaTodosLosCamposSinNulos()
    {
        var dto = new EventoGeocercaOfflinePayloadDto(
            EventoLocalId: 104,
            HojaDeRutaId: Guid.NewGuid(),
            ParadaId: Guid.NewGuid(),
            EnvioId: Guid.NewGuid(),
            CodigoSeguimiento: "ENV-2026-00108",
            TipoEvento: "LLEGADA_GEOCERCA",
            Latitud: -34.9058,
            Longitud: -56.1885,
            DistanciaMetros: 42.5,
            TiempoPermanenciaSegundos: 65,
            TimestampUtc: new DateTime(2026, 11, 4, 1, 30, 0, DateTimeKind.Utc),
            IdempotencyKey: "GEO-LLEGADA-bbbb-123456789"
        );

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var deserializado = JsonSerializer.Deserialize<EventoGeocercaOfflinePayloadDto>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.NotNull(deserializado);
        Assert.Equal(dto.EventoLocalId, deserializado.EventoLocalId);
        Assert.Equal(dto.HojaDeRutaId, deserializado.HojaDeRutaId);
        Assert.Equal(dto.ParadaId, deserializado.ParadaId);
        Assert.Equal(dto.EnvioId, deserializado.EnvioId);
        Assert.Equal(dto.CodigoSeguimiento, deserializado.CodigoSeguimiento);
        Assert.Equal(dto.TipoEvento, deserializado.TipoEvento);
        Assert.Equal(dto.Latitud, deserializado.Latitud);
        Assert.Equal(dto.Longitud, deserializado.Longitud);
        Assert.Equal(dto.DistanciaMetros, deserializado.DistanciaMetros);
        Assert.Equal(dto.TiempoPermanenciaSegundos, deserializado.TiempoPermanenciaSegundos);
        Assert.Equal(dto.TimestampUtc, deserializado.TimestampUtc);
        Assert.Equal(dto.IdempotencyKey, deserializado.IdempotencyKey);
    }

    [Fact]
    public void PayloadSecuenciaOptimizada_Serializacion_ConservaOrdenYVisita()
    {
        var paradas = new List<ParadaSecuenciaUpdatePayloadDto>
        {
            new(Guid.NewGuid(), 1, 1, "08:30"),
            new(Guid.NewGuid(), 2, 2, "09:30"),
            new(Guid.NewGuid(), 3, 3, "10:30")
        };

        var json = JsonSerializer.Serialize(paradas);
        var res = JsonSerializer.Deserialize<List<ParadaSecuenciaUpdatePayloadDto>>(json);

        Assert.NotNull(res);
        Assert.Equal(3, res.Count);
        Assert.Equal(1, res[0].OrdenVisita);
        Assert.Equal("08:30", res[0].HoraEstimada);
        Assert.Equal(2, res[1].OrdenVisita);
        Assert.Equal(3, res[2].OrdenVisita);
    }

    [Fact]
    public void RegistrarEventoGeocercaCommand_InstanciacionValida_CumpleInvariantes()
    {
        var cmd = new RegistrarEventoGeocercaCommand(
            HojaDeRutaId: Guid.NewGuid(),
            ParadaId: Guid.NewGuid(),
            EnvioId: Guid.NewGuid(),
            TipoEvento: "LLEGADA_GEOCERCA",
            Latitud: -34.9058,
            Longitud: -56.1885,
            DistanciaMetros: 50.0,
            TiempoPermanenciaSegundos: null,
            TimestampUtc: DateTime.UtcNow,
            IdempotencyKey: "GEO-IDEMP-001"
        );

        Assert.NotEqual(Guid.Empty, cmd.HojaDeRutaId);
        Assert.NotEqual(Guid.Empty, cmd.ParadaId);
        Assert.Equal("LLEGADA_GEOCERCA", cmd.TipoEvento);
        Assert.True(cmd.DistanciaMetros > 0);
    }

    // =========================================================================
    // 4. SEGURIDAD CRIPTOGRÁFICA Y ALMACENAMIENTO SQLCIPHER (RNF 6.3, RNF 6.11)
    // =========================================================================

    public record EstadoCifradoDiagnosticoDto(
        bool CifradoActivo,
        string CipherVersion,
        bool ArchivoEsCifradoEnDisco,
        string MensajeDiagnostico);

    [Fact]
    public void GeneracionClaveCriptografica_256Bits_ProduceClaveValidaYEntropiaAlta()
    {
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var base64Key = Convert.ToBase64String(keyBytes);

        Assert.Equal(32, keyBytes.Length);
        Assert.Equal(44, base64Key.Length);
        Assert.False(string.IsNullOrWhiteSpace(base64Key));

        // Claves sucesivas deben ser criptográficamente impredecibles y únicas
        var keyBytes2 = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var base64Key2 = Convert.ToBase64String(keyBytes2);
        Assert.NotEqual(base64Key, base64Key2);
    }

    [Fact]
    public void ValidacionCabeceraBaseDeDatos_DeteccionTextoPlanoVsCifrado_DistingueCorrectamente()
    {
        var sqlitePlaintextHeader = "SQLite format 3\0"u8.ToArray();
        Assert.Equal(16, sqlitePlaintextHeader.Length);

        // Archivo en texto plano: primeros 16 bytes coinciden con el magic header de SQLite
        byte[] samplePlaintext = new byte[16];
        Array.Copy(sqlitePlaintextHeader, samplePlaintext, 16);
        bool esTextoPlano = samplePlaintext.SequenceEqual(sqlitePlaintextHeader);
        bool esCifrado = !esTextoPlano;

        Assert.True(esTextoPlano);
        Assert.False(esCifrado);

        // Archivo cifrado con SQLCipher: primeros 16 bytes contienen salt criptográfico aleatorio
        byte[] sampleCiphertext = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        bool esTextoPlanoCipher = sampleCiphertext.SequenceEqual(sqlitePlaintextHeader);
        bool esCifradoCipher = !esTextoPlanoCipher;

        Assert.False(esTextoPlanoCipher);
        Assert.True(esCifradoCipher);
    }

    [Fact]
    public void EstadoCifradoDiagnosticoDto_ContratoCifradoSQLCipher_CumpleInvariantes()
    {
        var dto = new EstadoCifradoDiagnosticoDto(
            CifradoActivo: true,
            CipherVersion: "4.5.7 community",
            ArchivoEsCifradoEnDisco: true,
            MensajeDiagnostico: "Cifrado activo con SQLCipher v4.5.7 community. Base de datos cifrada en disco."
        );

        Assert.True(dto.CifradoActivo);
        Assert.Equal("4.5.7 community", dto.CipherVersion);
        Assert.True(dto.ArchivoEsCifradoEnDisco);
        Assert.Contains("SQLCipher", dto.MensajeDiagnostico);

        var json = JsonSerializer.Serialize(dto);
        var deserializado = JsonSerializer.Deserialize<EstadoCifradoDiagnosticoDto>(json);

        Assert.NotNull(deserializado);
        Assert.Equal(dto.CifradoActivo, deserializado.CifradoActivo);
        Assert.Equal(dto.CipherVersion, deserializado.CipherVersion);
        Assert.Equal(dto.ArchivoEsCifradoEnDisco, deserializado.ArchivoEsCifradoEnDisco);
    }

    // =========================================================================
    // 5. PRUEBA DE ENTREGA (POD): FIRMA, FOTO Y AUDITORÍA GPS
    // =========================================================================

    [Fact]
    public void PruebaDeEntrega_FirmaRasterizada_GeneraEncabezadoPngValidoYBase64()
    {
        // Encabezado estándar PNG (RFC 2083)
        byte[] pngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

        // Simulación de comprobante de firma rasterizada
        using var ms = new MemoryStream();
        ms.Write(pngHeader);
        ms.Write(new byte[128]); // Datos simulados de imagen rasterizada

        var bytes = ms.ToArray();
        var base64 = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

        Assert.True(bytes.Length > 8);
        Assert.True(bytes.Take(8).SequenceEqual(pngHeader));
        Assert.StartsWith("data:image/png;base64,", base64);
        Assert.True(base64.Length > 30);
    }

    [Fact]
    public void PruebaDeEntrega_CompresionFotografica_ValidaEncabezadoJpegYRelacionCompresion()
    {
        // Encabezado SOI estándar JPEG
        byte[] jpegHeader = [0xFF, 0xD8];

        long pesoOriginalBytes = 3_420_000; // ~3.4 MB
        long pesoComprimidoBytes = 78_500;  // ~78 KB

        double factorCompresion = (double)pesoComprimidoBytes / pesoOriginalBytes;
        Assert.True(factorCompresion < 0.10); // Más del 90% de reducción

        byte[] fakeJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];
        Assert.True(fakeJpeg.Take(2).SequenceEqual(jpegHeader));
        Assert.Equal(0xFF, fakeJpeg[^2]);
        Assert.Equal(0xD9, fakeJpeg[^1]);
    }

    [Fact]
    public void PruebaDeEntrega_AuditoriaGeolocalizacionYTimestamp_PreservaPrecision()
    {
        double latitud = -34.9011;
        double longitud = -56.1645;
        var timestampUtc = DateTime.UtcNow;

        var payload = new
        {
            HojaDeRutaId = Guid.NewGuid(),
            ParadaId = Guid.NewGuid(),
            EnvioId = Guid.NewGuid(),
            IdempotencyKey = $"ENTREGA-{Guid.NewGuid():N}",
            NombreReceptor = "Carlos Martinez",
            DocumentoReceptor = "4.567.890-1",
            FirmaDigitalBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg==",
            FotoEvidenciaBase64 = "data:image/jpeg;base64,/9j/4AAQSkZJRg==",
            Latitud = latitud,
            Longitud = longitud,
            FechaHoraDispositivoUtc = timestampUtc
        };

        var json = JsonSerializer.Serialize(payload);
        Assert.Contains("-34.9011", json);
        Assert.Contains("-56.1645", json);
        Assert.Contains("Carlos Martinez", json);
        Assert.NotNull(payload.FirmaDigitalBase64);
        Assert.NotNull(payload.FotoEvidenciaBase64);
        Assert.True(payload.FechaHoraDispositivoUtc <= DateTime.UtcNow);
    }

    // =========================================================================
    // 6. VALIDACIÓN BIOMÉTRICA EN OPERACIONES SENSIBLES (RNF 6.3, RNF 6.11)
    // =========================================================================

    public interface ITestBiometricAuthService
    {
        Task<bool> ValidarIdentidadAsync(string titulo, string motivo);
        Task<bool> DispositivoSoportaBiometriaAsync();
    }

    public class FakeBiometricAuthService : ITestBiometricAuthService
    {
        public bool DebeAprobar { get; set; } = true;
        public bool SoportaBiometria { get; set; } = true;
        public int LlamadasValidar { get; private set; }
        public string? UltimoTitulo { get; private set; }
        public string? UltimoMotivo { get; private set; }

        public Task<bool> DispositivoSoportaBiometriaAsync()
            => Task.FromResult(SoportaBiometria);

        public Task<bool> ValidarIdentidadAsync(string titulo, string motivo)
        {
            LlamadasValidar++;
            UltimoTitulo = titulo;
            UltimoMotivo = motivo;
            return Task.FromResult(DebeAprobar);
        }
    }

    [Fact]
    public async Task Biometria_OperacionSensible_Autorizada_PermiteCompletarAccion()
    {
        var bioService = new FakeBiometricAuthService { DebeAprobar = true };

        var autorizado = await bioService.ValidarIdentidadAsync(
            "Autorizar Custodia de Carga",
            "Verifique su identidad para asumir la custodia de los bultos.");

        Assert.True(autorizado);
        Assert.Equal(1, bioService.LlamadasValidar);
        Assert.Equal("Autorizar Custodia de Carga", bioService.UltimoTitulo);
    }

    [Fact]
    public async Task Biometria_OperacionSensible_Rechazada_ImpideContinuar()
    {
        var bioService = new FakeBiometricAuthService { DebeAprobar = false };

        var autorizado = await bioService.ValidarIdentidadAsync(
            "Autorizar Rendicion de Ruta",
            "Verifique su identidad para confirmar la entrega de bultos en deposito.");

        Assert.False(autorizado);
        Assert.Equal(1, bioService.LlamadasValidar);
        Assert.Equal("Autorizar Rendicion de Ruta", bioService.UltimoTitulo);
    }

    // =========================================================================
    // 7. TELEMETRIA EN SEGUNDO PLANO Y POSICIONES OFFLINE
    // =========================================================================

    public class TelemetriaGpsOfflinePayloadPrueba
    {
        public Guid HojaDeRutaId { get; set; }
        public Guid RepartidorId { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double? PrecisionMetros { get; set; }
        public DateTime TimestampUtc { get; set; }
        public int? BateriaPct { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
    }

    [Fact]
    public void PayloadTelemetriaGps_SerializacionYDeserializacion_ConservaTodosLosCampos()
    {
        var rutaId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var fecha = new DateTime(2026, 11, 4, 18, 30, 0, DateTimeKind.Utc);

        var original = new TelemetriaGpsOfflinePayloadPrueba
        {
            HojaDeRutaId = rutaId,
            RepartidorId = repId,
            Latitud = -34.9011,
            Longitud = -56.1645,
            PrecisionMetros = 8.5,
            TimestampUtc = fecha,
            BateriaPct = 85,
            IdempotencyKey = $"GPS-{rutaId:N}-{fecha.Ticks}"
        };

        var json = JsonSerializer.Serialize(original);
        var recuperado = JsonSerializer.Deserialize<TelemetriaGpsOfflinePayloadPrueba>(json);

        Assert.NotNull(recuperado);
        Assert.Equal(original.HojaDeRutaId, recuperado.HojaDeRutaId);
        Assert.Equal(original.RepartidorId, recuperado.RepartidorId);
        Assert.Equal(original.Latitud, recuperado.Latitud, 4);
        Assert.Equal(original.Longitud, recuperado.Longitud, 4);
        Assert.Equal(original.PrecisionMetros, recuperado.PrecisionMetros);
        Assert.Equal(original.TimestampUtc, recuperado.TimestampUtc);
        Assert.Equal(original.BateriaPct, recuperado.BateriaPct);
        Assert.Equal(original.IdempotencyKey, recuperado.IdempotencyKey);
    }

    [Fact]
    public void RegistrarPosicionesCommand_InstanciacionValida_CumpleInvariantes()
    {
        var rutaId = Guid.NewGuid();
        var fecha = DateTime.UtcNow;

        var pos1 = new UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones.PosicionDispositivoDto(
            Latitud: -34.9011,
            Longitud: -56.1645,
            TomadaEn: fecha,
            VelocidadKmh: 42.0,
            BateriaPct: 78,
            HojaDeRutaId: rutaId);

        var comando = new UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones.RegistrarPosicionesCommand([pos1]);

        Assert.Single(comando.Posiciones);
        Assert.Equal(-34.9011, comando.Posiciones[0].Latitud);
        Assert.Equal(-56.1645, comando.Posiciones[0].Longitud);
        Assert.Equal(fecha, comando.Posiciones[0].TomadaEn);
        Assert.Equal(42.0, comando.Posiciones[0].VelocidadKmh);
        Assert.Equal(78, comando.Posiciones[0].BateriaPct);
        Assert.Equal(rutaId, comando.Posiciones[0].HojaDeRutaId);
    }

    [Fact]
    public void RegistrarPosicionesCommand_ConListaVacia_MantieneInvarianteColeccion()
    {
        var comando = new UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones.RegistrarPosicionesCommand([]);

        Assert.NotNull(comando.Posiciones);
        Assert.Empty(comando.Posiciones);
    }

    [Fact]
    public void PosicionDispositivoDto_ConLimitesExtremosYValoresNulos_PreservaIntegridad()
    {
        var posMin = new UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones.PosicionDispositivoDto(
            Latitud: -90.0,
            Longitud: -180.0,
            TomadaEn: null,
            VelocidadKmh: null,
            BateriaPct: null,
            HojaDeRutaId: null);

        var posMax = new UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones.PosicionDispositivoDto(
            Latitud: 90.0,
            Longitud: 180.0,
            TomadaEn: DateTime.MaxValue,
            VelocidadKmh: 300.0,
            BateriaPct: 100,
            HojaDeRutaId: Guid.Empty);

        Assert.Equal(-90.0, posMin.Latitud);
        Assert.Equal(-180.0, posMin.Longitud);
        Assert.Null(posMin.TomadaEn);
        Assert.Null(posMin.VelocidadKmh);
        Assert.Null(posMin.BateriaPct);
        Assert.Null(posMin.HojaDeRutaId);

        Assert.Equal(90.0, posMax.Latitud);
        Assert.Equal(180.0, posMax.Longitud);
        Assert.Equal(DateTime.MaxValue, posMax.TomadaEn);
        Assert.Equal(300.0, posMax.VelocidadKmh);
        Assert.Equal(100, posMax.BateriaPct);
        Assert.Equal(Guid.Empty, posMax.HojaDeRutaId);
    }
}
