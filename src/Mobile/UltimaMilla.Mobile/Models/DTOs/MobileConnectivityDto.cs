namespace UltimaMilla.Mobile.Models.DTOs;

public record MobileConnectivityDto(
    string Status,
    string Message,
    DateTime TimestampUtc,
    string Environment,
    string Version
);
