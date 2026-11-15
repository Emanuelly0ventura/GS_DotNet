namespace AgroConnect.Application.DTOs;

public sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn, UserResponse User);
