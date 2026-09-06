namespace TmsApi.Application.Dtos;

public record LoginDto(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string Role = "Student");

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string Email,
    string FirstName,
    string LastName
);

public record RefreshResponse(
    string AccessToken,
    string RefreshToken
);
