---
trigger: always_on
---

# Secretos y Configuración

##
- Nunca hardcodear API keys, contraseñas, tokens o connection strings en el código o en `appsettings.json`.
- Usar **User Secrets** (`dotnet user-secrets`) en desarrollo.
- Usar **Azure Key Vault** / AWS Secrets Manager / equivalente en producción.
- `.env`, `appsettings.Development.json` con datos reales, `.pfx`, `.pem` → siempre en `.gitignore`, nunca en el repo.
