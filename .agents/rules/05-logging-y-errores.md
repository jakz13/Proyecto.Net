---
trigger: always_on
---

# Logging y Manejo de Errores

##
- Configurar Serilog/NLog con enrichers o extensiones que enmascaren campos sensibles automáticamente (ej: `email.Mask()`, `dni.Mask()`).
- Nunca loggear objetos completos de request/response (`_logger.LogInformation("{@Request}", req)`) que puedan contener datos sensibles o PII. Loggear propiedades específicas enmascaradas.
- Capturar excepciones explícitamente; nunca silenciarlas.
- No exponer stack traces ni mensajes de error internos en producción (fail-safe).
