using Microsoft.Data.Sqlite;

namespace Mediatheque;

public sealed class LibraryDatabase : IDisposable
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public LibraryDatabase()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediathequeV1");
        Directory.CreateDirectory(directory);
        DatabasePath = Path.Combine(directory, "mediatheque.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true
        }.ToString();
        Initialize();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Books (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Author TEXT NOT NULL,
                Isbn TEXT NOT NULL DEFAULT '',
                Category TEXT NOT NULL,
                MediaType TEXT NOT NULL,
                PublicationYear INTEGER NOT NULL,
                IsAvailable INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL COLLATE NOCASE UNIQUE,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Member' CHECK (Role IN ('Member', 'Admin'))
            );
            CREATE TABLE IF NOT EXISTS MemberSavedBooks (
                UserId INTEGER NOT NULL,
                BookId INTEGER NOT NULL,
                SavedAt TEXT NOT NULL,
                PRIMARY KEY (UserId, BookId),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (BookId) REFERENCES Books(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS MemberSaveHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                BookId INTEGER,
                BookTitle TEXT NOT NULL,
                BookAuthor TEXT NOT NULL,
                Action TEXT NOT NULL CHECK (Action IN ('Sauvegardé', 'Retiré')),
                OccurredAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_Books_Title ON Books(Title);
            CREATE INDEX IF NOT EXISTS IX_Books_Author ON Books(Author);
            CREATE INDEX IF NOT EXISTS IX_Books_Category ON Books(Category);
            CREATE INDEX IF NOT EXISTS IX_MemberSavedBooks_UserId ON MemberSavedBooks(UserId);
            CREATE INDEX IF NOT EXISTS IX_MemberSaveHistory_UserId ON MemberSaveHistory(UserId);
            """;
        command.ExecuteNonQuery();

        command.CommandText = "SELECT COUNT(*) FROM Books;";
        if (Convert.ToInt64(command.ExecuteScalar()) == 0)
            SeedBooks(connection);
    }

    private static void SeedBooks(SqliteConnection connection)
    {
        var books = new (string Title, string Author, string Isbn, string Category, string Type, int Year, bool Available)[]
        {
            ("L'Étranger", "Albert Camus", "9782070360024", "Littérature", "Livre", 1942, true),
            ("Le Petit Prince", "Antoine de Saint-Exupéry", "9782070612758", "Jeunesse", "Livre", 1943, true),
            ("Les Misérables", "Victor Hugo", "9782253096344", "Classique", "Livre", 1862, true),
            ("La Peste", "Albert Camus", "9782070360420", "Littérature", "Livre", 1947, false),
            ("Une brève histoire du temps", "Stephen Hawking", "9782081398914", "Sciences", "Livre", 1988, true),
            ("Sapiens : une brève histoire de l'humanité", "Yuval Noah Harari", "9782226257017", "Histoire", "Livre", 2011, true),
            ("Le Comte de Monte-Cristo", "Alexandre Dumas", "9782253098034", "Classique", "Livre", 1844, true),
            ("L'Œuvre au noir", "Marguerite Yourcenar", "9782070367986", "Littérature", "Livre", 1968, true),
            ("Le Monde sans fin", "Christophe Blain, Jean-Marc Jancovici", "9782205088168", "Bande dessinée", "BD", 2021, true),
            ("Persepolis", "Marjane Satrapi", "9782844142401", "Bande dessinée", "BD", 2000, true),
            ("National Geographic - Océans", "Collectif", "", "Nature", "Magazine", 2023, true),
            ("Les Quatre Saisons", "Antonio Vivaldi", "", "Musique classique", "CD", 1725, true)
        };

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO Books (Title, Author, Isbn, Category, MediaType, PublicationYear, IsAvailable) VALUES ($title, $author, $isbn, $category, $type, $year, $available);";
        foreach (var book in books)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$title", book.Title);
            command.Parameters.AddWithValue("$author", book.Author);
            command.Parameters.AddWithValue("$isbn", book.Isbn);
            command.Parameters.AddWithValue("$category", book.Category);
            command.Parameters.AddWithValue("$type", book.Type);
            command.Parameters.AddWithValue("$year", book.Year);
            command.Parameters.AddWithValue("$available", book.Available ? 1 : 0);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public List<Book> SearchBooks(string query, string category, string mediaType, string availability)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Title, Author, Isbn, Category, MediaType, PublicationYear, IsAvailable
            FROM Books
            WHERE ($query = '' OR Title LIKE $like OR Author LIKE $like OR Isbn LIKE $like OR Category LIKE $like OR MediaType LIKE $like)
              AND ($category = '' OR Category = $category)
              AND ($type = '' OR MediaType = $type)
              AND ($availability = '' OR ($availability = 'Disponible' AND IsAvailable = 1) OR ($availability = 'Indisponible' AND IsAvailable = 0))
            ORDER BY Title COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$query", query.Trim());
        command.Parameters.AddWithValue("$like", $"%{query.Trim()}%");
        command.Parameters.AddWithValue("$category", category == "Toutes les catégories" ? "" : category);
        command.Parameters.AddWithValue("$type", mediaType == "Tous les supports" ? "" : mediaType);
        command.Parameters.AddWithValue("$availability", availability == "Tous les états" ? "" : availability);
        using var reader = command.ExecuteReader();
        var results = new List<Book>();
        while (reader.Read())
        {
            results.Add(new Book
            {
                Id = reader.GetInt32(0), Title = reader.GetString(1), Author = reader.GetString(2),
                Isbn = reader.GetString(3), Category = reader.GetString(4), MediaType = reader.GetString(5),
                PublicationYear = reader.GetInt32(6), IsAvailable = reader.GetInt32(7) == 1
            });
        }
        return results;
    }

    public List<string> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Category FROM Books ORDER BY Category COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var values = new List<string> { "Toutes les catégories" };
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }

    public List<string> GetMediaTypes()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT MediaType FROM Books ORDER BY MediaType COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var values = new List<string> { "Tous les supports" };
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }

    public List<SavedBookRow> GetSavedBooks(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.Id, b.Title, b.Author, b.Category, b.MediaType, b.PublicationYear, s.SavedAt
            FROM MemberSavedBooks s JOIN Books b ON b.Id = s.BookId
            WHERE s.UserId = $userId
            ORDER BY s.SavedAt DESC;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        var results = new List<SavedBookRow>();
        while (reader.Read())
        {
            results.Add(new SavedBookRow
            {
                Id = reader.GetInt32(0), Title = reader.GetString(1), Author = reader.GetString(2),
                Category = reader.GetString(3), MediaType = reader.GetString(4), PublicationYear = reader.GetInt32(5),
                SavedAt = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }
        return results;
    }

    public List<HistoryRow> GetHistory(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Action, BookTitle, BookAuthor, OccurredAt FROM MemberSaveHistory WHERE UserId = $userId ORDER BY OccurredAt DESC, Id DESC;";
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        var results = new List<HistoryRow>();
        while (reader.Read())
        {
            results.Add(new HistoryRow
            {
                Action = reader.GetString(0), Title = reader.GetString(1), Author = reader.GetString(2),
                OccurredAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }
        return results;
    }

    public int GetBookCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Books;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int GetAvailableBookCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Books WHERE IsAvailable = 1;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int GetSavedBookCount(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM MemberSavedBooks WHERE UserId = $userId;";
        command.Parameters.AddWithValue("$userId", userId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool IsSaved(int userId, int bookId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM MemberSavedBooks WHERE UserId = $userId AND BookId = $bookId);";
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$bookId", bookId);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public void ToggleSavedBook(int userId, Book book)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$id", book.Id);
        command.Parameters.AddWithValue("$title", book.Title);
        command.Parameters.AddWithValue("$author", book.Author);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM MemberSavedBooks WHERE UserId = $userId AND BookId = $id);";
        var alreadySaved = Convert.ToInt32(command.ExecuteScalar()) == 1;
        command.CommandText = alreadySaved
            ? "DELETE FROM MemberSavedBooks WHERE UserId = $userId AND BookId = $id; INSERT INTO MemberSaveHistory (UserId, BookId, BookTitle, BookAuthor, Action, OccurredAt) VALUES ($userId, $id, $title, $author, 'Retiré', $now);"
            : "INSERT INTO MemberSavedBooks (UserId, BookId, SavedAt) VALUES ($userId, $id, $now); INSERT INTO MemberSaveHistory (UserId, BookId, BookTitle, BookAuthor, Action, OccurredAt) VALUES ($userId, $id, $title, $author, 'Sauvegardé', $now);";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public int? CreateMember(string email, string displayName, string passwordHash)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Users (Email, DisplayName, PasswordHash, Role) VALUES ($email, $name, $password, 'Member'); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$name", displayName);
        command.Parameters.AddWithValue("$password", passwordHash);
        try
        {
            return Convert.ToInt32(command.ExecuteScalar());
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return null;
        }
    }

    public MemberAccount? GetMemberByEmail(string email)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Email, DisplayName, PasswordHash, Role FROM Users WHERE Email = $email COLLATE NOCASE;";
        command.Parameters.AddWithValue("$email", email);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new MemberAccount(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4))
            : null;
    }

    public bool IsUserAdmin(string email)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Users WHERE Email = $email COLLATE NOCASE AND Role = 'Admin');";
        command.Parameters.AddWithValue("$email", email);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public void AddBook(Book book)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Books (Title, Author, Isbn, Category, MediaType, PublicationYear, IsAvailable) VALUES ($title, $author, $isbn, $category, $type, $year, $available);";
        AddBookParameters(command, book);
        command.ExecuteNonQuery();
    }

    public bool UpdateBook(Book book)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Books SET Title = $title, Author = $author, Isbn = $isbn, Category = $category, MediaType = $type, PublicationYear = $year, IsAvailable = $available WHERE Id = $id;";
        AddBookParameters(command, book);
        command.Parameters.AddWithValue("$id", book.Id);
        return command.ExecuteNonQuery() == 1;
    }

    public bool DeleteBook(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Books WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() == 1;
    }

    private static void AddBookParameters(SqliteCommand command, Book book)
    {
        command.Parameters.AddWithValue("$title", book.Title);
        command.Parameters.AddWithValue("$author", book.Author);
        command.Parameters.AddWithValue("$isbn", book.Isbn);
        command.Parameters.AddWithValue("$category", book.Category);
        command.Parameters.AddWithValue("$type", book.MediaType);
        command.Parameters.AddWithValue("$year", book.PublicationYear);
        command.Parameters.AddWithValue("$available", book.IsAvailable ? 1 : 0);
    }

    public void Dispose() { }
}
