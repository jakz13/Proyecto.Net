# Estado de la auditoría nocturna (leer esto primero)

> Se actualiza al cerrar cada ronda. Detalle completo en [INFORME.md](INFORME.md); línea de tiempo en `diario.log`.

- **Última actualización:** 2026-11-14 09:25
- **Estado:** producción **operativa** en la VM reconstruida. Escenarios 1 a 7 de la defensa ensayados.
- **Qué pasó esta madrugada con F7 (destruir y reconstruir):** la reconstrucción de las 05:08 recreó la VM, la IP y la aplicación (≈ 9 min), pero **dejó los dominios sin servicio hasta las 09:12** porque `scripts/actualizar-dns.ps1` estaba vacío desde que se creó (defecto D-16). Ya está escrito, probado y publicado; el servicio se restableció a mano a las 09:12.
- **Hallazgos:** 17 (D-01 a D-17), todos corregidos con prueba que fallaba antes. Más observaciones OBS-01 a OBS-21 (ver informe).
- **IP actual de la VM:** `57.156.63.70` (cambia en cada reconstrucción). El secreto `VM_HOST` de GitHub apunta a esa IP y **el despliegue automático ya funciona contra la VM nueva** (comprobado a las 09:35: servicios recreados con la imagen del pipeline, dominios y `/ready` en 200). D-17: yo mismo dejé el secreto con una marca BOM delante la primera vez y el despliegue falló; corregido.
- **Último commit publicado de código:** `10f638b` (línea base previa a la auditoría: `d65d984`).

## Qué hay que dejar listo antes de la defensa (jueves 19/11, 15:00)
1. **Receptor de prueba de avisos (escenario 6): APAGADO** (es el valor por defecto tras reconstruir). Para encenderlo:
   `ssh -i ~/.ssh/azure_ultimamilla azureuser@<IP> "cd ~/shippingbox && echo RECEPTOR_PRUEBA_AVISOS=true >> .env.prod && for s in api1 api2; do docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --no-deps --force-recreate \$s; sleep 40; done"`.
   Es un endpoint anónimo: apagarlo después. El comercio debe suscribirse a un evento y apuntar a `http://api1:8080/api/testing/webhook-receiver` (con clave `secreto-demo-comercio`).
2. **Operador 2 (escenario 4):** no trae zonas ni comercios. Hay que crearle una zona (con polígono), un comercio y un usuario de comercio, o ensayar esa secuencia (ver ronda 8).
3. **No ensayar F7 otra vez antes del jueves** salvo necesidad: Let's Encrypt limita a 5 certificados por semana por nombre y cada reconstrucción pide certificados nuevos (OBS-21). Si hay que reconstruir: definir `DUCKDNS_TOKEN` y `SEED_PASSWORD_DEMO` y ejecutar `scripts\reconstruir.ps1`; si Caddy no obtiene el certificado, `docker restart shippingbox-caddy-1`.
4. Antes de la defensa, comprobar que las imágenes de `ghcr.io` están al día (la reconstrucción las descarga de ahí; OBS-20).
5. Costo del ambiente: ≈ 1,40 USD/día (≈ 7,7 USD hasta la defensa). Después de la defensa: `terraform destroy`.

## Hallazgos de mayor impacto para la defensa
| # | Gravedad | Qué pasaba | Estado |
|---|---|---|---|
| D-16 | crítica (F7) | `actualizar-dns.ps1` vacío: tras reconstruir, los dominios quedaban apuntando a la IP vieja | corregido y verificado |
| D-14, D-15 | alta (F7) | la reconstrucción se cortaba por comillas de PowerShell 5.1 y por el progreso de `docker compose` | corregidos (scripts) |
| D-06 | crítica (F5) | el formulario de tarifas renombraba todas las reglas y las altas fallaban | corregido y verificado |
| D-07 | alta (F5) | no se podía publicar la tarifa vigente (volumen sin límite) y no mostraba el error | corregido y verificado |
| D-08 | crítica (F5) | publicar una regla operativa cambiaba en silencio la prueba de entrega y la devolución | corregido y verificado |
| D-10, D-11 | alta (F6) | la suscripción a "Admisión de Envío" nunca generaba avisos; el worker no alcanzaba el receptor de prueba | corregidos y verificados |
| D-13 | alta | el hub de tiempo real aceptaba posiciones y canales de cualquier usuario y los difundía a todos los operadores | corregido y verificado |
| D-12 | media-alta | el token de un usuario desactivado seguía valiendo 8 h | corregido y verificado |
| D-01 | alta | el límite de tasa era un cupo único compartido por todos los visitantes | corregido y verificado |
| D-02 a D-05 | media | elementos nulos en listas → HTTP 500; `{}` guardaba datos por defecto | corregidos y verificados |

## Estado de producción ahora
- Base **nueva y sembrada**: tarifa (150 / 300 / 120), regla operativa original (3 intentos), 3 zonas del operador 1, 7 cuentas demo con la contraseña única. Todos los datos de ensayo (`AUDIT…`, versiones intermedias, suscripciones) desaparecieron con la reconstrucción.
- Certificados de Let's Encrypt vigentes hasta el 12/02/2027 en los 4 dominios.

## Cómo saber si sigo trabajando
1. La hora de la última línea de `docs/auditoria/diario.log`.
2. La fecha de modificación de `C:\Users\Curbe\.claude\projects\c--Users-Curbe-OneDrive-Escritorio-Proyecto-Proyecto-Net\70ebc7ed-eac9-44d4-99b2-f66f2414612d.jsonl`.
3. `C:\Users\Curbe\auditoria_local\latido.txt` (anti-suspensión, cada 5 min; vence hoy a las 14:00) y `reanudar.log` (script de reanudación).

## Pendiente
- Prueba de humo de regresión sobre la VM reconstruida (formularios de tarifa y reglas, rechazo de datos mal formados, límite de tasa, token desactivado).
- Nuevas ideas de prueba (ver cola en el informe) y cierre final: limpieza de datos de prueba (ya hecha por la reconstrucción) y estado definitivo.
