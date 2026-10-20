---
trigger: always_on
---

# Agrupación y Organización de DTOs y Contratos de Casos de Uso

## Regla de Organización:

- **Agrupación por caso de uso o módulo:** los `record` de entrada y salida estrechamente relacionados (Command/Query, resultado y request del endpoint)
  se agrupan en el **mismo archivo `.cs`** del caso de uso. Ejemplo: `RecepcionarBultosCommand.cs` contiene `RecepcionarBultosCommand`;
  los DTOs de resultado del mismo módulo van juntos en un único archivo (ej: `DTOs/Deposito/RecepcionDto.cs` con `RecepcionResultDto` y `DiscrepanciaDto`).
- **Evitar archivos sueltos por cada `record` derivado:** no crear un `.cs` independiente para cada pequeño request, response o resultado si
  pertenece conceptualmente al mismo caso de uso o entidad. El handler sí va en su propio archivo (`<Caso>CommandHandler.cs`).
- **Inmutabilidad y tipado fuerte:** los DTOs son `public record` con parámetros posicionales o propiedades `init`.
- **Sin prefijo `Dt`:** los nombres siguen la convención del repo (`EnvioDto`, `ConfirmarCargaResultDto`, `...Request`).
- Los DTOs nunca exponen entidades de dominio.
