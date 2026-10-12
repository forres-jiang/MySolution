namespace My.XXX.APIs.Contracts.Authentication;

public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiryInMinutes);
