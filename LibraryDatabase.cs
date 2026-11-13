using Microsoft.Data.Sqlite;

namespace Mediatheque;

public sealed class LibraryDatabase : IDisposable
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public LibraryDatabase()
    {
        var configuredPath = Environment.GetEnvironmentVariable("MEDIATHEQUE_DATABASE_PATH");
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediathequeV1");
        DatabasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(directory, "mediatheque.db")
            : Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
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
                IsAvailable INTEGER NOT NULL DEFAULT 1,
                TotalCopies INTEGER NOT NULL DEFAULT 1 CHECK (TotalCopies >= 0),
                AvailableCopies INTEGER NOT NULL DEFAULT 1 CHECK (AvailableCopies >= 0 AND AvailableCopies <= TotalCopies),
                CoverUrl TEXT NOT NULL DEFAULT '',
                Synopsis TEXT NOT NULL DEFAULT ''
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
            CREATE TABLE IF NOT EXISTS Loans (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                BookId INTEGER NOT NULL,
                BorrowedAt TEXT NOT NULL,
                DueAt TEXT NOT NULL,
                ExtensionUsed INTEGER NOT NULL DEFAULT 0 CHECK (ExtensionUsed IN (0, 1)),
                ReturnedAt TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (BookId) REFERENCES Books(Id) ON DELETE RESTRICT
            );
            DROP INDEX IF EXISTS UX_Loans_ActiveBook;
            CREATE INDEX IF NOT EXISTS IX_Loans_ActiveBook ON Loans(BookId) WHERE ReturnedAt IS NULL;
            CREATE INDEX IF NOT EXISTS IX_Loans_User_Active ON Loans(UserId, DueAt) WHERE ReturnedAt IS NULL;
            CREATE INDEX IF NOT EXISTS IX_Books_Title ON Books(Title);
            CREATE INDEX IF NOT EXISTS IX_Books_Author ON Books(Author);
            CREATE INDEX IF NOT EXISTS IX_Books_Category ON Books(Category);
            CREATE INDEX IF NOT EXISTS IX_MemberSavedBooks_UserId ON MemberSavedBooks(UserId);
            CREATE INDEX IF NOT EXISTS IX_MemberSaveHistory_UserId ON MemberSaveHistory(UserId);
            """;
        command.ExecuteNonQuery();
        EnsureBookColumn(connection, "CoverUrl");
        EnsureBookColumn(connection, "Synopsis");
        var totalCopiesAdded = EnsureIntegerBookColumn(connection, "TotalCopies");
        var availableCopiesAdded = EnsureIntegerBookColumn(connection, "AvailableCopies");
        if (totalCopiesAdded || availableCopiesAdded)
        {
            using var migration = connection.CreateCommand();
            migration.CommandText = "UPDATE Books SET TotalCopies = 1, AvailableCopies = IsAvailable;";
            migration.ExecuteNonQuery();
        }

        command.CommandText = "SELECT COUNT(*) FROM Books;";
        if (Convert.ToInt64(command.ExecuteScalar()) == 0)
            SeedBooks(connection);
    }

    private static bool EnsureIntegerBookColumn(SqliteConnection connection, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Books);";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1) == columnName) return false;
            }
        }
        command.CommandText = $"ALTER TABLE Books ADD COLUMN {columnName} INTEGER NOT NULL DEFAULT 1 CHECK ({columnName} >= 0);";
        command.ExecuteNonQuery();
        return true;
    }

    private static void EnsureBookColumn(SqliteConnection connection, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Books);";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1) == columnName) return;
            }
        }
        command.CommandText = $"ALTER TABLE Books ADD COLUMN {columnName} TEXT NOT NULL DEFAULT '';";
        command.ExecuteNonQuery();
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
        command.CommandText = "INSERT INTO Books (Title, Author, Isbn, Category, MediaType, PublicationYear, IsAvailable, TotalCopies, AvailableCopies) VALUES ($title, $author, $isbn, $category, $type, $year, $available, 1, $available);";
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
            SELECT Id, Title, Author, Isbn, Category, MediaType, PublicationYear, TotalCopies, AvailableCopies, CoverUrl, Synopsis
            FROM Books
            WHERE ($query = '' OR Title LIKE $like OR Author LIKE $like OR Isbn LIKE $like OR Category LIKE $like OR MediaType LIKE $like)
              AND ($category = '' OR Category = $category)
              AND ($type = '' OR MediaType = $type)
              AND ($availability = '' OR ($availability = 'Disponible' AND AvailableCopies > 0) OR ($availability = 'Indisponible' AND AvailableCopies = 0))
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
                PublicationYear = reader.GetInt32(6), TotalCopies = reader.GetInt32(7),
                AvailableCopies = reader.GetInt32(8), CoverUrl = reader.GetString(9), Synopsis = reader.GetString(10)
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

    public List<LoanRow> GetActiveLoans(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.Id, b.Title, b.Author, b.Category, b.MediaType, b.CoverUrl, b.Synopsis,
                   l.BorrowedAt, l.DueAt, l.ExtensionUsed
            FROM Loans l JOIN Books b ON b.Id = l.BookId
            WHERE l.UserId = $userId AND l.ReturnedAt IS NULL
            ORDER BY l.DueAt;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        var results = new List<LoanRow>();
        while (reader.Read())
        {
            results.Add(new LoanRow
            {
                Id = reader.GetInt32(0), Title = reader.GetString(1), Author = reader.GetString(2),
                Category = reader.GetString(3), MediaType = reader.GetString(4), CoverUrl = reader.GetString(5),
                Synopsis = reader.GetString(6),
                BorrowedAt = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DueAt = DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ExtensionUsed = reader.GetInt32(9) == 1
            });
        }
        return results;
    }

    public List<AdminLoanRow> GetAllLoansForAdmin()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.Id, u.Email, b.Title, l.BorrowedAt, l.DueAt, l.ReturnedAt, l.ExtensionUsed
            FROM Loans l
            JOIN Users u ON u.Id = l.UserId
            JOIN Books b ON b.Id = l.BookId
            ORDER BY CASE WHEN l.ReturnedAt IS NULL AND l.DueAt < $now THEN 0 WHEN l.ReturnedAt IS NULL THEN 1 ELSE 2 END,
                     l.DueAt, l.BorrowedAt DESC;
            """;
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        using var reader = command.ExecuteReader();
        var results = new List<AdminLoanRow>();
        while (reader.Read())
        {
            var dueAt = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind);
            DateTime? returnedAt = reader.IsDBNull(5)
                ? null
                : DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind);
            results.Add(new AdminLoanRow
            {
                Id = reader.GetInt32(0), MemberEmail = reader.GetString(1), BookTitle = reader.GetString(2),
                BorrowedAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DueAt = dueAt, ReturnedAt = returnedAt,
                IsLate = returnedAt is null && dueAt < DateTime.UtcNow,
                ExtensionUsed = reader.GetInt32(6) == 1
            });
        }
        return results;
    }

    public bool BorrowBook(int userId, int bookId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Books SET AvailableCopies = AvailableCopies - 1, IsAvailable = CASE WHEN AvailableCopies - 1 > 0 THEN 1 ELSE 0 END WHERE Id = $bookId AND AvailableCopies > 0;";
        command.Parameters.AddWithValue("$bookId", bookId);
        if (command.ExecuteNonQuery() != 1) return false;

        var borrowedAt = DateTime.UtcNow;
        command.Parameters.Clear();
        command.CommandText = "INSERT INTO Loans (UserId, BookId, BorrowedAt, DueAt) VALUES ($userId, $bookId, $borrowedAt, $dueAt);";
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$bookId", bookId);
        command.Parameters.AddWithValue("$borrowedAt", borrowedAt.ToString("O"));
        command.Parameters.AddWithValue("$dueAt", borrowedAt.AddDays(14).ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }

    public bool ExtendLoan(int userId, int loanId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT DueAt FROM Loans WHERE Id = $loanId AND UserId = $userId AND ReturnedAt IS NULL AND ExtensionUsed = 0;";
        command.Parameters.AddWithValue("$loanId", loanId);
        command.Parameters.AddWithValue("$userId", userId);
        var dueAtValue = command.ExecuteScalar();
        if (dueAtValue is null || dueAtValue is DBNull) return false;
        var dueAt = DateTime.Parse(Convert.ToString(dueAtValue)!, null, System.Globalization.DateTimeStyles.RoundtripKind);

        command.CommandText = "UPDATE Loans SET DueAt = $dueAt, ExtensionUsed = 1 WHERE Id = $loanId AND UserId = $userId AND ReturnedAt IS NULL AND ExtensionUsed = 0;";
        command.Parameters.AddWithValue("$dueAt", dueAt.AddDays(7).ToString("O"));
        if (command.ExecuteNonQuery() != 1) return false;
        transaction.Commit();
        return true;
    }

    public bool ReturnLoan(int userId, int loanId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT BookId FROM Loans WHERE Id = $loanId AND UserId = $userId AND ReturnedAt IS NULL;";
        command.Parameters.AddWithValue("$loanId", loanId);
        command.Parameters.AddWithValue("$userId", userId);
        var bookIdValue = command.ExecuteScalar();
        if (bookIdValue is null || bookIdValue is DBNull) return false;
        var bookId = Convert.ToInt32(bookIdValue);

        command.CommandText = "UPDATE Loans SET ReturnedAt = $returnedAt WHERE Id = $loanId AND UserId = $userId AND ReturnedAt IS NULL;";
        command.Parameters.AddWithValue("$returnedAt", DateTime.UtcNow.ToString("O"));
        if (command.ExecuteNonQuery() != 1) return false;
        command.CommandText = "UPDATE Books SET AvailableCopies = MIN(TotalCopies, AvailableCopies + 1), IsAvailable = CASE WHEN MIN(TotalCopies, AvailableCopies + 1) > 0 THEN 1 ELSE 0 END WHERE Id = $bookId;";
        command.Parameters.AddWithValue("$bookId", bookId);
        command.ExecuteNonQuery();
        transaction.Commit();
        return true;
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
        command.CommandText = "SELECT COALESCE(SUM(AvailableCopies), 0) FROM Books;";
        return Convert.ToInt32(command.ExecuteScalar());
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
        command.CommandText = "INSERT INTO Books (Title, Author, Isbn, Category, MediaType, PublicationYear, IsAvailable, TotalCopies, AvailableCopies, CoverUrl, Synopsis) VALUES ($title, $author, $isbn, $category, $type, $year, CASE WHEN $totalCopies > 0 THEN 1 ELSE 0 END, $totalCopies, $totalCopies, $coverUrl, $synopsis);";
        AddBookParameters(command, book);
        command.ExecuteNonQuery();
    }

    public bool UpdateBook(Book book)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Books SET Title = $title, Author = $author, Isbn = $isbn, Category = $category, MediaType = $type, PublicationYear = $year, TotalCopies = $totalCopies, AvailableCopies = $totalCopies - (SELECT COUNT(*) FROM Loans WHERE Loans.BookId = Books.Id AND Loans.ReturnedAt IS NULL), IsAvailable = CASE WHEN $totalCopies - (SELECT COUNT(*) FROM Loans WHERE Loans.BookId = Books.Id AND Loans.ReturnedAt IS NULL) > 0 THEN 1 ELSE 0 END, CoverUrl = $coverUrl, Synopsis = $synopsis WHERE Id = $id AND $totalCopies >= (SELECT COUNT(*) FROM Loans WHERE Loans.BookId = Books.Id AND Loans.ReturnedAt IS NULL);";
        AddBookParameters(command, book);
        command.Parameters.AddWithValue("$id", book.Id);
        return command.ExecuteNonQuery() == 1;
    }

    public bool HasLoanHistory(int bookId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Loans WHERE BookId = $bookId);";
        command.Parameters.AddWithValue("$bookId", bookId);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public bool DeleteBook(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Books WHERE Id = $id AND NOT EXISTS (SELECT 1 FROM Loans WHERE BookId = $id);";
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
        command.Parameters.AddWithValue("$totalCopies", book.TotalCopies);
        command.Parameters.AddWithValue("$coverUrl", book.CoverUrl);
        command.Parameters.AddWithValue("$synopsis", book.Synopsis);
    }

    public void Dispose() { }
}
