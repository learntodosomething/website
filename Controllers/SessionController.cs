using System;
using Microsoft.Data.Sqlite; // JAVÍTÁS: Microsoft.Data.Sqlite használata SQLiteConnection helyett
using Microsoft.AspNetCore.Mvc;
using _02post.Models;

[ApiController]
[Route("[Controller]/[action]")]
public class HelloController : Controller
{
    // JAVÍTÁS: Ugyanaz a connection string mint a PostController-ben és Program.cs-ben
    private const string ConnectionString = "Data Source=shema.db";
    private const string SessionUserIdKey = "UserId";
    private const string SessionIdKey = "SessionId";
    private const string SessionUserNameKey = "UserName";

    [HttpPost]
    public string Post([FromForm] string fname, [FromForm] string email)
    {
        // Generálja a jelszó hash-t és kapja meg a sót
        string hashedPassword, salt;
        hashedPassword = PasswordManager.GeneratePasswordHash(fname, out salt);

        try
        {
            // Mentés az adatbázisba - JAVÍTOTT: helyes paraméter sorrend
            SaveToDatabase(fname, email, hashedPassword, salt);
        }
        catch (Exception ex)
        {
            // Kezeljük le az esetleges kivételt
            return ex.Message;
        }

        // Kiírja mind a sima hash-t, mind a sót és az email-t
        return $"Szia {fname} a regisztráció sikeres volt!";
    }

    private void SaveToDatabase(string fname, string email, string hashedPassword, string salt)
    {
        // JAVÍTÁS: Microsoft.Data.Sqlite használata
        using (SqliteConnection connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            // Ellenőrzi, hogy az email még nem szerepel-e az adatbázisban
            using (SqliteCommand checkEmailCommand = new SqliteCommand(
                "SELECT COUNT(*) FROM Users WHERE Email = @email;",
                connection))
            {
                checkEmailCommand.Parameters.AddWithValue("@email", email);

                int emailCount = Convert.ToInt32(checkEmailCommand.ExecuteScalar());
                if (emailCount > 0)
                {
                    // Az email már szerepel az adatbázisban, kezeljük le ezt a helyzetet
                    throw new Exception("E-mail already exists");
                }
            }

            // Ellenőrzi, hogy az fname még nem szerepel-e az adatbázisban
            using (SqliteCommand checkFnameCommand = new SqliteCommand(
                "SELECT COUNT(*) FROM Users WHERE FirstName = @fname;",
                connection))
            {
                checkFnameCommand.Parameters.AddWithValue("@fname", fname);

                int fnameCount = Convert.ToInt32(checkFnameCommand.ExecuteScalar());
                if (fnameCount > 0)
                {
                    // Az fname már szerepel az adatbázisban, kezeljük le ezt a helyzetet
                    throw new Exception("Username already exists");
                }
            }

            // JAVÍTÁS: Új adatbázis struktúra - Salt és PasswordHash mezők hozzáadása
            // Először ellenőrizzük, hogy léteznek-e ezek a mezők
            AddPasswordColumnsIfNotExist(connection);

            // JAVÍTOTT: Helyes adatbázis struktúra használata
            // Users táblában: Id, FirstName, Email, Password, Salt, CreatedAt
            using (SqliteCommand insertCommand = new SqliteCommand(
                "INSERT INTO Users (FirstName, Email, Password, Salt) VALUES (@fname, @email, @hashedPassword, @salt);",
                connection))
            {
                insertCommand.Parameters.AddWithValue("@fname", fname);
                insertCommand.Parameters.AddWithValue("@email", email);
                insertCommand.Parameters.AddWithValue("@hashedPassword", hashedPassword); // Hash a Password mezőbe
                insertCommand.Parameters.AddWithValue("@salt", salt); // Salt a Salt mezőbe

                insertCommand.ExecuteNonQuery();
            }
        }
    }

    private void AddPasswordColumnsIfNotExist(SqliteConnection connection)
    {
        try
        {
            // Ellenőrizzük, hogy létezik-e a Salt oszlop
            string checkSaltColumn = "PRAGMA table_info(Users)";
            bool saltExists = false;

            using (var command = new SqliteCommand(checkSaltColumn, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader.GetString(1);
                        if (columnName.Equals("Salt", StringComparison.OrdinalIgnoreCase))
                        {
                            saltExists = true;
                            break;
                        }
                    }
                }
            }

            // Ha nem létezik a Salt oszlop, hozzáadjuk
            if (!saltExists)
            {
                string addSaltColumn = "ALTER TABLE Users ADD COLUMN Salt TEXT";
                using (var command = new SqliteCommand(addSaltColumn, connection))
                {
                    command.ExecuteNonQuery();
                    Console.WriteLine("✅ Salt oszlop hozzáadva a Users táblához");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Salt oszlop hozzáadási hiba: {ex.Message}");
        }
    }

    [HttpPost]
    public IActionResult Login([FromForm] string email, [FromForm] string pass)
    {
        try
        {
            // DEBUG: Bejelentkezési kísérlet logolása
            Console.WriteLine($"🔐 Bejelentkezési kísérlet - Email: {email}");
            
            // Ellenőrzi, hogy a felhasználó létezik-e és a jelszó helyes-e
            var (userId, userName) = CheckCredentials(email, pass);

            Console.WriteLine($"✅ Sikeres bejelentkezés - UserId: {userId}, UserName: {userName}");

            // Ha a felhasználó létezik és a jelszó helyes, hozz létre egy munkamenet azonosítót
            string sessionId = Guid.NewGuid().ToString();
            
            // JAVÍTÁS: Session beállítások explicit módon
            HttpContext.Session.SetString(SessionUserIdKey, userId.ToString());
            HttpContext.Session.SetString(SessionUserNameKey, userName);
            HttpContext.Session.SetString(SessionIdKey, sessionId);

            // DEBUG: Session információk ellenőrzése
            Console.WriteLine($"🔍 Session beállítva:");
            Console.WriteLine($"  - Session ID: {HttpContext.Session.Id}");
            Console.WriteLine($"  - UserId: {HttpContext.Session.GetString(SessionUserIdKey)}");
            Console.WriteLine($"  - UserName: {HttpContext.Session.GetString(SessionUserNameKey)}");
            Console.WriteLine($"  - SessionId: {HttpContext.Session.GetString(SessionIdKey)}");

            // Beállítja a cookie-t a sikeres bejelentkezés jelzésére
            Response.Cookies.Append("LoggedIn", "true", new CookieOptions 
            { 
                Path = "/",
                HttpOnly = false // Hogy a JavaScript is hozzáférjen
            });

            // Elküldi a munkamenet azonosítót süti formájában
            Response.Cookies.Append("SessionIdCookie", sessionId, new CookieOptions 
            { 
                Path = "/",
                HttpOnly = true 
            });

            return Ok("A bejelentkezés sikeres volt");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Bejelentkezési hiba: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    private (int userId, string userName) CheckCredentials(string email, string pass)
    {
        // JAVÍTÁS: Microsoft.Data.Sqlite használata
        using (SqliteConnection connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            // JAVÍTOTT: Helyes adatbázis struktúra szerint dolgozunk
            // Először próbáljuk az új struktúrával (Password és Salt oszlopokkal)
            using (SqliteCommand command = new SqliteCommand(
                "SELECT Id, FirstName, Password, Salt FROM Users WHERE Email = @email;",
                connection))
            {
                command.Parameters.AddWithValue("@email", email);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int userId = reader.GetInt32(0);            // Index 0: Id
                        string firstName = reader.GetString(1);     // Index 1: FirstName
                        string storedPasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2); // Index 2: Password
                        string salt = reader.IsDBNull(3) ? "" : reader.GetString(3); // Index 3: Salt (JAVÍTVA!)

                        // Teljes név összeállítása
                        string userName = $"{firstName}";

                        Console.WriteLine($"🔍 Felhasználó megtalálva - ID: {userId}, Név: {userName}");
                        Console.WriteLine($"🔍 Jelszó ellenőrzés...");

                        // Ha van Salt oszlop és nem üres, akkor az új rendszert használjuk
                        if (!string.IsNullOrEmpty(salt))
                        {
                            // Ellenőrzi a felhasználó által megadott jelszót a tárolt hash-sel és sóval
                            if (PasswordManager.Verify(pass, salt, storedPasswordHash))
                            {
                                Console.WriteLine($"✅ Jelszó helyes (új rendszer)!");
                                return (userId, userName);
                            }
                            else
                            {
                                Console.WriteLine($"❌ Hibás jelszó (új rendszer)!");
                                throw new Exception("Hibás jelszó!");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Régi adatbázis struktúra használata");
                            // Próbáljuk a régi módszert is
                            try
                            {
                                string oldSalt = "";
                                if (PasswordManager.Verify(pass, oldSalt, storedPasswordHash))
                                {
                                    Console.WriteLine($"✅ Jelszó helyes (régi rendszer)!");
                                    return (userId, firstName); // Csak a firstName-t adjuk vissza
                                }
                            }
                            catch
                            {
                                // Ha a régi módszer sem működik, hibát dobunk
                            }
                            
                            Console.WriteLine($"❌ Hibás jelszó!");
                            throw new Exception("Hibás jelszó!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Felhasználó nem található!");
                        throw new Exception("Hibás felhasználónév!");
                    }
                }
            }
        }
    }

    // Új metódus: Felhasználó információk lekérése
    [HttpGet]
    public IActionResult GetCurrentUser()
    {
        try
        {
            var userIdString = HttpContext.Session.GetString(SessionUserIdKey);
            var userName = HttpContext.Session.GetString(SessionUserNameKey);
            
            Console.WriteLine($"🔍 GetCurrentUser - UserId: '{userIdString}', UserName: '{userName}'");
            
            if (string.IsNullOrEmpty(userIdString) || string.IsNullOrEmpty(userName))
            {
                return Unauthorized(new { message = "Nincs bejelentkezve" });
            }

            return Ok(new { 
                userId = int.Parse(userIdString), 
                userName = userName,
                isLoggedIn = true 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetCurrentUser hiba: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
    }

    // Session ellenőrzés
    [HttpGet]
    public IActionResult CheckSession()
    {
        var userIdString = HttpContext.Session.GetString(SessionUserIdKey);
        var userName = HttpContext.Session.GetString(SessionUserNameKey);
        
        Console.WriteLine($"🔍 CheckSession - UserId: '{userIdString}', UserName: '{userName}'");
        Console.WriteLine($"🔍 CheckSession - Session ID: {HttpContext.Session.Id}");
        Console.WriteLine($"🔍 CheckSession - Session Keys: {string.Join(", ", HttpContext.Session.Keys)}");
        
        if (string.IsNullOrEmpty(userIdString) || string.IsNullOrEmpty(userName))
        {
            return Ok(new { isLoggedIn = false });
        }

        return Ok(new { 
            isLoggedIn = true,
            userId = int.Parse(userIdString),
            userName = userName
        });
    }

    // FEJLESZTÉSI SEGÉD: Teszt bejelentkezés endpoint
    [HttpPost]
    public IActionResult TestLogin([FromForm] int userId)
    {
        try
        {
            // Felhasználó adatainak lekérdezése az adatbázisból
            using (SqliteConnection connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                
                using (SqliteCommand command = new SqliteCommand(
                    "SELECT Id, FirstName FROM Users WHERE Id = @userId;", connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string firstName = reader.GetString(1);
                            
                            // Teljes név összeállítása
                            string userName = $"{firstName}";
                            
                            // Session beállítása
                            string sessionId = Guid.NewGuid().ToString();
                            HttpContext.Session.SetString(SessionUserIdKey, id.ToString());
                            HttpContext.Session.SetString(SessionUserNameKey, userName);
                            HttpContext.Session.SetString(SessionIdKey, sessionId);
                            
                            Console.WriteLine($"🔓 Teszt bejelentkezés - UserId: {id}, UserName: {userName}");
                            
                            return Ok($"Teszt bejelentkezés sikeres - {userName}");
                        }
                        else
                        {
                            return BadRequest("Felhasználó nem található");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Teszt bejelentkezés hiba: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }
}