using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mediatheque.Pages;

public sealed class IndexModel(LibraryDatabase database) : PageModel
{
    public string Section { get; private set; } = "home";
    public IReadOnlyList<Book> Books { get; private set; } = [];
    public IReadOnlyList<SavedBookRow> SavedBooks { get; private set; } = [];
    public IReadOnlyList<HistoryRow> History { get; private set; } = [];
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public IReadOnlyList<string> MediaTypes { get; private set; } = [];
    public int BookCount { get; private set; }
    public int AvailableBookCount { get; private set; }
    public int SavedBookCount { get; private set; }
    public bool IsAuthenticated { get; private set; }

    [BindProperty(SupportsGet = true)] public string Q { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Category { get; set; } = "Toutes les catégories";
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "Tous les supports";
    [BindProperty(SupportsGet = true)] public string Availability { get; set; } = "Tous les états";

    public void OnGet(string? section)
    {
        Section = section is "catalogue" or "saved" or "history" ? section : "home";
        IsAuthenticated = User.Identity?.IsAuthenticated == true;
        var userId = GetCurrentUserId();
        BookCount = database.GetBookCount();
        AvailableBookCount = database.GetAvailableBookCount();
        SavedBookCount = userId is int memberId ? database.GetSavedBookCount(memberId) : 0;

        if (Section == "catalogue")
        {
            Categories = database.GetCategories();
            MediaTypes = database.GetMediaTypes();
            Books = database.SearchBooks(Q, Category, Type, Availability);
        }
        else if (Section == "saved")
        {
            if (userId is int savedUserId) SavedBooks = database.GetSavedBooks(savedUserId);
        }
        else if (Section == "history")
        {
            if (userId is int historyUserId) History = database.GetHistory(historyUserId);
        }
        else
        {
            Books = database.SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états").Take(4).ToList();
        }
    }

    public IActionResult OnPostSave(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToPage("/Account");
        var book = FindBook(id);
        if (book is null) return NotFound();
        if (!database.IsSaved(userId.Value, book.Id)) database.ToggleSavedBook(userId.Value, book);
        return RedirectToPage(new { section = "saved" });
    }

    public IActionResult OnPostRemove(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToPage("/Account");
        var book = FindBook(id);
        if (book is null) return NotFound();
        if (database.IsSaved(userId.Value, book.Id)) database.ToggleSavedBook(userId.Value, book);
        return RedirectToPage(new { section = "saved" });
    }

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private Book? FindBook(int id) => database
        .SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états")
        .FirstOrDefault(book => book.Id == id);
}
