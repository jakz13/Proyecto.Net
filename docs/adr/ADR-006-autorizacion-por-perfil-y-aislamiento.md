# ADR-006: Cómo manejamos usuarios, permisos y separación entre operadores y comercios

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.3 y 6.5

## 1. Contexto y problema
En el sistema conviven tres niveles de acceso: el **operador logístico** (inquilino principal), el **comercio** (cliente del operador, que solo puede ver lo suyo) y el **destinatario** (anónimo). Además, dentro de cada operador hay distintos perfiles: administrador, despachador, operario de depósito y repartidor.

La letra pide autorización en la API por perfil, aislamiento estricto entre operadores y entre comercios, y pruebas automatizadas que demuestren que nadie puede ver datos de otro.

## 2. Decisión
- **Autenticación con tabla propia y JWT emitido por la API:** usamos nuestra tabla `Usuarios` (vinculada a `Perfil`, `OperadorId` y `ComercioId`) y emitimos tokens JWT HS256 con esos datos en los claims.
  - Para guardar contraseñas usamos `PasswordHasher` de ASP.NET Identity (PBKDF2 con sal aleatoria), exigimos contraseñas fuertes (mínimo 10 caracteres combinados), bloqueamos la cuenta tras 5 intentos fallidos por 15 minutos y limitamos intentos por IP.
  - *Nota transparente:* la letra menciona usar OIDC/OAuth 2.1 o el almacén completo de ASP.NET Identity (`IdentityUser`). Elegimos mantener nuestra tabla propia para no duplicar tablas ni acoplar el dominio, y evitar un servidor OIDC externo por costo y simplicidad.
- **Aislamiento en dos niveles:**
  - **Nivel 1 (Operador):** filtro global automático en Entity Framework Core por `OperadorId` en todas las entidades del operador, más validación al guardar para impedir escribir en otro operador.
  - **Nivel 2 (Comercio):** los repositorios y casos de uso filtran por la relación `ComercioOperadorId`. Si un comercio pide un recurso de otro, se responde `404 Not Found` para ni siquiera confirmar que existe.
- **Autorización por perfil en cada endpoint (`PoliticasApi`):** cada endpoint exige su política (*Administracion*, *Operaciones*, *Deposito*, *Repartidor*, *Comercio*, etc.). En desarrollo local (`localhost`), el modo demo permite probar sin login, pero en el servidor está apagado y solo vale un JWT real.

## 3. Alternativas consideradas
- **Pedir solo `RequireAuthorization()` genérico:** descartada (y corregida tras detectarse al inicio), porque permitía que un usuario de comercio llamara endpoints internos del operador.
- **Base de datos o esquema separado por operador:** descartada por costo y complejidad de migraciones frente a filtros por fila.
- **Proveedor OIDC externo o ASP.NET Identity completo (`AspNetUsers`):** descartadas para no sumar infraestructura extra ni duplicar el modelo de usuarios y perfiles ya diseñado.

## 4. Consecuencias
- **Ventajas:** cada endpoint queda protegido por perfil y cada consulta filtrada por operador y comercio, validado por una batería de pruebas de integración (`AutorizacionPorPerfilApiTests`) que recorren roles permitidos y denegados.
- **Desventajas y cómo se mitigan:** mantener la emisión y validación propia de JWT requiere más cuidado en seguridad (por eso se valida algoritmo, firma, vencimiento, emisor y audiencia). También aceptamos como limitación conocida que el límite de tasa por IP cuenta por instancia (con 2 instancias el tope efectivo es el doble).
