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

## Segunda auditoría (10/11): lo que se corrigió
Se revisó el proyecto como lo haría alguien que lo ataca. Cada punto tiene su prueba.
| Hallazgo | Corrección |
|---|---|
| **Crítico.** El seeder creaba en producción cuentas con las contraseñas publicadas en el repositorio (`admin@gmail.com`…) y claves de API de demostración | `PoliticaDeSemillas`: en producción no se crea ninguna cuenta sin `SEED_PASSWORD_DEMO` (≥ 12 caracteres); las claves de API de demostración son aleatorias; al arrancar se **rotan** las cuentas y claves que aún tengan el valor publicado. Pruebas: `PoliticaDeSemillasTests` |
| Paquete `SignalR.StackExchangeRedis` con vulnerabilidad alta (GHSA-wr8v-9x5q-ggcm) | Actualizado a 10.0.15; `dotnet list package --vulnerable` sin resultados |
| Swagger de la API interna público | El contrato interno solo se publica en desarrollo; en el servidor queda el de la API pública (`/swagger/integracion`) |
| Modo demo "fail-open" (activo si faltaba la variable) | Seguro por defecto: sin `Auth:PermitirModoDemo` definido, solo existe en el entorno Development (`ModoDemo`) |
| Endpoint anónimo `POST /api/mobile/v1/demo/reset` que modificaba datos | **Eliminado** (con los métodos de reinicio de demostración del dominio) |
| Receptor de pruebas de avisos anónimo, con clave fija y estado en memoria del proceso (incoherente con 2 instancias) | Solo existe si se lo habilita (`Avisos:ReceptorDePrueba:Habilitado`, por defecto solo en desarrollo; en el servidor, `RECEPTOR_PRUEBA_AVISOS=true` durante una demostración). Su estado vive en el caché distribuido |
| Sin política de contraseñas | `PoliticaDePassword`: ≥ 10 caracteres, tres tipos de caracteres, sin contraseñas triviales; se aplica al alta de usuarios y de repartidores y al cambio de contraseña. Pruebas: `PoliticaDePasswordTests` |
| Inicio de sesión sin límite por origen | Límite por IP (10 por minuto, `RateLimit:LoginPorMinuto`) además del bloqueo por cuenta. Prueba: `LimiteDeLoginApiTests` |
| Validación del JWT incompleta | Algoritmo fijo HS256 (rechaza `none`, HS512, RS256), `nbf`, emisor y audiencia obligatorios. Pruebas: `TokenJwtServiceTests` |
| El menú del Backoffice mostraba la marca de demostración a todos los operadores | La marca (nombre, logo, colores) se pide a la API con el token del usuario: cada operador ve la suya |
| Datos de demostración dentro del código de producción (coordenadas por código `ENV-2026-0010x`, teléfono y franja por defecto) | Eliminados |
| App móvil con credenciales del repartidor, evidencia, firma y GPS inventados y clave de cifrado fija en DEBUG | Eliminados (ver `docs/Plan_de_Cierre.md`); la clave de desarrollo es aleatoria por equipo |
| `docker-compose.yml` de desarrollo publicaba Postgres, Redis y RabbitMQ con credenciales por defecto en todas las interfaces | Los puertos se publican solo en `127.0.0.1` |
| Archivos sueltos en la raíz (copias de la letra, `dotnet-install.ps1`), página de privacidad de plantilla, `.vscode/` | Eliminados o ignorados; la letra queda una sola vez en `docs/` |

## Riesgos aceptados (decirlos antes de que los encuentren)
1. **Límite de tasa por instancia**: con 2 instancias el límite real es el doble (aclarado en el documento de cambios, 3.1).
2. **`AllowedHosts: *` y sin Content-Security-Policy en el Backoffice y los portales** (la API sí la tiene): el tráfico externo pasa siempre por Caddy, que solo atiende los dominios configurados. Una política CSP estricta exige probar cada pantalla (mapas, gráficos, Blazor) en un navegador; queda como mejora.
3. **Sin purga del outbox procesado**: crecimiento de tabla, no un riesgo de seguridad.
4. **Modo demo en `localhost`**: en el entorno Development se acepta una identidad de demostración sin login; no existe fuera de él salvo que se lo fuerce con `Auth__PermitirModoDemo=true`.
5. **JWT propio en lugar de `Microsoft.IdentityModel`**: se validan firma (comparación en tiempo constante), algoritmo, vencimiento, `nbf`, emisor y audiencia, con pruebas; migrar a la librería estándar es una mejora de mantenimiento.
6. **Excepciones capturadas sin registrar** en los avisos en tiempo real posteriores a guardar (`catch` con comentario): son deliberadas, el cambio ya está confirmado y el aviso es complementario.

## Cómo se verifica
```bash
dotnet test UltimaMilla.Backend.slnf -c Release                       # incluye SeguridadEndurecimientoTests
dotnet list UltimaMilla.Backend.slnf package --vulnerable --include-transitive
```
