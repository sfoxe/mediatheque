namespace Mediatheque;

public sealed record MemberAccount(int Id, string Email, string DisplayName, string PasswordHash, string Role);
