
# Estilo de Código y Comentarios

## Reglas Absolutas de Estilo

### 1. Prohibición Total de Emojis
- **Nunca** usar emojis en ninguna parte del proyecto:
  - Código fuente (`.cs`, `.ts`, `.js`, `.html`, `.css`, etc.)
  - Comentarios de código (inline, XML doc, bloque)
  - Mensajes de log
  - Mensajes de commit (ver regla `10-estandares-de-commits.md`)
  - Archivos de configuración (`appsettings.json`, `.env`, etc.)
  - Documentación interna (`README.md`, wikis, etc.)
  - Interfaces de usuario (labels, tooltips, placeholders, mensajes de error, etc.)
- **Si se necesita un ícono visual** en el frontend, usar exclusivamente **íconos vectorizados** (SVG):
  - Librerías recomendadas: Heroicons, Lucide, Tabler Icons, Phosphor Icons.
  - Los SVG deben inlinarse o referenciarse como componente, **nunca** reemplazarse por un emoji de Unicode.

### 2. Idioma de Comentarios: Español Obligatorio
- **Todos los comentarios** deben estar escritos en **español**, sin excepción:
  - Comentarios inline (`//`)
  - Comentarios de bloque (`/* */`)
  - XML doc comments (`/// <summary>`, `/// <param>`, `/// <returns>`, etc.)
  - Comentarios `TODO`, `FIXME`, `HACK`, `NOTE`
- **No se permite** escribir comentarios en inglés, aunque el identificador o la API referenciada sea en inglés.
- Los **nombres de variables, métodos y clases** pueden seguir convenciones técnicas en inglés o español según la consistencia del módulo, pero el **texto descriptivo dentro de comentarios** siempre será en español.

**Ejemplo correcto:**
```csharp
// Verificamos si el estudiante ya tiene una postulacion activa antes de crear una nueva
var postulacionExistente = await _repositorio.ObtenerPorEstudianteAsync(estudianteId);

/// <summary>
/// Procesa la prelacion de postulantes segun los criterios configurados para el ciclo actual.
/// </summary>
/// <param name="cicloId">Identificador unico del ciclo academico a procesar.</param>
/// <returns>Resultado con el listado de postulantes admitidos y rechazados.</returns>
public async Task<DtResultadoPrelacion> ProcesarPrelacionAsync(Guid cicloId) { ... }
```

**Ejemplo incorrecto:**
```csharp
// Comentario en ingles - PROHIBIDO
// Check if the student already has an active application

// XML doc en ingles - PROHIBIDO
/// <summary>Processes the prelation for the current cycle.</summary>
```
