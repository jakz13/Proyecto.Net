---
trigger: always_on
---

# Calidad de Código .NET

##
- Habilitar `Nullable Reference Types` (`<Nullable>enable</Nullable>`).
- Usar `record` para DTOs inmutables cuando aplique.
- Usar `IOptions<T>` para configuración tipada.
- Habilitar analizadores estáticos de seguridad (`Microsoft.CodeAnalysis.NetAnalyzers`, reglas CA2100, CA3001, etc.).
- Funciones pequeñas, con una sola responsabilidad. Nombres claros y descriptivos.
- Separar configuración, lógica de negocio y capa de presentación.
