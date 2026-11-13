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
    public bool IsAvailable { get; init; }
}

public sealed class SavedBookRow
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Category { get; init; } = "";
    public string MediaType { get; init; } = "";
    public int PublicationYear { get; init; }
    public DateTime SavedAt { get; init; }
}

public sealed class HistoryRow
{
    public string Action { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public DateTime OccurredAt { get; init; }
}
