namespace Core.Identity.Requests;

public sealed record RegisterRequest(string UserName, string Email, string PhoneNumber, string Password);
