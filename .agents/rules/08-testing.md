---
trigger: always_on
---

# Testing y Calidad de Pruebas de Software

## Regla Obligatoria de Testing Exhaustivo y Resiliencia:

En todo desarrollo, refactorización o incorporación de casos de uso, los tests automatizados (xUnit) deben ser **exhaustivos** y validar que el sistema sea tolerante a fallos, no solo probando el camino feliz (*happy path*), sino asegurando robustez frente a datos erróneos, formatos inesperados o consultas anómalas.

---

### 1. Obligatoriedad de Escenarios No Felices y Datos Anómalos
Cada suite de pruebas de un servicio, validador o componente de negocio debe incluir obligatoriamente:

- **Valores Nulos y Vacíos (*Null & Empty Safety*)**:
  - Parámetros y DTOs de entrada con valor `null`.
  - Propiedades obligatorias nulas o vacías (`string.Empty`, strings con solo espacios en blanco, listas/colecciones vacías `[]`).
  - Entidades relacionadas nulas (ej. `ReglaOperativa = null`, `MotivoNoEntrega = null`) verificando que se asignen fallbacks seguros o se lancen excepciones tipadas sin crashear con `NullReferenceException`.

- **Valores Fuera de Rango y Límites (*Boundary / Edge Cases*)**:
  - Valores numéricos en los límites extremos: cero, valores negativos, valores máximos permitidos y valores que excedan los topes (ej. peso o volumen <= 0, cantidad de paradas sobre el máximo, intentos de entrega sobre el máximo configurado).
  - Fechas inválidas, en el pasado lejano, futuras fuera de período o en orden cronológico inconsistente (ej. fecha de despacho anterior a la de armado).

- **Identificadores Inválidos e Inexistentes**:
  - Identificadores vacíos (`Guid.Empty`), identificadores que no existen en los repositorios/base de datos y listas con IDs mixtos (válidos + inválidos).
  - Comprobar que se generen advertencias o respuestas `404 NotFound` / `KeyNotFoundException` claras sin corromper el estado del sistema.

- **Formatos y Entradas Malformadas**:
  - Documentos del receptor, correos, teléfonos, códigos de seguimiento o códigos de bulto que no respeten el formato esperado (caracteres especiales, longitud incorrecta).
  - Archivos o payloads con campos faltantes, encabezados incorrectos o filas vacías durante procesos de importación o sincronización.

---

### 2. Validación de Resiliencia y Control de Excepciones
- **Excepciones Tipadas y Explícitas**:
  - Validar que las precondiciones violadas disparen excepciones de dominio o aplicación específicas (`ArgumentNullException`, `ArgumentOutOfRangeException`, `KeyNotFoundException`, `InvalidOperationException`, `ValidationException`).
- **Idempotencia y Prevención de Operaciones Duplicadas**:
  - Probar qué ocurre al reejecutar una acción que ya fue realizada (ej: despachar dos veces la misma hoja de ruta, asignar un envío ya asignado a otra ruta, reimportar el mismo lote con la misma clave de idempotencia). El sistema debe responder de forma idempotente o reportar una advertencia sin duplicar registros.

---

### 3. Aislamiento y Buenas Prácticas
- **Uso estricto de Mocks y Fakes**: Nunca utilizar datos reales ni conexiones a bases de datos de producción en los tests.
- **Trazabilidad y Eventos**: Comprobar que los eventos de dominio solo se emitan cuando la operación fue efectivamente exitosa y no ante fallos.
- **No Mergear sin Tests en Verde**: Todo cambio de código debe acompañarse de sus pruebas unitarias correspondientes y la suite de tests del proyecto debe pasar al 100% (`dotnet test`).
