using MediatR;
using UltimaMilla.Application.DTOs;

namespace UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;

public class CheckMobileConnectivityQueryHandler : IRequestHandler<CheckMobileConnectivityQuery, MobileConnectivityDto>
{
    public Task<MobileConnectivityDto> Handle(CheckMobileConnectivityQuery request, CancellationToken cancellationToken)
    {
        var response = new MobileConnectivityDto(
            Status: "Online",
            Message: "Conexión exitosa entre la App Móvil MAUI y la Web API de Última Milla",
            TimestampUtc: DateTime.UtcNow,
            Environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            Version: "1.0.0"
        );

        return Task.FromResult(response);
    }
}
