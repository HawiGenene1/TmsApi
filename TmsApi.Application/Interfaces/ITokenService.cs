namespace TmsApi.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(string userId, string email, string firstName, string lastName, IEnumerable<string> roles);
    string GenerateRefreshToken();
}
