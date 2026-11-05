using MediatR;
using UltimaMilla.Application.DTOs;

namespace UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;

public class CheckMobileConnectivityQueryHandler : IRequestHandler<CheckMobileConnectivityQuery, MobileConnectivityDto>
{
    public Task<MobileConnectivityDto> Handle(CheckMobileConnectivityQuery request, CancellationToken cancellationToken)
    {
        var response = new MobileConnectivityDto(
            Status: "ok",
            Message: "Servicio operativo",
            TimestampUtc: DateTime.UtcNow
        );

        return Task.FromResult(response);
    }
}
