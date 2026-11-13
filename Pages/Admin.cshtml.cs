using System.Security.Claims;
using Mediatheque;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mediatheque.Pages;

public sealed class AdminModel(LibraryDatabase database) : PageModel
{
    [BindProperty] public BookForm Input { get; set; } = new();
    public IReadOnlyList<Book> Books { get; private set; } = [];
    public IReadOnlyList<AdminLoanRow> Loans { get; private set; } = [];
    [TempData] public string? Message { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCurrentUserAdmin()) return StatusCode(StatusCodes.Status403Forbidden);
        LoadBooks();
        return Page();
    }

    public IActionResult OnPostAdd()
    {
        if (!IsCurrentUserAdmin()) return StatusCode(StatusCodes.Status403Forbidden);
        if (!ValidateInput()) return ShowPage();
        database.AddBook(ToBook());
        return RedirectToPage();
    }

    public IActionResult OnPostEdit()
    {
        if (!IsCurrentUserAdmin()) return StatusCode(StatusCodes.Status403Forbidden);
        if (Input.Id <= 0 || !ValidateInput()) return ShowPage();
        if (!database.UpdateBook(ToBook()))
        {
            ModelState.AddModelError("", "La quantité totale ne peut pas être inférieure au nombre de copies actuellement empruntées, ou le document n’existe plus.");
            return ShowPage();
        }
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!IsCurrentUserAdmin()) return StatusCode(StatusCodes.Status403Forbidden);
        if (!database.DeleteBook(id))
        {
            if (database.HasLoanHistory(id))
            {
                Message = "Ce document ne peut pas être supprimé car il possède un historique de prêts. Modifiez plutôt son stock à 0 pour le retirer du catalogue.";
                return RedirectToPage();
            }
            return NotFound();
        }
        return RedirectToPage();
    }

    private bool IsCurrentUserAdmin()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is not null && database.IsUserAdmin(email);
    }

    private IActionResult ShowPage()
    {
        LoadBooks();
        return Page();
    }

    private void LoadBooks()
    {
        Books = database.SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états");
        Loans = database.GetAllLoansForAdmin();
    }

    private bool ValidateInput()
    {
        Input.Title = Input.Title?.Trim() ?? "";
        Input.Author = Input.Author?.Trim() ?? "";
        Input.Isbn = Input.Isbn?.Trim() ?? "";
        Input.Category = Input.Category?.Trim() ?? "";
        Input.MediaType = Input.MediaType?.Trim() ?? "";
        Input.CoverUrl = Input.CoverUrl?.Trim() ?? "";
        Input.Synopsis = Input.Synopsis?.Trim() ?? "";
        if (Input.Title.Length is < 1 or > 200) ModelState.AddModelError("Input.Title", "Le titre est obligatoire (200 caractères maximum).");
        if (Input.Author.Length is < 1 or > 160) ModelState.AddModelError("Input.Author", "L’auteur est obligatoire (160 caractères maximum).");
        if (Input.Isbn.Length > 30) ModelState.AddModelError("Input.Isbn", "L’ISBN ne peut pas dépasser 30 caractères.");
        if (Input.Category.Length is < 1 or > 100) ModelState.AddModelError("Input.Category", "La catégorie est obligatoire (100 caractères maximum).");
        if (Input.MediaType.Length is < 1 or > 60) ModelState.AddModelError("Input.MediaType", "Le support est obligatoire (60 caractères maximum).");
        if (Input.TotalCopies is < 0 or > 100000) ModelState.AddModelError("Input.TotalCopies", "La quantité doit être comprise entre 0 et 100 000.");
        if (Input.CoverUrl.Length > 500 || !IsValidCoverUrl(Input.CoverUrl)) ModelState.AddModelError("Input.CoverUrl", "Utilisez une URL HTTPS ou un chemin local commençant par /.");
        if (Input.Synopsis.Length > 5000) ModelState.AddModelError("Input.Synopsis", "Le synopsis ne peut pas dépasser 5 000 caractères.");
        if (Input.PublicationYear is < 0 or > 3000) ModelState.AddModelError("Input.PublicationYear", "Indiquez une année valide.");
        return ModelState.IsValid;
    }

    private static bool IsValidCoverUrl(string value) =>
        value.Length == 0 ||
        value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private Book ToBook() => new()
    {
        Id = Input.Id,
        Title = Input.Title,
        Author = Input.Author,
        Isbn = Input.Isbn,
        Category = Input.Category,
        MediaType = Input.MediaType,
        PublicationYear = Input.PublicationYear,
        TotalCopies = Input.TotalCopies,
        CoverUrl = Input.CoverUrl,
        Synopsis = Input.Synopsis
    };

    public sealed class BookForm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Isbn { get; set; } = "";
        public string Category { get; set; } = "";
        public string MediaType { get; set; } = "";
        public int PublicationYear { get; set; }
        public int TotalCopies { get; set; } = 1;
        public string CoverUrl { get; set; } = "";
        public string Synopsis { get; set; } = "";
    }
}
