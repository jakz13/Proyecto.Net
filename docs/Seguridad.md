# Seguridad: auditoría, correcciones y riesgos aceptados

Revisamos todo el repositorio buscando fallas de seguridad (secretos, endpoints sin permiso, entradas de usuario, XSS, archivos subidos, SSRF, sesiones y validación de tokens). Cada corrección quedó respaldada por pruebas automatizadas (`SeguridadEndurecimientoTests`, `AutorizacionPorPerfilApiTests`).

## 1. Primera auditoría: qué se corrigió
| Hallazgo | Riesgo | Qué hicimos |
|---|---|---|
| Un comercio podía listar usuarios, comercios y reglas del operador | Fuga de datos entre perfiles | Política por perfil en cada endpoint (`PoliticasApi`) y matriz de pruebas |
| Un comercio podía ver el detalle de un envío de otro comercio | Fuga entre comercios | El handler filtra por comercio y devuelve `404` si pertenece a otro |
| Endpoint anónimo para reiniciar datos de demo | Cualquiera podía borrar datos | Devuelve `404` cuando `Auth:PermitirModoDemo=false` |
| **SSRF** en webhooks de comercios | El servidor podía ser usado para atacar su red interna | `DestinoWebhookSeguro` resuelve el DNS y bloquea IPs privadas/internas sin seguir redirecciones |
| **XSS almacenado** en Recepción de depósito | Ejecución de JavaScript en el backoffice | Serialización JSON y escape al renderizar |
| Subida de logo sin validar tipo real (`.html`, `.svg`) | XSS almacenado | Solo PNG, JPG o WEBP hasta 2 MB validando la cabecera binaria |
| Token JWT sin `exp` aceptado | Token que no vence nunca | Se rechaza todo token sin expiración |
| Clave JWT de desarrollo usada como respaldo | Permitiría firmar tokens con una clave pública | En `Production` sin `Jwt:SecretKey` la API no arranca |
| Contraseña fija al crear repartidores (`Repartidor.2026!`) | Credencial conocida | El alta exige contraseña (mín. 8 caracteres); al restablecer genera una aleatoria |
| Listado anónimo de operadores | Enumeración de inquilinos | Habilitado solo en modo demo local |
| Cookie del backoffice sin `Secure` | Robo de sesión por HTTP | `Secure` obligatorio fuera de desarrollo |
| Cabeceras de seguridad solo en la API | Clickjacking / sniffing en web | Caddy las agrega en todos los sitios (HSTS, `nosniff`, `X-Frame-Options`, `Referrer-Policy`) |
| Paquete con vulnerabilidad alta | Riesgo en dependencias | Actualizado y auditado en el pipeline de CI |

## 2. Medidas que ya estaban implementadas
- Contraseñas con PBKDF2-SHA256, sal aleatoria y comparación en tiempo constante; bloqueo temporal tras 5 intentos fallidos por 15 minutos y mensaje genérico (*"Credenciales inválidas"*).
- Firma JWT verificada en tiempo constante (`FixedTimeEquals`) y avisos a comercios firmados con `HMAC-SHA256`.
- Sin SQL concatenado: todo pasa por EF Core parametrizado (sin `FromSqlRaw`).
- Aislamiento por operador con filtro global de EF y por comercio en handlers.
- `/metrics` bloqueado en Caddy: solo accesible por Prometheus en la red interna de Docker.
- Seguimiento público con límite de tasa por IP y respuesta idéntica para códigos inexistentes.

## 3. Segunda auditoría (10/11): refuerzos adicionales
| Hallazgo | Qué hicimos |
|---|---|
| Cuentas y claves API de demo sembradas en producción con valores públicos | `PoliticaDeSemillas` impide crear cuentas demo en producción sin `SEED_PASSWORD_DEMO` (≥ 12 caracteres), genera claves API aleatorias y rota credenciales públicas existentes (`PoliticaDeSemillasTests`) |
| Paquete `SignalR.StackExchangeRedis` vulnerable (GHSA-wr8v-9x5q-ggcm) | Actualizado a `10.0.15`; `dotnet list package --vulnerable` queda limpio |
| Swagger de la API interna público | En producción solo queda publicada la API pública (`/swagger/integracion`) |
| Modo demo activo si faltaba la variable | Ahora es seguro por defecto: solo se activa en `Development` (`ModoDemo`) |
| Endpoint `POST /api/mobile/v1/demo/reset` | Eliminado |
| Receptor de prueba de avisos anónimo y en memoria | Solo se habilita con `Avisos:ReceptorDePrueba:Habilitado` (`RECEPTOR_PRUEBA_AVISOS`) y su estado vive en Redis |
| Política de contraseñas débil | `PoliticaDePassword`: ≥ 10 caracteres, tres tipos de caracteres y sin contraseñas triviales (`PoliticaDePasswordTests`) |
| Login sin límite por IP | Límite de 10 por minuto por IP (`RateLimit:LoginPorMinuto`, `LimiteDeLoginApiTests`) |
| Validación parcial del JWT | Algoritmo fijo HS256, `nbf`, emisor y audiencia obligatorios (`TokenJwtServiceTests`) |
| Marca fija en el menú del Backoffice | Se consulta a la API con el token del usuario para mostrar la marca de su operador |
| Datos de demo hardcodeados en producción y en móvil | Eliminados; la clave de cifrado local en desarrollo se genera por equipo |
| Puertos de Postgres, Redis y RabbitMQ expuestos en `0.0.0.0` en local | Restringidos a `127.0.0.1` en `docker-compose.yml` |

## 4. Ruta pública de descargas de la app móvil (15/11)
`https://shippingbox.duckdns.org/descargas/*` entrega el APK firmado y `version.json` sin autenticación (como una tienda de aplicaciones):
- Caddy sirve solo esa carpeta en solo lectura y sin listado de directorio (`version.json` con `Cache-Control: no-store`).
- El APK no lleva secretos del servidor y Android solo instala actualizaciones firmadas con la misma clave.
- La app verifica obligatoriamente el `sha256`, tamaño exacto y URL HTTPS del propio servidor antes de instalar (`PoliticaDeActualizacion`), sin seguir redirecciones.
- **Riesgo aceptado:** cualquiera con la URL puede descargar el APK (sin límite de tasa en esa ruta).
- **Pendiente:** pasar las contraseñas del keystore de firma de `UltimaMilla.Mobile.csproj` a variables de entorno y rotarlas.

## 5. Sesión única activa para repartidores y sincronización en vivo (15/11)
- Al iniciar sesión un **repartidor**, sus tokens anteriores quedan invalidados (`SESION_DESPLAZADA`, `401`), ya que la app móvil se usa en un único dispositivo (`PoliticaSesionUnica` + `SesionUnicaActivaMiddleware`). Administrador, despachador, operario, comercio y cuentas demo sí pueden abrir múltiples sesiones a la vez. Si Redis cae, la política se degrada sin bloquear.
- Para evitar desfasajes entre sesiones abiertas del Backoffice o Portal, toda escritura exitosa de la API emite un aviso `DatosCambiaron` por SignalR (`CambiosEnTiempoRealMiddleware`) con el nombre del área (sin datos) para refrescar listados automáticamente (excluyendo pantallas de alta/edición para no pisar formularios).

## 6. Riesgos aceptados (y conocidos)
1. **Límite de tasa por instancia:** con 2 instancias de la API, el límite efectivo por IP es el doble del configurado.
2. **Sin `Content-Security-Policy` en Backoffice/Portales:** el tráfico pasa por Caddy solo para los dominios configurados; agregar CSP estricto en Blazor y mapas queda como mejora futura.
3. **Sin limpieza automática de `OutboxMessages` procesados:** hace crecer la tabla con el tiempo, sin impacto en seguridad.
4. **Modo demo en `localhost`:** deshabilitado fuera de `Development`, salvo activación explícita.
5. **Validación JWT propia en lugar de `Microsoft.IdentityModel`:** validada con pruebas (`TokenJwtServiceTests`), con migración posible a futuro sin cambiar políticas.

## 7. Cómo verificarlo
```bash
dotnet test UltimaMilla.Backend.slnf -c Release
dotnet list UltimaMilla.Backend.slnf package --vulnerable --include-transitive
```
