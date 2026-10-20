---
trigger: always_on
---

# Prevención de Vulnerabilidades

##
- **SQL Injection**: usar LINQ, `FromSqlInterpolated` o procedimientos parametrizados. Nunca `FromSqlRaw` con concatenación de strings.
- **XSS**: escapar toda salida renderizada en vistas/HTML.
- **CSRF**: Habilitar protección anti-forgery (`[ValidateAntiForgeryToken]` o middleware de Antiforgery) en endpoints mutables si se usan cookies de sesión.
- **Headers de seguridad HTTP**: Configurar headers de seguridad OWASP (`Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Strict-Transport-Security`).
- **Inyección de comandos**: evitar `Process.Start` o `shell=true` con entrada de usuario sin sanitizar.
- Evitar `eval`/ejecución dinámica de código con entrada no confiable.
- Validar TODA entrada de usuario en el servidor (no solo en cliente), usando `FluentValidation` o `DataAnnotations`.
- Validar tamaño de payloads y archivos subidos para evitar DoS.
