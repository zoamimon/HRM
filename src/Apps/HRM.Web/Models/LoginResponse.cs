namespace HRM.Web.Models;

/// <summary>
/// Login API response
/// Contains authentication tokens and user information
/// </summary>
public class LoginResponse
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiry { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshTokenExpiry { get; init; }
    public required UserInfo User { get; init; }
}

/// <summary>
/// User information returned after successful login
/// </summary>
public class UserInfo
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
}
