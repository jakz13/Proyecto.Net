---
trigger: always_on
---

# Estándares de Commits (Conventional Commits)

**Formato:**

```
##<tipo>(<alcance opcional>): <descripción corta en imperativo, EN ESPAÑOL>

<cuerpo opcional: qué y por qué, no cómo — en español>

<footer opcional: breaking changes, referencias a issues>
```

> ⚠️ **La descripción del commit siempre debe estar en español**, sin importar el idioma del código o comentarios internos.

**Tipos permitidos:**

| Tipo | Uso |
|---|---|
| `feat` | Nueva funcionalidad |
| `fix` | Corrección de bug |
| `security` | Corrección o mejora de seguridad (usar aunque también sea un `fix`) |
| `refactor` | Cambio de código sin alterar comportamiento |
| `perf` | Mejora de rendimiento |
| `test` | Agregar o corregir tests |
| `docs` | Documentación |
| `chore` | Tareas de mantenimiento (deps, configs) |
| `ci` | Cambios en pipelines/CI-CD |
| `style` | Formato, sin cambios de lógica |

**Ejemplos:**

```
feat(auth): agregar autenticación con JWT y refresh token
fix(pacientes): corregir validación de DNI que permitía duplicados
security(logging): enmascarar campos sensibles en logs de Serilog
refactor(repositorio): separar lógica de acceso a datos del servicio
chore(deps): actualizar Microsoft.EntityFrameworkCore a 8.0.6 por CVE-2024-XXXX
```

**Reglas críticas:**

- Un commit = un cambio lógico (no mezclar `feat` + `fix` + `refactor`).
- **Nunca** incluir datos reales de usuarios en el mensaje de commit (ni como ejemplo).
- **Nunca** commitear archivos con secretos (`appsettings.Development.json` con datos reales, `.env`, `.pfx`, `.pem`).
- Antes de cada commit, correr escaneo de secretos (`gitleaks`, `git-secrets` o hook de pre-commit).
- Si un commit corrige una vulnerabilidad, no detallar el exploit en el mensaje si el repo no es privado; referenciar el ticket interno.

**Automatización recomendada:**

- Pre-commit: `dotnet format --verify-no-changes`, `gitleaks protect --staged`.
- Commit-lint adaptado para validar formato y que la descripción esté en español.
- Plantilla de PR con checklist: *¿Este cambio toca datos sensibles? ¿Se agregaron tests? ¿Se corrió el escaneo de secretos?*
