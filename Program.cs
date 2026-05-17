using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure sessions
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Fájl feltöltés konfiguráció
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Adatbázis inicializálás - IDE kerül be!
InitializeDatabase();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseSession();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("index.html"));

app.Run();

// Adatbázis inicializáló függvények
void InitializeDatabase()
{
    // Változtasd meg az adatbázis útvonalát a saját fájlodra!
    string connectionString = "Data Source=shema.db";
    
    try
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            
            // FOREIGN KEY ellenőrzés kikapcsolása az inicializálás alatt
            ExecuteCommand(connection, "PRAGMA foreign_keys = OFF", "FOREIGN KEY ellenőrzés kikapcsolása");
            
            // Először hozzuk létre az alapvető táblákat ha nem léteznek
            CreateTablesIfNotExist(connection);
            
            // Aztán adjuk hozzá a hiányzó oszlopokat
            AddMissingColumns(connection);
            
            // Adatok tisztítása - érvénytelen hivatkozások törlése
            CleanInvalidReferences(connection);
            
            // FOREIGN KEY ellenőrzés visszakapcsolása
            ExecuteCommand(connection, "PRAGMA foreign_keys = ON", "FOREIGN KEY ellenőrzés bekapcsolása");
            
            Console.WriteLine("✅ Adatbázis sikeresen inicializálva!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Adatbázis inicializálási hiba: {ex.Message}");
    }
}

void CreateTablesIfNotExist(SqliteConnection connection)
{
    // Users tábla létrehozása - JAVÍTÁS: ezt is létre kell hozni!
    string createUsersTable = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FirstName TEXT NOT NULL,
            Email TEXT UNIQUE,
            Password TEXT,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        )";
    
    // Posts tábla létrehozása - FOREIGN KEY constraint nélkül egyelőre
    string createPostsTable = @"
        CREATE TABLE IF NOT EXISTS Posts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER,
            Content TEXT,
            ImagePath TEXT,
            FileName TEXT,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        )";
    
    // Comments tábla létrehozása - FOREIGN KEY constraint nélkül egyelőre
    string createCommentsTable = @"
        CREATE TABLE IF NOT EXISTS Comments (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PostId INTEGER,
            UserId INTEGER,
            Content TEXT NOT NULL,
            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
        )";
    
    // Minden táblát létrehozunk
    ExecuteCommand(connection, createUsersTable, "Users tábla");
    ExecuteCommand(connection, createPostsTable, "Posts tábla");
    ExecuteCommand(connection, createCommentsTable, "Comments tábla");
    
    // Teszt felhasználó hozzáadása ha üres a Users tábla
    AddTestUserIfEmpty(connection);
}

void AddTestUserIfEmpty(SqliteConnection connection)
{
    // Ellenőrizzük hogy van-e már felhasználó
    string checkQuery = "SELECT COUNT(*) FROM Users";
    using (var command = new SqliteCommand(checkQuery, connection))
    {
        var count = Convert.ToInt32(command.ExecuteScalar());
        
        if (count == 0)
        {
            // Teszt felhasználó hozzáadása
            string insertUser = @"
                INSERT INTO Users (FirstName, Email, Password) 
                VALUES ('Teszt', 'Felhasználó', 'teszt@example.com', 'password123')";
            
            using (var insertCommand = new SqliteCommand(insertUser, connection))
            {
                insertCommand.ExecuteNonQuery();
                Console.WriteLine("👤 Teszt felhasználó létrehozva (ID: 1)");
            }
        }
    }
}

void CleanInvalidReferences(SqliteConnection connection)
{
    try
    {
        // Érvénytelen Posts törlése (ahol a UserId nem létezik)
        string cleanPosts = @"
            DELETE FROM Posts 
            WHERE UserId NOT IN (SELECT Id FROM Users)";
        
        using (var command = new SqliteCommand(cleanPosts, connection))
        {
            int deletedPosts = command.ExecuteNonQuery();
            if (deletedPosts > 0)
            {
                Console.WriteLine($"🧹 Törölve {deletedPosts} érvénytelen post");
            }
        }
        
        // Érvénytelen Comments törlése (ahol a UserId vagy PostId nem létezik)
        string cleanComments = @"
            DELETE FROM Comments 
            WHERE UserId NOT IN (SELECT Id FROM Users) 
               OR PostId NOT IN (SELECT Id FROM Posts)";
        
        using (var command = new SqliteCommand(cleanComments, connection))
        {
            int deletedComments = command.ExecuteNonQuery();
            if (deletedComments > 0)
            {
                Console.WriteLine($"🧹 Törölve {deletedComments} érvénytelen komment");
            }
        }
        
        Console.WriteLine("✅ Adatbázis tisztítása befejezve");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Adatbázis tisztítási hiba: {ex.Message}");
    }
}

void AddMissingColumns(SqliteConnection connection)
{
    // Users tábla oszlopainak ellenőrzése
    if (TableExists(connection, "Users"))
    {
        AddColumnIfNotExists(connection, "Users", "FirstName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfNotExists(connection, "Users", "Email", "TEXT");
        AddColumnIfNotExists(connection, "Users", "Password", "TEXT");
        AddColumnIfNotExists(connection, "Users", "Salt", "TEXT"); // JAVÍTÁS: Salt oszlop hozzáadása
        AddColumnIfNotExists(connection, "Users", "CreatedAt", "DATETIME DEFAULT CURRENT_TIMESTAMP");
    }
    
    // Posts tábla oszlopainak ellenőrzése
    if (TableExists(connection, "Posts"))
    {
        AddColumnIfNotExists(connection, "Posts", "UserId", "INTEGER");
        AddColumnIfNotExists(connection, "Posts", "Content", "TEXT");
        AddColumnIfNotExists(connection, "Posts", "ImagePath", "TEXT");
        AddColumnIfNotExists(connection, "Posts", "FileName", "TEXT");
        AddColumnIfNotExists(connection, "Posts", "CreatedAt", "DATETIME DEFAULT CURRENT_TIMESTAMP");
    }
    
    // Comments tábla oszlopainak ellenőrzése
    if (TableExists(connection, "Comments"))
    {
        AddColumnIfNotExists(connection, "Comments", "PostId", "INTEGER");
        AddColumnIfNotExists(connection, "Comments", "UserId", "INTEGER");
        AddColumnIfNotExists(connection, "Comments", "Content", "TEXT");
        AddColumnIfNotExists(connection, "Comments", "CreatedAt", "DATETIME DEFAULT CURRENT_TIMESTAMP");
    }
}

bool TableExists(SqliteConnection connection, string tableName)
{
    string query = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName";
    using (var command = new SqliteCommand(query, connection))
    {
        command.Parameters.AddWithValue("@tableName", tableName);
        var result = command.ExecuteScalar();
        return result != null;
    }
}

void AddColumnIfNotExists(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
{
    try
    {
        // Ellenőrizzük hogy létezik-e az oszlop
        string checkQuery = $"PRAGMA table_info({tableName})";
        bool columnExists = false;
        
        using (var command = new SqliteCommand(checkQuery, connection))
        {
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // JAVÍTÁS: oszlop név szerint elérés helyett ordinal használata
                    string currentColumnName = reader.GetString(1); // Az 1. index a "name" oszlop
                    if (currentColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }
        }
        
        // Ha nem létezik, hozzáadjuk
        if (!columnExists)
        {
            string alterQuery = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
            using (var command = new SqliteCommand(alterQuery, connection))
            {
                command.ExecuteNonQuery();
                Console.WriteLine($"➕ Oszlop hozzáadva: {tableName}.{columnName}");
            }
        }
    }
    catch (Exception ex)
    {
        // Ha a tábla nem létezik, azt már a CreateTablesIfNotExist kezeli
        if (!ex.Message.Contains("no such table"))
        {
            Console.WriteLine($"⚠️ Oszlop hozzáadási hiba ({tableName}.{columnName}): {ex.Message}");
        }
    }
}

void ExecuteCommand(SqliteConnection connection, string sql, string description)
{
    try
    {
        using (var command = new SqliteCommand(sql, connection))
        {
            command.ExecuteNonQuery();
            Console.WriteLine($"✅ {description} létrehozva/ellenőrizve");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {description} hiba: {ex.Message}");
    }
}