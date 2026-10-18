using FactorEdge.Application.Features.Auth.Models;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using FactorEdge.Application.Interfaces.Services.Auth;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;


namespace FactorEdge.Application.Features.Auth.Services
{
    public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
    {
        public string GenerateToken(LoginResponse user)
        {
            var settings = options.Value;

            if (string.IsNullOrWhiteSpace(settings.Issuer) ||
                string.IsNullOrWhiteSpace(settings.Audience) ||
                string.IsNullOrWhiteSpace(settings.Key) ||
                settings.ExpirationMinutes <= 0)
            {
                throw new InvalidOperationException(
                    "JWT configuration is invalid.");
            }

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(settings.Key);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    "JWT signing key must be Base64 encoded.");
            }

            if (keyBytes.Length < 32)
                throw new InvalidOperationException(
                    "JWT signing key must be at least 256 bits.");

            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

            // SuperAdmin için TenantId claim'i oluşturulmaz.
            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim(
                    "tenantId",
                    user.TenantId.Value.ToString()));
            }

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;

            var token = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(settings.ExpirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
