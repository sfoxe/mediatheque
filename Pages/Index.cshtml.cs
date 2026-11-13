using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mediatheque.Pages;

public sealed class IndexModel(LibraryDatabase database) : PageModel
{
    public string Section { get; private set; } = "home";
    public IReadOnlyList<Book> Books { get; private set; } = [];
    public IReadOnlyList<LoanRow> Loans { get; private set; } = [];
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public IReadOnlyList<string> MediaTypes { get; private set; } = [];
    public int BookCount { get; private set; }
    public int AvailableBookCount { get; private set; }
    public int ActiveLoanCount { get; private set; }
    public bool IsAuthenticated { get; private set; }
    [TempData] public string? LoanMessage { get; set; }

    [BindProperty(SupportsGet = true)] public string Q { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Category { get; set; } = "Toutes les catégories";
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "Tous les supports";
    [BindProperty(SupportsGet = true)] public string Availability { get; set; } = "Tous les états";

    public void OnGet(string? section)
    {
        Section = section == "saved" || section == "loans" ? "loans" : section == "catalogue" ? "catalogue" : "home";
        IsAuthenticated = User.Identity?.IsAuthenticated == true;
        var userId = GetCurrentUserId();
        BookCount = database.GetBookCount();
        AvailableBookCount = database.GetAvailableBookCount();
        ActiveLoanCount = userId is int loanUserId ? database.GetActiveLoans(loanUserId).Count : 0;

        if (Section == "catalogue")
        {
            Categories = database.GetCategories();
            MediaTypes = database.GetMediaTypes();
            Books = database.SearchBooks(Q, Category, Type, Availability);
        }
        else if (Section == "loans" && userId is int memberId)
        {
            Loans = database.GetActiveLoans(memberId);
        }
        else
        {
            Books = database.SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états").Take(4).ToList();
        }
    }

    public IActionResult OnPostBorrow(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToPage("/Account");
        var book = FindBook(id);
        if (book is null) return NotFound();
        LoanMessage = database.BorrowBook(userId.Value, id)
            ? "Le livre a été emprunté pour 14 jours."
            : "Ce livre n’est plus disponible.";
        return RedirectToPage(new { section = "loans" });
    }

    public IActionResult OnPostExtend(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToPage("/Account");
        LoanMessage = database.ExtendLoan(userId.Value, id)
            ? "Le prêt a été prolongé de 7 jours."
            : "Ce prêt ne peut pas être prolongé : il a déjà été prolongé ou n’est plus actif.";
        return RedirectToPage(new { section = "loans" });
    }

    public IActionResult OnPostReturn(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToPage("/Account");
        LoanMessage = database.ReturnLoan(userId.Value, id)
            ? "Le livre a été rendu et est de nouveau disponible."
            : "Ce prêt n’est plus actif.";
        return RedirectToPage(new { section = "loans" });
    }

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private Book? FindBook(int id) => database
        .SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états")
        .FirstOrDefault(book => book.Id == id);
}
