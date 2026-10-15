using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
