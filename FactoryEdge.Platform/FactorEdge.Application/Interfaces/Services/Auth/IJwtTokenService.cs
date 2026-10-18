using FactorEdge.Application.Features.Auth.Models;

namespace FactorEdge.Application.Interfaces.Services.Auth
{
    public interface IJwtTokenService
    {
        string GenerateToken(LoginResponse user);
    }
}
