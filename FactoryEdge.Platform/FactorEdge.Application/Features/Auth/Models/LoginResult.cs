namespace FactorEdge.Application.Features.Auth.Models
{
    public sealed class LoginResult
    {
        public LoginStatus Status { get; set; }
        public LoginResponse? User { get; set; }
        public bool IsSuccess => Status == LoginStatus.Success;
    }
}
