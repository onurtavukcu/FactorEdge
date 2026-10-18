namespace FactorEdge.Application.Features.Auth.Models
{
    public enum LoginStatus
    {
        Success = 1,
        InvalidCredentials = 2,
        UserInactive = 3,
        TenantInactive = 4
    }
}
