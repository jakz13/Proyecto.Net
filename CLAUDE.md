# Última Milla — guía para Claude Code

Plataforma multioperador de distribución de última milla (laboratorio .NET 10, UTEC). Responder y comentar el código **en español**.

## Arquitectura (ADR 01)
- `src/Domain` → entidades y reglas de negocio. **No depende de nada** (ni EF, ni ASP.NET, ni otras capas).
- `src/Application` → casos de uso (MediatR), puertos (`Ports/I*Repository`). Depende solo del dominio.
- `src/Infrastructure` → EF Core + Postgres, repositorios `Ef*`, RabbitMQ, Redis, outbox.
- `src/Api` (monolito modular, minimal APIs), `src/Worker` (proceso independiente, consume la cola), `src/Web/*` (Backoffice Razor Pages, Portal del comercio y Seguimiento público en Blazor), `src/Mobile` (MAUI).
- Las reglas de dependencias están respaldadas por `tests/UltimaMilla.Architecture.Tests` (NetArchTest): si se violan, el pipeline falla.

## Comandos
```
dotnet build UltimaMilla.Backend.slnf -c Release
dotnet test  UltimaMilla.Backend.slnf -c Release          # dominio, aplicación, arquitectura, integración (necesita Postgres local: docker compose up -d postgres)
dotnet ef migrations add <Nombre> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
dotnet list UltimaMilla.Backend.slnf package --vulnerable --include-transitive
```
Despliegue manual: `scripts\desplegar.bat [api|worker|backoffice|portal|panel]`. Con push a `main`, el pipeline despliega solo.

## Reglas que no se negocian
1. **Cada cambio del modelo lleva su migración.** Nunca ocultar propiedades con `Ignore` ni guardar datos en diccionarios estáticos: la API corre en 2 instancias y el worker es otro proceso; el estado vive en la base.
2. **Aislamiento por inquilino en dos niveles:** operador (filtro global EF) y comercio (el handler/repositorio filtra por `ComercioOperadorId`). Cada entidad nueva con `OperadorId` lleva su filtro y su prueba.
3. **Autorización por perfil en cada endpoint** (`PoliticasApi`: Administracion, Operaciones, Deposito, Repartidor, Comercio). Un endpoint nuevo sin política es un defecto. La escritura de administración usa `.ExigirParaEscritura(PoliticasApi.Administracion)`.
4. **Máquinas de estado declarativas** (`MaquinaEstadosEnvio`, `MaquinaEstadosBulto`, `MaquinaEstadosRetiro`): el estado solo cambia por métodos de la entidad.
5. **Outbox:** el evento se guarda en la misma transacción que el cambio (`EfUnitOfWork`). El consumidor es idempotente.
6. **Sin secretos en el repo.** Credenciales en `.env.prod` (VM) o secretos de GitHub. `localhost` mantiene el modo demo sin login (`Auth:PermitirModoDemo`); en el servidor está apagado.
7. Lo que se cambia lleva prueba. Una corrección de seguridad lleva una prueba que fallaba antes.

## Convenciones
- Commits en español, formato `tipo(ámbito): resumen`, **sin** marcas de herramientas. PRs breves; no abrirlos sin pedido.
- Ramas: `desarrollo-backend` (backend), `mobile`, rama de frontend; `main` despliega. No tocar los módulos de los compañeros sin avisar.
- Las pruebas de integración comparten una base: no depender del estado que dejan otras (crear repartidor/vehículo propios), y no usar `despacho@gmail.com` (otra prueba bloquea esa cuenta a propósito).
