namespace UltimaMilla.Application.DTOs;

public record MobileConnectivityDto(
    string Status,
    string? Message = null,
    DateTime? TimestampUtc = null
);
