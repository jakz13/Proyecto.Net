---
trigger: always_on
---

# Manejo de Datos Sensibles

##
- Identificar y etiquetar qué datos son sensibles (PII, datos financieros, salud, etc.).
- Aplicar el principio de minimización: nunca almacenar más datos de los necesarios.
- **Nunca** usar datos reales de producción en entornos de desarrollo o testing. Anonimizar o generar datos sintéticos.
- Cifrar datos sensibles en reposo (AES-256 mínimo, `System.Security.Cryptography`, `Always Encrypted` o TDE en SQL Server).
- Forzar cifrado en tránsito: TLS 1.2+, HSTS habilitado.
- Usar `Data Protection API` de ASP.NET Core para tokens, cookies y datos temporales.
