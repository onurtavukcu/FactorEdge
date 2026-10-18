using FactorEdge.Application.Features.Auth.Models;

namespace FactorEdge.Application.Interfaces.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
        Task LogoutAsync();
    }
}
