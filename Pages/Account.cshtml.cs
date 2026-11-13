using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Mediatheque;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mediatheque.Pages;

public sealed class AccountModel(LibraryDatabase database) : PageModel
{
    [BindProperty] public string LoginEmail { get; set; } = "";
    [BindProperty] public string LoginPassword { get; set; } = "";
    [BindProperty] public string DisplayName { get; set; } = "";
    [BindProperty] public string RegisterEmail { get; set; } = "";
    [BindProperty] public string RegisterPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? CurrentEmail { get; private set; }
    public string? CurrentRole { get; private set; }
    public string DatabasePath => database.DatabasePath;

    public IActionResult OnGet()
    {
        CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
        if (CurrentEmail is not null)
        {
            var member = database.GetMemberByEmail(CurrentEmail);
            CurrentRole = member?.Role;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        DisplayName = DisplayName.Trim();
        RegisterEmail = RegisterEmail.Trim();
        if (DisplayName.Length is < 2 or > 80 || !new EmailAddressAttribute().IsValid(RegisterEmail) || RegisterPassword.Length < 10)
        {
            ErrorMessage = "Indiquez un nom (2 à 80 caractères), une adresse e-mail valide et un mot de passe d’au moins 10 caractères.";
            return Page();
        }
        if (RegisterPassword != ConfirmPassword)
        {
            ErrorMessage = "Les deux mots de passe ne correspondent pas.";
            return Page();
        }

        var hash = HashPassword(RegisterPassword);
        var id = database.CreateMember(RegisterEmail, DisplayName, hash);
        if (id is null)
        {
            ErrorMessage = "Un compte utilise déjà cette adresse e-mail.";
            return Page();
        }
        await SignInAsync(id.Value, RegisterEmail, DisplayName, "Member");
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        var member = database.GetMemberByEmail(LoginEmail.Trim());
        if (member is null || !VerifyPassword(LoginPassword, member.PasswordHash))
        {
            ErrorMessage = "Adresse e-mail ou mot de passe incorrect.";
            return Page();
        }
        await SignInAsync(member.Id, member.Email, member.DisplayName, member.Role);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private async Task SignInAsync(int id, string email, string displayName, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return $"210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < 100_000)
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
