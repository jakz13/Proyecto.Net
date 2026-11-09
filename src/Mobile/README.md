# Modulo Movil - Ultima Milla (App Repartidor)

Aplicacion movil desarrollada en **.NET 10 MAUI**, disenada para dispositivos Android con arquitectura MVVM (CommunityToolkit.Mvvm) y enfoque **Offline-First**.

## Caracteristicas Principales

1. **Almacenamiento Local Cifrado con SQLCipher:**
   - Base de datos SQLite cifrada mediante `sqlite-net-sqlcipher` y `SQLitePCLRaw.bundle_e_sqlcipher`.
   - Clave criptografica de 256 bits generada aleatoriamente y resguardada en `SecureStorage` (Android Keystore).
   - `android:allowBackup="false"` para prevenir inconsistencias de clave en restauraciones del sistema.

2. **Escaneo de Codigos de Barras y QR:**
   - Integracion nativa de `ZXing.Net.Maui.Controls` con camara en Android, optimizado para formatos logisticos (`QrCode`, `Code128`, `Code39`, `Ean13`).
   - Simulador de entrada manual mantenido para ejecucion en Windows.

3. **Prueba de Entrega Digital (POD):**
   - Captura de firma en pantalla mediante lienzo tactil vectorial (`GraphicsView` + `SignaturePadDrawable`).
   - Evidencia fotografica con compresion dual inteligente (JPEG max. 1024px @ 70%, segunda pasada a 800px @ 50% si supera 100 KB).
   - Geolocalizacion y marca temporal exacta del dispositivo.

4. **Validacion Biometrica y Seguridad Nativa:**
   - `BiometricPrompt` nativo en Android (huella dactilar, biometria facial, credencial de bloqueo de pantalla).
   - Respaldo de seguridad mediante verificacion criptografica (SHA-256) de la contrasena del repartidor en entornos de produccion (`#if !DEBUG`).
   - Politica de seguridad de red endurecida (`network_security_config.xml`) con restriccion de texto plano y certificados de usuario limitados a depuracion.

5. **Telemetria GPS y Modo Segundo Plano:**
   - Servicio en primer plano nativo de Android (`LocationForegroundService`) con notificacion persistente del sistema y `WakeLock` parcial para garantizar operacion con pantalla apagada.
   - Streaming en tiempo real via SignalR hacia el Tablero en Vivo del Backoffice, con persistencia directa en base de datos.
   - Encolado local de telemetria con tope de 1000 posiciones y sincronizacion agrupada en lotes de hasta 500 registros.

6. **Notificaciones Locales Operativas:**
   - Canal de alta prioridad para alertas de reasignacion o modificaciones urgentes de ruta transmitidas por SignalR durante la jornada.

## Decisiones de Arquitectura (ADRs)

- [ADR-001: Estrategia de Resolucion de Conflictos de Sincronizacion Offline-First](../../docs/adr/ADR-001-resolucion-conflictos-sincronizacion.md)
- [ADR-002: Estrategia de Notificaciones Moviles y Descarte de Firebase Cloud Messaging (FCM)](../../docs/adr/ADR-002-notificaciones-moviles-fcm.md)

