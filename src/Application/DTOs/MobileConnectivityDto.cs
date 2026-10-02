namespace UltimaMilla.Application.DTOs;

public record MobileConnectivityDto(
    string Status,
    string Message,
    DateTime TimestampUtc,
    string Environment,
    string Version
);
