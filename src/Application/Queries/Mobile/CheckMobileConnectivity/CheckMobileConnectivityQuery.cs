using MediatR;
using UltimaMilla.Application.DTOs;

namespace UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;

public record CheckMobileConnectivityQuery() : IRequest<MobileConnectivityDto>;
