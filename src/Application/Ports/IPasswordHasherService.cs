namespace UltimaMilla.Application.Ports;

public interface IPasswordHasherService
{
    string HashearPassword(string passwordPlano);
    bool VerificarPassword(string passwordPlano, string passwordHash);
}
