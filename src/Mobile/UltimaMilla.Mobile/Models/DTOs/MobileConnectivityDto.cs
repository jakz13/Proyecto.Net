namespace UltimaMilla.Mobile.Models.DTOs;

public record MobileConnectivityDto(
    string Status,
    string? Message = null,
    DateTime? TimestampUtc = null
);
