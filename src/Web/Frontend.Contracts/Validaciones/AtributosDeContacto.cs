using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Validaciones;

// Reglas de formato compartidas por todos los formularios (backoffice y portal). Heredan de RegularExpressionAttribute a propósito:
// así el backoffice las valida también en el navegador (data-val-regex) y Blazor las valida con DataAnnotationsValidator, y el error se
// muestra con el mismo texto rojo (asp-validation-for / ValidationMessage). El backend vuelve a validar todo en el dominio
// (ContactoValidacion): lo de acá es la comodidad de la pantalla, no la seguridad.
// Los patrones usan solo sintaxis válida en JavaScript y en .NET.

/// <summary>Correo con arroba, parte local sin puntos al borde ni repetidos y dominio válido con extensión (nombre@empresa.com). Vacío es válido: combinar con [Required] si es obligatorio.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EmailValidoAttribute : RegularExpressionAttribute
{
    public const string PatronEmail = @"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@([A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,24}$";
    public const string Mensaje = "Ingrese un correo electrónico válido (ej.: nombre@empresa.com).";

    public EmailValidoAttribute() : base(PatronEmail) => ErrorMessage = Mensaje;
}

/// <summary>Celular de exactamente 9 dígitos (se admiten espacios o guiones entre dígitos, p. ej. 099 123 456). Vacío es válido: combinar con [Required] si es obligatorio.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TelefonoNueveDigitosAttribute : RegularExpressionAttribute
{
    public const string PatronTelefono = @"^\s*[0-9]([ \-]?[0-9]){8}\s*$";
    public const string Mensaje = "El teléfono debe tener exactamente 9 dígitos, solo números (ej.: 099123456).";

    public TelefonoNueveDigitosAttribute() : base(PatronTelefono) => ErrorMessage = Mensaje;
}

/// <summary>Dirección o calle: con al menos una letra y sin signos de marcado (&lt; &gt;) ni caracteres de control.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DireccionValidaAttribute : RegularExpressionAttribute
{
    public const string PatronDireccion = @"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])[^<>\u0000-\u001F]+$";
    public const string Mensaje = "Ingrese una dirección válida: debe incluir letras y no puede contener los signos < >.";

    public DireccionValidaAttribute() : base(PatronDireccion) => ErrorMessage = Mensaje;
}

/// <summary>Número de puerta: letras, números, espacio, guion, punto o barra (1234, 1234 bis, S/N).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NumeroPuertaValidoAttribute : RegularExpressionAttribute
{
    public const string PatronNumero = @"^[A-Za-z0-9][A-Za-z0-9 /\-.]{0,29}$";
    public const string Mensaje = "El número de puerta no es válido (use letras, números, espacio, guion o barra; ej.: 1234, 1234 bis, S/N).";

    public NumeroPuertaValidoAttribute() : base(PatronNumero) => ErrorMessage = Mensaje;
}

/// <summary>Nombre de ciudad: solo letras, espacios, punto, apóstrofo o guion.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CiudadValidaAttribute : RegularExpressionAttribute
{
    public const string PatronCiudad = @"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ][A-Za-zÁÉÍÓÚÜÑáéíóúüñ .'\-]{1,99}$";
    public const string Mensaje = "La ciudad no es válida (solo letras, espacios, punto, apóstrofo o guion).";

    public CiudadValidaAttribute() : base(PatronCiudad) => ErrorMessage = Mensaje;
}

/// <summary>Texto libre sin signos de marcado (&lt; &gt;) ni caracteres de control (nombres, razón social, etc.).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SinMarcadoAttribute : RegularExpressionAttribute
{
    public const string PatronSinMarcado = @"^[^<>\u0000-\u001F]*$";
    public const string Mensaje = "El texto no puede contener los signos < > ni caracteres de control.";

    public SinMarcadoAttribute() : base(PatronSinMarcado) => ErrorMessage = Mensaje;
}

/// <summary>Color con formato #RRGGBB.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ColorHexValidoAttribute : RegularExpressionAttribute
{
    public const string PatronColor = @"^#[0-9A-Fa-f]{6}$";
    public const string Mensaje = "El color debe tener el formato #RRGGBB (ej.: #2563EB).";

    public ColorHexValidoAttribute() : base(PatronColor) => ErrorMessage = Mensaje;
}
