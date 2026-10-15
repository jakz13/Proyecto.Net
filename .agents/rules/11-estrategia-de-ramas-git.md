---
trigger: always_on
---

# Estrategia de Ramas y Flujo de Trabajo en Git

## - `main` → Rama estable. No se commitea directo durante el desarrollo de casos de uso.
- `desarrollo-backend` → Rama de integración del backend de Anthony (módulo Operación). Todos los casos de uso se desarrollan acá, y se le
  incorpora (merge) lo que se vaya subiendo a `main`.
- `feature/<nombre>`, `fix/<nombre>`, `security/<nombre>` → opcionales, para trabajos puntuales que se integran a `desarrollo-backend`.
- **Pull Requests (PRs)**: el merge de `desarrollo-backend` hacia `main` se hace por PR, con al menos 1 aprobación y la suite de tests en verde en CI.
- Un commit = un cambio lógico (normalmente un caso de uso o un sub-bloque de él). Nunca se commitea sin autorización explícita.
