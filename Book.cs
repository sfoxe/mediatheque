namespace Mediatheque;

public sealed class Book
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Isbn { get; init; } = "";
    public string Category { get; init; } = "";
    public string MediaType { get; init; } = "";
    public int PublicationYear { get; init; }
    public int TotalCopies { get; init; }
    public int AvailableCopies { get; init; }
    public bool IsAvailable => AvailableCopies > 0;
    public string CoverUrl { get; init; } = "";
    public string Synopsis { get; init; } = "";
}

public sealed class LoanRow
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Category { get; init; } = "";
    public string MediaType { get; init; } = "";
    public string CoverUrl { get; init; } = "";
    public string Synopsis { get; init; } = "";
    public DateTime BorrowedAt { get; init; }
    public DateTime DueAt { get; init; }
    public bool ExtensionUsed { get; init; }
}

public sealed class AdminLoanRow
{
    public int Id { get; init; }
    public string MemberEmail { get; init; } = "";
    public string BookTitle { get; init; } = "";
    public DateTime BorrowedAt { get; init; }
    public DateTime DueAt { get; init; }
    public DateTime? ReturnedAt { get; init; }
    public bool IsLate { get; init; }
    public bool ExtensionUsed { get; init; }
}
