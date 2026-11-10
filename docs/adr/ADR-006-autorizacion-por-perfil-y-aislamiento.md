# ADR-006: Autorización por perfil y aislamiento de dos niveles

**Estado:** aprobado · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.3 y 6.5

## 1. Contexto y restricciones
Hay tres niveles de actores: el operador (inquilino), el comercio (cliente del operador, con sus propios usuarios) y el destinatario (anónimo). Dentro del operador conviven administrador, despachador, operario de depósito y repartidor. La letra pide autorización a nivel de API contemplando los perfiles y, además, aislamiento por operador y por comercio, con pruebas automatizadas que demuestren que no hay filtración.

## 2. Decisión
- **Autenticación:** ASP.NET Identity como almacén y JWT firmado con el perfil como claim de rol, el operador (`operador_id`) y, para usuarios de comercio, la relación comercio-operador (`comercio_id`). Contraseñas con *hash*, bloqueo tras 5 intentos fallidos (423) y secretos fuera del repositorio.
- **Aislamiento del operador (primer nivel):** filtro global de EF Core por `OperadorId` en toda entidad con inquilino, y una validación al guardar que impide escribir recursos de otro operador o con el propietario vacío.
- **Aislamiento del comercio (segundo nivel):** los casos de uso y repositorios filtran por la relación comercio-operador cuando el usuario es de comercio; el recurso de otro comercio se informa como **inexistente (404)** para no revelar que existe.
- **Autorización por perfil en cada endpoint** (`PoliticasApi`): *Administracion*, *Operaciones*, *Deposito*, *Repartidor*, *Comercio* y *PersonalDelOperador*. Los grupos de administración se pueden **leer** con un perfil más amplio y se **modifican** solo con el administrador (`ExigirParaEscritura`). Los endpoints son denegados por defecto: uno nuevo sin política es un defecto.
- **Modo demo local** (`Auth:PermitirModoDemo`): en `localhost` una identidad con todos los perfiles evita el login; en el servidor está apagado y solo vale un JWT. El endpoint de reinicio de datos de demo no existe con el modo demo apagado.
- **Seguimiento público y avisos:** anónimo, con límite de tasa y sin datos personales; el canal por clave de API autentica al comercio y fija el operador.

## 3. Alternativas consideradas
- **Solo `RequireAuthorization()`:** es lo que había y era insuficiente: un usuario de comercio podía listar usuarios, comercios, reglas y reportes del operador llamando directamente a la API (detectado y corregido; hoy lo impiden pruebas con sesiones reales).
- **Base de datos o esquema por inquilino:** descartadas en el análisis inicial (ver multitenancy por fila).
- **Proveedor de identidad externo (OIDC):** evaluada y descartada por dependencia y costo en el entorno gratuito.

## 4. Consecuencias
- Las pruebas de integración inician sesión con usuarios reales de cada perfil y recorren una matriz de endpoints permitidos y denegados (`AutorizacionPorPerfilApiTests`), más pruebas de filtración entre operadores y entre comercios.
- Toda entidad nueva con inquilino debe sumar su filtro y su prueba de aislamiento.
- Las políticas se evalúan con el rol del token: cambiar los nombres de perfil exige cambiar las constantes de `PoliticasApi`.
- Limitación conocida: el limitador de tasa de ASP.NET Core cuenta por instancia, de modo que con dos instancias el límite efectivo es el doble del configurado.
