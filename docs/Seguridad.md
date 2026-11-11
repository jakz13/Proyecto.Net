# Seguridad: auditoría, correcciones y riesgos aceptados

Auditoría hecha sobre todo el repositorio (secretos, endpoints anónimos, entradas de usuario, salidas HTML, archivos subidos, salidas HTTP, tokens). Cada corrección tiene una prueba que fallaba antes (`SeguridadEndurecimientoTests`, `AutorizacionPorPerfilApiTests`).

## Corregido
| Hallazgo | Riesgo | Corrección |
|---|---|---|
| Autorización: un comercio podía listar usuarios, comercios y reglas del operador | Fuga entre perfiles | Política por perfil en cada endpoint (`PoliticasApi`) y matriz de pruebas |
| Detalle de envío visible entre comercios del mismo operador | Fuga entre comercios | El handler filtra por comercio; 404 al otro comercio |
| Reinicio de demo anónimo en el servidor | Cualquiera restablece datos | Devuelve 404 con `Auth:PermitirModoDemo=false` |
| **SSRF** en avisos a comercios: la URL del webhook la elige el comercio | El servidor llega a su red interna (base, cola, metadatos de la nube) | `DestinoWebhookSeguro`: se resuelve el DNS y se rechaza toda IP interna al abrir la conexión (cubre DNS rebinding); sin redirecciones |
| **XSS almacenado** en Recepción de depósito: la identificación del bulto se escribía sin escapar dentro de JavaScript | Ejecución de script en el backoffice | Serialización JSON del valor y escape al pintar |
| Subida de logo: cualquier extensión (`.html`, `.svg`) servida desde el sitio | XSS almacenado | Solo PNG/JPG/WEBP, 2 MB, cabecera del archivo verificada; el endpoint de logos solo sirve nombres simples con esas extensiones |
| Token JWT sin `exp` aceptado | Token eterno | Se rechaza |
| Clave JWT de desarrollo en el código como respaldo | Firmar tokens con una clave pública | En `Production` sin `Jwt:SecretKey` la aplicación no arranca |
| Contraseña por defecto `Repartidor.2026!` al dar de alta repartidores | Credencial conocida | El alta exige contraseña (mínimo 8); el restablecimiento genera una aleatoria |
| Listado anónimo de todos los operadores | Enumeración de inquilinos | Solo en modo demo |
| Cookie del backoffice sin `Secure` | Robo de sesión por HTTP | `Secure` fuera de desarrollo |
| Cabeceras de seguridad solo en la API | Clickjacking, sniffing | Caddy las agrega a los tres sitios (HSTS, `nosniff`, `X-Frame-Options`, `Referrer-Policy`) |
| Paquete con vulnerabilidad alta | Cadena de suministro | Actualizado; el pipeline audita en cada ejecución |

## Ya estaba bien (y se puede defender)
- Contraseñas con PBKDF2-SHA256, sal aleatoria y comparación en tiempo constante; bloqueo de cuenta por intentos y mensaje genérico ("Credenciales inválidas") que no revela si el usuario existe.
- Firma del JWT con comparación en tiempo constante (`FixedTimeEquals`); firma de avisos HMAC-SHA256.
- Sin SQL concatenado: todo el acceso a datos es EF Core parametrizado (no hay `FromSqlRaw`).
- Aislamiento por operador con filtro global de EF y por comercio en el handler; escritura sin inquilino lanza excepción.
- `/metrics` no se publica: Caddy no lo enruta, solo Prometheus lo consulta por la red interna.
- Seguimiento público: respuesta idéntica para códigos válidos e inexistentes y límite de tasa por IP.

## Riesgos aceptados (decirlos antes de que los encuentren)
1. **Cuentas de demostración con contraseña conocida** (`admin@gmail.com`, etc.): el ambiente es de laboratorio y el profesor las necesita. Al desplegar un ambiente real se define `SEED_PASSWORD_DEMO` y todas nacen con esa. Las contraseñas de la VM real (Grafana, base, JWT) están en `.env.prod`, fuera del repositorio.
2. **Swagger público en producción**: es la documentación de la API pública. No expone datos; los endpoints exigen token.
3. **Receptor de prueba de avisos (`/api/testing/webhook-receiver`)** anónimo con una clave de firma de demostración: es la herramienta del escenario de defensa (9.3). Guarda solo los últimos 50 avisos en memoria y limita el tamaño.
4. **Límite de tasa por instancia**: con 2 instancias el límite real es el doble (aclarado en el documento de cambios, 3.1).
5. **`AllowedHosts: *`** y cadena de conexión local en `appsettings.json`: son valores de desarrollo; en el servidor la cadena viene de variables de entorno y el tráfico externo pasa siempre por Caddy.
6. **Modo demo en `localhost`**: acepta una identidad de demostración sin login. Está apagado en el servidor (`Auth:PermitirModoDemo=false`).
7. **Sin purga del outbox procesado**: crecimiento de tabla, no un riesgo de seguridad.
8. **Contraseña de demostración en la app móvil** (`AuthService.DemoRepartidorPassword`): solo rellena el formulario en modo demo.

## Cómo se verifica
```bash
dotnet test UltimaMilla.Backend.slnf -c Release                       # incluye SeguridadEndurecimientoTests
dotnet list UltimaMilla.Backend.slnf package --vulnerable --include-transitive
```
