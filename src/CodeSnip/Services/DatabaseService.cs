using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SnippetView;
using Dapper;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeSnip.Services;

/// <summary>
/// Handles all interactions with the SQLite database, including creating the database,
/// seeding initial data, and performing CRUD operations for snippets, languages, and categories.
/// </summary>
public class DatabaseService
{
    private readonly string _dbPath = "snippets.sqlite";
    private static readonly Dictionary<int, List<InitialSnippet>> initialSnippets = [];

    /// <summary>
    /// Creates and returns a new SQLite database connection.
    /// </summary>
    /// <returns>An open <see cref="IDbConnection"/> to the SQLite database.</returns>
    public IDbConnection CreateConnection()
    {
        return new SQLiteConnection($"Data Source={_dbPath};foreign keys=true;");
    }

    /// <summary>
    /// Initializes the database if the file does not exist. This method creates the database schema
    /// and seeds it with default languages, categories, and a few example snippets.
    /// </summary>
    /// <param name="dbSchema">The DDL and DML script to execute for database creation and seeding.</param>
    public void InitializeDatabaseIfNeeded(string dbSchema = ddl)
    {
        if (!File.Exists(_dbPath))
        {
            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // 1. Create tables + insert languages + categories
            connection.Execute(dbSchema, transaction: transaction);

            PopulateInitialSnippets();

            // 2. Insert initial snippets

            foreach (var kvp in initialSnippets)
            {
                int langId = kvp.Key;
                var snippets = kvp.Value;

                var categoryId = connection.ExecuteScalar<int>(
                    "SELECT ID FROM Categories WHERE LanguageId = @langId AND Name = 'Basic Syntax'",
                    new { langId }, transaction);

                foreach (var snip in snippets)
                {
                    connection.Execute(@"
                    INSERT INTO Snippets (CategoryId, Title, Code, Description, Tag) VALUES (@CategoryId, @Title, @Code, @Description, @Tag)",
                        new
                        {
                            CategoryId = categoryId,
                            snip.Title,
                            snip.Code,
                            snip.Description,
                            snip.Tag
                        }, transaction);
                }
            }

            transaction.Commit();
        }

        // 2. ALWAYS run migrations (for both new and existing DB)
        MigrateDatabase();
    }

    /// <summary>
    /// Retrieves all languages, their categories, and associated snippets from the database.
    /// The snippet code is not loaded initially to support lazy loading.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="Language"/> objects, structured hierarchically.</returns>
    public IEnumerable<Language> GetSnippets()
    {
        try
        {
            using var conn = CreateConnection();

            var sql = @"
 SELECT 
    L.ID AS LanguageID,
    L.Code AS LanguageCode,
    L.Name AS LanguageName,
    L.SupportsXshd AS LanguageSupportsXshd,
    L.SupportsTextMate AS LanguageSupportsTextMate,

    C.ID AS CategoryID,
    C.LanguageId AS CategoryLanguageId,
    C.Name AS CategoryName,

    S.ID AS SnippetID,
    S.CategoryId AS SnippetCategoryId,
    S.Title AS SnippetTitle,
    -- S.Code AS SnippetCode, -- Lazy load this
    S.Description AS SnippetDescription,
    S.Tag AS SnippetTag,
    S.DateCreated AS SnippetDateCreated,
    S.DateModified AS SnippetDateModified
FROM Languages L
LEFT JOIN Categories C ON C.LanguageId = L.ID
LEFT JOIN Snippets S ON S.CategoryId = C.ID
ORDER BY L.Name, C.Name, S.Title";

            var lookup = new Dictionary<int, Language>();
            //var result = conn.Query(sql).ToList();
            var result = conn.Query<dynamic>(sql).ToList();

            foreach (var row in result)
            {
                // Find or create a language
                int langId = (int)row.LanguageID;
                if (!lookup.TryGetValue(langId, out var language))
                {
                    language = new Language
                    {
                        Id = langId,
                        Code = (string)row.LanguageCode,
                        Name = (string)row.LanguageName,
                        SupportsXshd = (int)row.LanguageSupportsXshd == 1,
                        SupportsTextMate = (int)row.LanguageSupportsTextMate == 1,
                        Categories = new ObservableCollection<Category>() // Collection initialization
                    };
                    lookup.Add(langId, language);
                }
                // Find or create a category
                if (row.CategoryID != null)
                {
                    int catId = (int)row.CategoryID;
                    var category = language.Categories.FirstOrDefault(c => c.Id == catId);
                    if (category == null)
                    {
                        category = new Category
                        {
                            Id = catId,
                            LanguageId = (int)row.CategoryLanguageId,
                            Name = (string)row.CategoryName,
                            Language = language,
                            Snippets = new ObservableCollection<Snippet>() // Collection initialization
                        };
                        language.Categories.Add(category);
                    }
                    // Find or create a snippet
                    if (row.SnippetID != null)
                    {
                        var snippet = new Snippet
                        {
                            Id = (int)row.SnippetID,
                            CategoryId = (int)row.SnippetCategoryId,
                            Title = (string)row.SnippetTitle,
                            //Code = (string)row.SnippetCode,
                            Code = string.Empty, // Initially empty
                            Description = (string)row.SnippetDescription,
                            Tag = (string)row.SnippetTag,
                            DateCreated = (string)row.SnippetDateCreated,
                            DateModified = (string)row.SnippetDateModified,
                            Category = category
                        };
                        category.Snippets.Add(snippet);
                    }
                }
            }
            return lookup.Values;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Failed to load snippets from database: {ex}");
            return Enumerable.Empty<Language>(); // Return an empty list on failure
        }
    }

    /// <summary>
    /// Lazily loads the code for a specific snippet from the database.
    /// </summary>
    /// <param name="snippetId">The ID of the snippet to retrieve code for.</param>
    /// <returns>The code content of the snippet as a string.</returns>
    public string GetSnippetCode(int snippetId)
    {
        using var conn = CreateConnection();
        return conn.ExecuteScalar<string>(
            "SELECT Code FROM Snippets WHERE ID = @Id",
            new { Id = snippetId }) ?? string.Empty;
    }

    /// <summary>
    /// Saves a snippet to the database. Performs an INSERT for a new snippet (Id = 0)
    /// or an UPDATE for an existing one.
    /// </summary>
    /// <param name="snippet">The <see cref="Snippet"/> object to save.</param>
    /// <returns>The saved <see cref="Snippet"/> object, updated with its new ID if it was an insert.</returns>
    public Snippet SaveSnippet(Snippet snippet)
    {
        using var conn = CreateConnection();
        if (snippet.Id == 0)
        {
            // INSERT
            var id = conn.ExecuteScalar<int>(
                "INSERT INTO Snippets (Title, Code, Description, Tag, CategoryId) VALUES (@Title, @Code, @Description, @Tag, @CategoryId); SELECT last_insert_rowid();",
                snippet);

            snippet.Id = id;
        }
        else
        {
            // UPDATE
            conn.Execute(
                "UPDATE Snippets SET Title = @Title, Code = @Code, Description = @Description, Tag = @Tag, CategoryId = @CategoryId WHERE Id = @Id",
                snippet);
        }
        return snippet;
    }

    /// <summary>
    /// Updates only the code content of a specific snippet in the database.
    /// </summary>
    /// <param name="id">The ID of the snippet to update.</param>
    /// <param name="code">The new code content.</param>
    public void UpdateSnippetCode(int id, string code)
    {
        using var conn = CreateConnection();
        conn.Execute(
            "UPDATE Snippets SET Code = @Code WHERE Id = @Id",
            new { Id = id, Code = code });
    }

    /// <summary>
    /// Deletes a snippet from the database.
    /// </summary>
    /// <param name="id">The ID of the snippet to delete.</param>
    public void DeleteSnippet(int id)
    {
        using var conn = CreateConnection();
        conn.Execute("DELETE FROM Snippets WHERE Id = @Id", new { Id = id });
    }

    /// <summary>
    /// Retrieves all languages and their associated categories from the database, without loading snippets.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="Language"/> objects, each containing its list of <see cref="Category"/> objects.</returns>
    public IEnumerable<Language> GetLanguagesWithCategories()
    {
        using var conn = CreateConnection();

        var sql = @"
        SELECT 
            L.ID AS LanguageID,
            L.Code AS LanguageCode,
            L.Name AS LanguageName,
            L.SupportsXshd AS LanguageSupportsXshd,
            L.SupportsTextMate AS LanguageSupportsTextMate,
            C.ID AS CategoryID,
            C.Name AS CategoryName,
            C.LanguageId AS CategoryLanguageId
        FROM Languages L
        LEFT JOIN Categories C ON C.LanguageId = L.ID
        ORDER BY L.Name, C.Name";

        var lookup = new Dictionary<int, Language>();

        var result = conn.Query<dynamic>(sql);

        foreach (var row in result)
        {
            int langId = (int)row.LanguageID;

            if (!lookup.TryGetValue(langId, out var language))
            {
                language = new Language
                {
                    Id = langId,
                    Code = (string)row.LanguageCode,
                    Name = (string)row.LanguageName,
                    SupportsXshd = (int)row.LanguageSupportsXshd == 1,
                    SupportsTextMate = (int)row.LanguageSupportsTextMate == 1,
                    Categories = new ObservableCollection<Category>()
                };
                lookup.Add(langId, language);
            }

            if (row.CategoryID != null)
            {
                var category = new Category
                {
                    Id = (int)row.CategoryID,
                    Name = (string)row.CategoryName,
                    LanguageId = (int)row.CategoryLanguageId,
                    Language = language
                };
                language.Categories.Add(category);
            }
        }

        return lookup.Values;
    }

    /// <summary>
    /// Saves a language to the database. Performs an INSERT for a new language (Id = 0)
    /// or an UPDATE for an existing one.
    /// </summary>
    /// <param name="language">The <see cref="Language"/> object to save.</param>
    /// <returns>The saved <see cref="Language"/> object, updated with its new ID if it was an insert.</returns>
    public Language SaveLanguage(Language language)
    {
        using var conn = CreateConnection();
        if (language.Id == 0)
        {
            var id = conn.ExecuteScalar<int>(
                "INSERT INTO Languages (Code, Name, SupportsXshd, SupportsTextMate) VALUES (@Code, @Name, @SupportsXshd, @SupportsTextMate); SELECT last_insert_rowid();",
                language);
            language.Id = id;
        }
        else
        {
            conn.Execute(
                "UPDATE Languages SET Code = @Code, Name = @Name, SupportsXshd = @SupportsXshd, SupportsTextMate = @SupportsTextMate WHERE ID = @Id",
                language);
        }
        return language;
    }

    /// <summary>
    /// Saves a category to the database. Performs an INSERT for a new category (Id = 0)
    /// or an UPDATE for an existing one.
    /// </summary>
    /// <param name="category">The <see cref="Category"/> object to save.</param>
    /// <returns>The saved <see cref="Category"/> object, updated with its new ID if it was an insert.</returns>
    public Category SaveCategory(Category category)
    {
        using var conn = CreateConnection();
        if (category.Id == 0)
        {
            var id = conn.ExecuteScalar<int>("INSERT INTO Categories (LanguageId, Name) VALUES (@LanguageId, @Name); SELECT last_insert_rowid();", category);
            category.Id = id;
        }
        else
        {
            conn.Execute("UPDATE Categories SET Name = @Name WHERE ID = @Id AND LanguageId = @LanguageId", category);
        }
        return category;
    }

    /// <summary>
    /// Deletes a language from the database. Note: This may fail if the language is referenced
    /// by categories, due to foreign key constraints.
    /// </summary>
    /// <param name="id">The ID of the language to delete.</param>
    public void DeleteLanguage(int id)
    {
        using var conn = CreateConnection();
        conn.Execute("DELETE FROM Languages WHERE ID = @Id", new { Id = id });
    }

    /// <summary>
    /// Deletes a category from the database. Note: This may fail if the category is referenced
    /// by snippets, due to foreign key constraints.
    /// </summary>
    /// <param name="id">The ID of the category to delete.</param>
    public void DeleteCategory(int id)
    {
        using var conn = CreateConnection();
        conn.Execute("DELETE FROM Categories WHERE ID = @Id", new { Id = id });
    }

    /// <summary>
    /// Counts the number of snippets within a specific category.
    /// </summary>
    /// <param name="categoryId">The ID of the category.</param>
    /// <returns>The total number of snippets in the category.</returns>
    public int CountSnippetsInCategory(int categoryId)
    {
        using var conn = CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Snippets WHERE CategoryId = @CategoryId", new { CategoryId = categoryId });
    }

    /// <summary>
    /// Counts the total number of snippets across all categories for a specific language.
    /// </summary>
    /// <param name="languageId">The ID of the language.</param>
    /// <returns>The total number of snippets in the language.</returns>
    public int CountSnippetsInLanguage(int languageId)
    {
        using var conn = CreateConnection();
        return conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) 
                FROM Snippets s
                JOIN Categories c ON s.CategoryId = c.ID
                WHERE c.LanguageId = @LanguageId", new { LanguageId = languageId });
    }

    /// <summary>
    /// Deletes a category and all of its associated snippets from the database.
    /// This method bypasses the 'ON DELETE RESTRICT' foreign key constraint by first
    /// deleting the child snippets and then the parent category within a transaction.
    /// </summary>
    /// <param name="categoryId">The ID of the category to delete.</param>
    public void ForceDeleteCategory(int categoryId)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            // Delete all snippete in category
            conn.Execute("DELETE FROM Snippets WHERE CategoryId = @CategoryId", new { CategoryId = categoryId }, transaction);
            // Delete the category itself
            conn.Execute("DELETE FROM Categories WHERE ID = @Id", new { Id = categoryId }, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Deletes a language, all of its categories, and all snippets contained within those categories.
    /// This method bypasses the 'ON DELETE RESTRICT' foreign key constraint by performing a
    /// hierarchical deletion (snippets -> categories -> language) within a transaction.
    /// </summary>
    /// <param name="languageId">The ID of the language to delete.</param>
    public void ForceDeleteLanguage(int languageId)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            // Delete all snippets in categories of this language
            conn.Execute("DELETE FROM Snippets WHERE CategoryId IN (SELECT ID FROM Categories WHERE LanguageId = @LanguageId)", new { LanguageId = languageId }, transaction);
            //  Delete all categories of this language
            conn.Execute("DELETE FROM Categories WHERE LanguageId = @LanguageId", new { LanguageId = languageId }, transaction);
            // Delete the language itself
            conn.Execute("DELETE FROM Languages WHERE ID = @Id", new { Id = languageId }, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Runs a database integrity check.
    /// </summary>
    /// <returns>A task that resolves to <c>true</c> if the check passes, otherwise <c>false</c>.</returns>
    public async Task<bool> RunIntegrityCheckAsync()
    {
        try
        {
            using var conn = CreateConnection();
            var result = await conn.QueryAsync<string>("PRAGMA integrity_check;");
            return result.Count() == 1 && result.First() == "ok";
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Integrity Check Failed", ex.Message, Icon.Error);
            return false;
        }
    }

    /// <summary>
    /// Runs the VACUUM command to rebuild the database file, repacking it into a minimal amount of disk space.
    /// </summary>
    /// <returns>A task that resolves to <c>true</c> if the operation succeeds, otherwise <c>false</c>.</returns>
    public async Task<bool> RunVacuumAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync("VACUUM;");
            return true;
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Vacuum Failed", ex.Message, Icon.Error);
            return false;
        }
    }

    /// <summary>
    /// Runs the REINDEX command to rebuild all indices in the database.
    /// </summary>
    /// <returns>A task that resolves to <c>true</c> if the operation succeeds, otherwise <c>false</c>.</returns>
    public async Task<bool> RunReindexAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync("REINDEX;");
            return true;
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Reindex Failed", ex.Message, Icon.Error);
            return false;
        }
    }

    /// <summary>
    /// Checks the database for fragmentation to determine if a VACUUM operation is needed.
    /// </summary>
    /// <returns>A tuple containing a boolean indicating if vacuum is needed (fragmentation >= 25%)
    /// and the calculated fragmentation percentage.</returns>
    public async Task<(bool NeedVacuum, double FragmentationPercent)> IsVacuumNeeded()
    {
        try
        {
            using var conn = CreateConnection();

            var freelistCount = await conn.ExecuteScalarAsync<long>("PRAGMA freelist_count;");
            var pageCount = await conn.ExecuteScalarAsync<long>("PRAGMA page_count;");

            if (pageCount == 0)
                return (false, 0);

            double fragmentationPercent = (double)freelistCount / pageCount;
            bool needVacuum = fragmentationPercent >= 0.25;

            return (needVacuum, fragmentationPercent);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Vacuum Check Failed", ex.Message, Icon.Error);
            return (false, 0);
        }
    }

    public void MigrateDatabase()
    {
        using var conn = CreateConnection();

        // 1) Create Meta table if it doesn't exist and get current DB version
        bool metaExists = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Meta'") > 0;

        if (!metaExists)
        {
            conn.Execute("CREATE TABLE Meta (Key TEXT PRIMARY KEY, Value TEXT)");
            conn.Execute("INSERT INTO Meta (Key, Value) VALUES ('DbVersion', '0')");
        }

        int version = conn.ExecuteScalar<int>("SELECT Value FROM Meta WHERE Key='DbVersion'");

        // 2) MIGRATION 1
        if (version < 1)
        {
            HighlightingService.InitializeTextMateExtensions(); // must be initialized before HighlightingService.SupportsTextMate is called
            conn.Execute("ALTER TABLE Languages ADD COLUMN SupportsXshd INTEGER NOT NULL DEFAULT 0");
            conn.Execute("ALTER TABLE Languages ADD COLUMN SupportsTextMate INTEGER NOT NULL DEFAULT 0");
            conn.Execute("ALTER TABLE Snippets ADD COLUMN DateCreated TEXT");
            conn.Execute("ALTER TABLE Snippets ADD COLUMN DateModified TEXT");
            conn.Execute(@"
                CREATE TRIGGER insert_snippet_dates AFTER INSERT ON Snippets
                BEGIN
                    UPDATE Snippets
                    SET DateCreated = DATETIME('now'),
                        DateModified = DATETIME('now')
                    WHERE ID = NEW.ID;
                END;
            ");
            conn.Execute(@"
                CREATE TRIGGER update_snippet_modified AFTER UPDATE ON Snippets
                BEGIN
                    UPDATE Snippets SET DateModified = DATETIME('now') WHERE ID = NEW.ID;
                END;
             ");
            // Populate SupportsXshd + SupportsTextMate for existing languages
            var languages = conn.Query<(int Id, string Code)>("SELECT ID, Code FROM Languages");

            foreach (var lang in languages)
            {
                string code = lang.Code?.Trim().ToLowerInvariant() ?? "";

                bool xshd = HighlightingService.SyntaxDefinitionExists(code);
                bool tm = HighlightingService.SupportsTextMate(code);

                conn.Execute(
                    "UPDATE Languages SET SupportsXshd = @xshd, SupportsTextMate = @tm WHERE ID = @id",
                    new { xshd = xshd ? 1 : 0, tm = tm ? 1 : 0, id = lang.Id });
            }

            conn.Execute("UPDATE Meta SET Value = 1 WHERE Key='DbVersion'");
            version = 1;
        }
        // Future migrations would go here, following the same pattern:
        // if (version < 2) ...
    }


    private const string ddl = @"
CREATE TABLE IF NOT EXISTS Meta (
    Key TEXT PRIMARY KEY,
    Value TEXT
);

CREATE TABLE IF NOT EXISTS Languages (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    Name TEXT,
    SupportsXshd INTEGER NOT NULL DEFAULT 0,
    SupportsTextMate INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS Categories (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    LanguageId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    FOREIGN KEY (LanguageId) REFERENCES Languages(ID) ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS Snippets (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Code TEXT,
    Description TEXT,
    Tag TEXT,
    DateCreated TEXT,
    DateModified TEXT,
    FOREIGN KEY (CategoryId) REFERENCES Categories(ID) ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TRIGGER insert_snippet_dates AFTER INSERT ON Snippets
BEGIN
    UPDATE Snippets
    SET DateCreated = DATETIME('now'),
        DateModified = DATETIME('now')
    WHERE ID = NEW.ID;
END;

CREATE TRIGGER update_snippet_modified AFTER UPDATE ON Snippets
BEGIN
    UPDATE Snippets SET DateModified = DATETIME('now') WHERE ID = NEW.ID;
END;

INSERT OR IGNORE INTO Meta (Key, Value) VALUES ('DbVersion', '1');
						
INSERT INTO Languages (ID, Code, Name, SupportsXshd, SupportsTextMate) VALUES
(1, 'as', 'ActionScript3',1 , 0),
(2, 'aspx', 'ASP/XHTML', 1, 0),
(3, 'atg', 'Coco', 1, 0),
(4, 'bat', 'BAT', 1, 1),
(5, 'boo', 'Boo', 1, 0),
(6, 'cpp', 'C++', 1, 1),
(7, 'cs', 'C#', 1, 1),
(8, 'css', 'CSS', 1, 1),
(9, 'd', 'D', 1, 0),
(10, 'fs', 'F#', 1, 1),
(11, 'fx', 'HLSL', 1, 1),
(12, 'html', 'HTML', 1, 1),
(13, 'ini', 'INI', 1, 1),
(14, 'java', 'Java', 1, 1),
(15, 'js', 'JavaScript', 1, 1),
(16, 'json', 'Json', 1, 1),
(17, 'md', 'MarkDown', 1, 1),
(18, 'nut', 'Squirrel', 1, 0),
(19, 'pas', 'Pascal', 1, 1),
(20, 'php', 'PHP', 1, 1),
(21, 'plsql', 'PLSQL', 1, 0),
(22, 'ps1', 'PowerShell', 1, 1),
(23, 'py', 'Python', 1, 1),
(24, 'rb', 'Ruby', 1, 1),
(25, 'rs', 'Rust', 1, 1),
(26, 'sql', 'SQL', 1, 1),
(27, 'tex', 'TeX', 1, 0),
(28, 'vb', 'VB.NET', 1, 1),
(29, 'vtl', 'VTL', 1, 0),
(30, 'xml', 'XML', 1, 1),
(31, 'lua', 'Lua', 1, 1),
(32, 'asm', 'Asm', 1, 0),
(33, 'il', 'IL', 1, 0),
(34, 'go', 'Go', 1, 1),
(35, 'zig', 'Zig', 1, 0),
(36, 'sh', 'Bash', 1, 1),
-- new from textmate grammars
(37, 'asciidoc', 'AsciiDoc', 0, 1),
(38, 'clojure', 'Clojure', 0, 1),
(39, 'coffee', 'CoffeeScript', 0, 1),
(40, 'cu', 'CUDA C++', 0, 1),
(41, 'dart', 'Dart', 0, 1),
(42, 'diff', 'Diff', 0, 1),
(43, 'dockerfile', 'Docker', 0, 1),
(44, 'gitignore', 'Ignore', 0, 1),
(45, 'groovy', 'Groovy', 0, 1),
(46, 'handlebars', 'Handlebars', 0, 1),
(47, 'properties', 'Properties', 0, 1),
(48, 'jsx', 'JavaScript React', 0, 1),
(49, 'jsonc', 'JSON with Comments', 0, 1),
(50, 'jl', 'Julia', 0, 1),
(51, 'ltx', 'LaTeX', 0, 1),
(52, 'bib', 'BibTeX', 0, 1),
(53, 'less', 'Less', 0, 1),
(54, 'log', 'Log', 0, 1),
(55, 'mk', 'Makefile', 0, 1),
(56, 'm', 'Objective-C', 0, 1),
(57, 'mm', 'Objective-C++', 0, 1),
(58, 'pl', 'Perl', 0, 1),
(59, 'pl6', 'Perl 6', 0, 1),
(60, 'jade', 'Jade', 0, 1),
(61, 'r', 'R', 0, 1),
(62, 'razor', 'Razor', 0, 1),
(63, 'scss', 'SCSS', 0, 1),
(64, 'shader', 'ShaderLab', 0, 1),
(65, 'swift', 'Swift', 0, 1),
(66, 'ts', 'TypeScript', 0, 1),
(67, 'tsx', 'TypeScript React', 0, 1),
(68, 'typ', 'Typst', 0, 1),
(69, 'typc', 'Typst (Code Mode)', 0, 1),
(70, 'xsl', 'XSL', 0, 1),
(71, 'yaml', 'YAML', 0, 1);
-- All new languages should be added here


INSERT INTO Categories (LanguageId, Name) VALUES
-- C++
(6, 'Basic Syntax'),
(6, 'STL (Standard Template Library)'),
(6, 'File I/O'),
(6, 'Exception Handling'),
(6, 'Pointers and Memory Management'),
(6, 'Classes and Objects'),
(6, 'Templates'),
(6, 'Multithreading'),
(6, 'Algorithms'),
(6, 'Preprocessor Directives'),
-- C#
(7, 'Basic Syntax'),
(7, 'Collections'),
(7, 'Database'),
(7, 'LINQ Queries'),
(7, 'File I/O'),
(7, 'Exception Handling'),
(7, 'OOP Concepts'),
(7, 'Delegates and Events'),
(7, 'Async Programming'),
(7, 'Windows Forms/WPF'),
(7, 'Networking'),
-- D
(9, 'Basic Syntax'),
(9, 'Ranges and Algorithms'),
(9, 'File I/O'),
(9, 'Exception Handling'),
(9, 'Memory Management'),
(9, 'Classes and Structs'),
(9, 'Templates and Mixins'),
(9, 'Concurrency'),
(9, 'Modules and Imports'),
(9, 'Metaprogramming'),
-- F#
(10, 'Basic Syntax'),
(10, 'Functions and Lambdas'),
(10, 'Pattern Matching'),
(10, 'Collections'),
(10, 'Async and Parallel Programming'),
(10, 'Type Providers'),
(10, 'Modules and Namespaces'),
(10, 'Error Handling'),
(10, 'Units of Measure'),
(10, 'Computation Expressions'),
-- Java
(14, 'Basic Syntax'),
(14, 'OOP Concepts'),
(14, 'Collections Framework'),
(14, 'Generics'),
(14, 'Exceptions'),
(14, 'File I/O'),
(14, 'Multithreading and Concurrency'),
(14, 'Java Streams and Lambdas'),
(14, 'Networking'),
(14, 'JVM Internals'),
-- JavaScript
(15, 'Basic Syntax'),
(15, 'Functions and Closures'),
(15, 'DOM Manipulation'),
(15, 'Events'),
(15, 'Promises and Async/Await'),
(15, 'Modules'),
(15, 'ES6+ Features'),
(15, 'Error Handling'),
(15, 'Testing'),
(15, 'Node.js'),
-- Pascal
(19, 'Basic Syntax'),
(19, 'Procedures and Functions'),
(19, 'Data Types and Variables'),
(19, 'Control Structures'),
(19, 'Records and Sets'),
(19, 'File Handling'),
(19, 'Object-Oriented Pascal'),
(19, 'Exception Handling'),
(19, 'Generics'),
(19, 'Multithreading'),
-- PowerShell
(22, 'Basic Syntax'),
(22, 'Cmdlets'),
(22, 'Functions and Scripts'),
(22, 'Modules'),
(22, 'Error Handling'),
(22, 'Remoting and Sessions'),
(22, 'Pipeline and Objects'),
(22, 'Security'),
(22, 'Event Handling'),
(22, 'Desired State Configuration (DSC)'),
-- Python
(23, 'Basic Syntax'),
(23, 'Strings'),
(23, 'Lists and Tuples'),
(23, 'Dictionaries and Sets'),
(23, 'File I/O'),
(23, 'Exception Handling'),
(23, 'Functions and Lambdas'),
(23, 'Classes and OOP'),
(23, 'Modules and Packages'),
(23, 'Iterators and Generators'),
(23, 'Comprehensions'),
(23, 'Decorators'),
(23, 'Context Managers'),
(23, 'Regular Expressions'),
(23, 'Data Serialization'),
(23, 'Networking'),
(23, 'Multithreading and Multiprocessing'),
(23, 'Virtual Environments and Packaging'),
-- Rust
(25, 'Basic Syntax'),
(25, 'Ownership and Borrowing'),
(25, 'Traits and Generics'),
(25, 'Error Handling'),
(25, 'Concurrency'),
(25, 'Modules and Crates'),
(25, 'Macros'),
(25, 'Patterns'),
(25, 'Unsafe Rust'),
(25, 'FFI (Foreign Function Interface)');
";

    private static void AddSnippet(int langId, InitialSnippet snippet)
    {
        if (!initialSnippets.TryGetValue(langId, out var list))
        {
            list = [];
            initialSnippets[langId] = list;
        }

        list.Add(snippet);
    }

    private static void PopulateInitialSnippets()
    {
        // C# Hello World
        AddSnippet(7, new InitialSnippet
        {
            Title = "Hello World",
            Code = @"using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}",
            Description = "Hello World program in C#",
            Tag = "hello,world"
        });

        // C# Roslyn Script
        AddSnippet(7, new InitialSnippet
        {
            Title = "Roslyn Script Example",
            Code = @"#r ""System.Net.Http""
using System.Net.Http;

var client = new HttpClient();
var response = await client.GetStringAsync(""https://example.com"");

Console.WriteLine(response);",
            Description = "Basic Roslyn C# script using #r directive and top-level statements.",
            Tag = "csharp,roslyn,script"
        });

        // F# Hello World
        AddSnippet(10, new InitialSnippet
        {
            Title = "Hello World",
            Code = @"printfn ""Hello, World!""",
            Description = "Hello World program in F#",
            Tag = "hello,world"
        });

        // F# Script Example
        AddSnippet(10, new InitialSnippet
        {
            Title = "F# Script Example",
            Code = @"#r ""nuget: Newtonsoft.Json""
open Newtonsoft.Json

type Person = { Name: string; Age: int }

let json = ""{ \""Name\"": \""Ana\"", \""Age\"": 30 }""
let person = JsonConvert.DeserializeObject<Person>(json)

printfn ""%A"" person",
            Description = "F# script using #r directive and JSON deserialization.",
            Tag = "fsharp,script,fsx"
        });

        // C++ Hello World
        AddSnippet(6, new InitialSnippet
        {
            Title = "Hello World",
            Code = @"#include <iostream>

int main()
{
    std::cout << ""Hello, World!"" << std::endl;
    return 0;
}",
            Description = "Hello World program in C++",
            Tag = "hello,world"
        });

        // D Hello World
        AddSnippet(9, new InitialSnippet
        {
            Title = "Hello World",
            Code = @"import std.stdio;

void main()
{
    writeln(""Hello, World!"");
}",
            Description = "Hello World program in D",
            Tag = "hello,world"
        });

        // Python Hello World
        AddSnippet(23, new InitialSnippet
        {
            Title = "Hello World",
            Code = @"def main():
    print(""Hello, World!"")
    
if __name__ == '__main__':
    main()",
            Description = "Hello World program in Python",
            Tag = "hello,world"
        });


    }


}

public class InitialSnippet
{
    public int LanguageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
}

