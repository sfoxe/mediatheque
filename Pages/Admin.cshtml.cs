using System.Security.Claims;
using Mediatheque;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mediatheque.Pages;

public sealed class AdminModel(LibraryDatabase database) : PageModel
{
    [BindProperty] public BookForm Input { get; set; } = new();
    public IReadOnlyList<Book> Books { get; private set; } = [];
    public string? Message { get; private set; }

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
        if (!database.UpdateBook(ToBook())) return NotFound();
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!IsCurrentUserAdmin()) return StatusCode(StatusCodes.Status403Forbidden);
        if (!database.DeleteBook(id)) return NotFound();
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

    private void LoadBooks() => Books = database.SearchBooks("", "Toutes les catégories", "Tous les supports", "Tous les états");

    private bool ValidateInput()
    {
        Input.Title = Input.Title?.Trim() ?? "";
        Input.Author = Input.Author?.Trim() ?? "";
        Input.Isbn = Input.Isbn?.Trim() ?? "";
        Input.Category = Input.Category?.Trim() ?? "";
        Input.MediaType = Input.MediaType?.Trim() ?? "";
        if (Input.Title.Length is < 1 or > 200) ModelState.AddModelError("Input.Title", "Le titre est obligatoire (200 caractères maximum).");
        if (Input.Author.Length is < 1 or > 160) ModelState.AddModelError("Input.Author", "L’auteur est obligatoire (160 caractères maximum).");
        if (Input.Isbn.Length > 30) ModelState.AddModelError("Input.Isbn", "L’ISBN ne peut pas dépasser 30 caractères.");
        if (Input.Category.Length is < 1 or > 100) ModelState.AddModelError("Input.Category", "La catégorie est obligatoire (100 caractères maximum).");
        if (Input.MediaType.Length is < 1 or > 60) ModelState.AddModelError("Input.MediaType", "Le support est obligatoire (60 caractères maximum).");
        if (Input.PublicationYear is < 0 or > 3000) ModelState.AddModelError("Input.PublicationYear", "Indiquez une année valide.");
        return ModelState.IsValid;
    }

    private Book ToBook() => new()
    {
        Id = Input.Id,
        Title = Input.Title,
        Author = Input.Author,
        Isbn = Input.Isbn,
        Category = Input.Category,
        MediaType = Input.MediaType,
        PublicationYear = Input.PublicationYear,
        IsAvailable = Input.IsAvailable
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
        public bool IsAvailable { get; set; } = true;
    }
}
