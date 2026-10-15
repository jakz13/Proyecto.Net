---
trigger: always_on
---

# Autenticación y Autorización

##
- Usar ASP.NET Core Identity o un IdP externo (Azure AD, Duende IdentityServer) en vez de auth propia.
- Hashear contraseñas solo con bcrypt/argon2 (nunca MD5/SHA1 planas).
- Aplicar `[Authorize]` con políticas granulares en cada endpoint, no solo en el frontend.
- **Protección Anti-IDOR (Insecure Direct Object References)**: Validar siempre que el usuario autenticado (claims del JWT/ClaimsPrincipal) posea autorización explícita sobre el **recurso específico** solicitado (`resourceId`), no solo que tenga acceso al endpoint.
- Aplicar principio de mínimo privilegio en roles y permisos.
- Implementar auditoría de accesos a datos sensibles (quién, qué, cuándo).
