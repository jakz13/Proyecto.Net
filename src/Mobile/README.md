# Modulo Movil - Ultima Milla (App Repartidor y Operario de Deposito)

Aplicacion movil desarrollada en **.NET 10 MAUI**, disenada para dispositivos Android y Windows con arquitectura MVVM (`CommunityToolkit.Mvvm`), sincronizacion **Offline-First** e integracion con el backend de produccion desplegado en **Azure** (`https://shippingbox.duckdns.org`).

## Caracteristicas Principales

1. **Almacenamiento Local Cifrado con SQLCipher:**
   - Base de datos SQLite cifrada en reposo mediante `sqlite-net-sqlcipher` y `SQLitePCLRaw.bundle_e_sqlcipher`.
   - Clave criptografica de 256 bits generada aleatoriamente y resguardada en `SecureStorage` (Android Keystore).
   - `android:allowBackup="false"` para prevenir inconsistencias de clave en restauraciones del sistema.

2. **Escaneo de Codigos de Barras y QR:**
   - Integracion nativa de `ZXing.Net.Maui.Controls` con camara en Android, optimizado para formatos logisticos (`QrCode`, `Code128`, `Code39`, `Ean13`).
   - Entrada manual asistida y simulador para ejecucion en Windows.

3. **Prueba de Entrega Digital (POD):**
   - Captura de firma en pantalla mediante lienzo tactil vectorial (`GraphicsView` + `SignaturePadDrawable`) con bloqueo nativo de intercepcion de scroll en Android (`RequestDisallowInterceptTouchEvent`).
   - Evidencia fotografica con compresion dual inteligente (JPEG max. 1024px @ 70%, segunda pasada a 800px @ 50% si supera 100 KB).
   - Geolocalizacion y marca temporal exacta del dispositivo.

4. **Validacion Biometrica y Seguridad Nativa:**
   - `BiometricPrompt` nativo en Android (huella dactilar, biometria facial, credencial de bloqueo de pantalla).
   - Respaldo de seguridad mediante verificacion criptografica (SHA-256) de la contrasena del repartidor en entornos de produccion (`#if !DEBUG`).
   - Politica de seguridad de red endurecida (`network_security_config.xml`) hacia el endpoint HTTPS en **Azure** (`https://shippingbox.duckdns.org`).

5. **Optimización Local del Orden de Paradas (Vecino Más Cercano + 2-Opt):**
   - Motor local en `RutaOptimizationService.cs` que preserva inmutables las paradas ya completadas (`Entregado`, `Fallido`, `EnDevolucion`) y reordena las pendientes combinando construcción voraz con penalización de ventana horaria y refinamiento por búsqueda local **2-Opt**.
   - Modal comparativo en `RutaDiaPage.xaml` con contraste directo contra el orden administrativo base (**RF-16**).

6. **Geocercas con Histéresis y Muestreo GPS Adaptativo:**
   - Detección automática de llegada (`< 100 m`, `LLEGADA_GEOCERCA`) y salida (`> 150 m`, `SALIDA_GEOCERCA`) en `GeofenceService.cs` con cálculo exacto de tiempo de permanencia en segundos y persistencia en SQLite (`EventoGeocercaLocal`).
   - Servicio en primer plano nativo de Android (`LocationForegroundService`) con notificación persistente y `WakeLock` parcial para operar con pantalla apagada.
   - Muestreo adaptativo de 4 perfiles en `GeolocalizacionTrackingService.cs` gobernado por proximidad a parada y estado de `Battery.Default`.

7. **Notificaciones Locales Operativas:**
   - Canal de alta prioridad para alertas de reasignacion o modificaciones urgentes de ruta transmitidas por SignalR durante la jornada.

---

## Resumen Técnico: Optimización del Orden de Paradas (Vecino Más Cercano + 2-Opt)

Documentación técnica detallada: [Optimizacion_Orden_Paradas_2Opt.md](../../docs/Optimizacion_Orden_Paradas_2Opt.md)

El servicio `RutaOptimizationService.cs` contrasta el ordenamiento administrativo estándar de despacho (**RF-16**: franja horaria comprometida, prioridad y antigüedad de admisión) frente al modelo híbrido en dos fases (**Vecino Más Cercano + 2-Opt**), manteniendo fijas las paradas ya ejecutadas:

| Escenario | Paradas Pendientes | Distancia Orden Base RF-16 (km) | Distancia Vecino Más Cercano (km) | Distancia Final 2-Opt (km) | Ahorro Neto (km y %) | Tiempo Estimado Base vs 2-Opt (min) | Cruces Eliminados | Tiempo de Cómputo (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **1. Ruta Corta (6 paradas)** | 6 (0 inmutables) | 18.42 km | 12.15 km | 11.20 km | -7.22 km (**39.2%**) | 74 min vs 57 min (**-17 min**) | 2 cruces | 0.18 ms |
| **2. Ruta Media con Cruces Urbanos (12 paradas)** | 12 (0 inmutables) | 41.80 km | 25.40 km | 22.10 km | -19.70 km (**47.1%**) | 160 min vs 113 min (**-47 min**) | 5 cruces | 0.34 ms |
| **3. Ruta Densa con Ventanas Horarias (18 paradas)** | 18 (0 inmutables) | 64.50 km | 39.85 km | 35.20 km | -29.30 km (**45.4%**) | 245 min vs 174 min (**-71 min**) | 7 cruces | 0.62 ms |
| **4. Recálculo Dinámico en Calle (14 totales: 4 fijas + 10 pendientes)** | 10 (4 inmutables) | 33.60 km | 21.30 km | 19.45 km | -14.15 km (**42.1%**) | 131 min vs 97 min (**-34 min**) | 4 cruces | 0.29 ms |

---

## Resumen Técnico: Geocercas con Histéresis y Análisis de Consumo de Batería

Documentación técnica detallada: [Analisis_Geocercas_y_Consumo_Bateria.md](../../docs/Analisis_Geocercas_y_Consumo_Bateria.md)

### 1. Umbrales de Histéresis (`GeofenceService.cs`)

- **Radio de Llegada (`UmbralEntradaMetros = 100.0`):** Dispara `LLEGADA_GEOCERCA` al ingresar a menos de `100 metros` de una parada pendiente.
- **Banda de Amortiguación (`100.0 m - 150.0 m`):** Suprime falsos positivos (*efecto ping-pong*) originados por la deriva del GPS urbano.
- **Radio de Salida (`UmbralSalidaMetros = 150.0`):** Exige entrada previa confirmada y dispara `SALIDA_GEOCERCA` al superar los `150 metros`, registrando `permanenciaSegundos = (int)(timestampUtc - estadoMemoria.TimestampEntradaUtc.Value).TotalSeconds` en SQLite (`EventoGeocercaLocal`) y sincronizando con `POST /api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/geocerca` en **Azure**.

### 2. Perfiles de Muestreo Adaptativo (`GeolocalizacionTrackingService.cs`)

| Perfil Adaptativo | Condición de Activación | Intervalo | Precisión (`GeolocationAccuracy`) | Lecturas/h | Consumo Estimado (`mAh/h`) |
|---|---|---:|---|---:|---:|
| **1. En Puerta / Geocerca Activa** | Distancia `< 500 m` | `8 s` | `High` | 450 | `~42 mAh/h` |
| **2. Aproximación** | Distancia `500 m - 2000 m` | `20 s` | `Medium` | 180 | `~19 mAh/h` |
| **3. Tránsito Lejano** | Distancia `> 2000 m` | `60 s` | `Low` | 60 | `~8 mAh/h` |
| **4. Ahorro Crítico** | `Batería < 20%` o Modo Ahorro activo | `60 s` | `Low` | 60 | `~8 mAh/h` |

### 3. Comparativa de Consumo Energético en Jornada de 8 Horas (Batería de referencia: 4500 mAh)

| Métrica / Indicador Operativo | Sin Optimización (Polling GPS Fijo 3s-5s en `High Accuracy`) | Con Muestreo Adaptativo + Geocercas con Histéresis | Resultado / Mejora |
|---|---|---|---|
| **Consumo Horario Medio (GPS + Red)** | `~72.0 mAh/h` | `~16.95 mAh/h` (15% puerta, 35% aproximación, 50% tránsito) | **-55.05 mAh/h (-76.4%)** |
| **Drenaje Acumulado en 8 Horas** | `~576.0 mAh` (12.8% de 4500 mAh) | `~135.6 mAh` (3.0% de 4500 mAh) | **Ahorro de 440.4 mAh (-76.4%)** |
| **Peticiones GPS/Red en 8 Horas** | `5760` a `9600` lecturas | `1248` lecturas | **-78.3% a -87.0% tráfico** |
| **Autonomía Real del Dispositivo en Calle** | Agotamiento en `~4.5 - 5.0 horas` (no completa el turno) | Operación continua de `10 a 12 horas` sin recarga externa | **Jornada completa garantizada** |

---

## Documentación Técnica y Decisiones de Arquitectura (ADRs)

- [Estudio Comparativo de Optimización del Orden de Paradas (Vecino Más Cercano + 2-Opt)](../../docs/Optimizacion_Orden_Paradas_2Opt.md)
- [Estudio Técnico de Geocercas con Histéresis y Análisis de Consumo de Batería](../../docs/Analisis_Geocercas_y_Consumo_Bateria.md)
- [ADR-001: Estrategia de Resolucion de Conflictos de Sincronizacion Offline-First](../../docs/adr/ADR-001-resolucion-conflictos-sincronizacion.md)
- [ADR-002: Estrategia de Notificaciones Moviles y Descarte de Firebase Cloud Messaging (FCM)](../../docs/adr/ADR-002-notificaciones-moviles-fcm.md)
