using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite; // JAVÍTÁS: Microsoft.Data.Sqlite használata
using _02post.Models;

namespace _02post.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class PostController : ControllerBase
    {
        private const string ConnectionString = "Data Source=shema.db";
        private const string SessionUserIdKey = "UserId";
        private readonly string _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        public PostController()
        {
            // Létrehozza az uploads mappát, ha nem létezik
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromForm] string? content, [FromForm] IFormFile? image)
        {
            // DEBUG: Session információk kiírása
            Console.WriteLine($"🔍 Session ID: {HttpContext.Session.Id}");
            Console.WriteLine($"🔍 Session Keys: {string.Join(", ", HttpContext.Session.Keys)}");
            
            var userIdString = HttpContext.Session.GetString(SessionUserIdKey);
            Console.WriteLine($"🔍 UserId from session: '{userIdString}'");
            
            // Ellenőrzi, hogy be van-e jelentkezve a felhasználó
            if (string.IsNullOrEmpty(userIdString))
            {
                Console.WriteLine("❌ Nincs UserId a session-ben");
                return Unauthorized(new { message = "Bejelentkezés szükséges!", requireLogin = true });
            }

            if (!int.TryParse(userIdString, out int userId))
            {
                Console.WriteLine($"❌ Nem sikerült parse-olni a UserId-t: '{userIdString}'");
                HttpContext.Session.Clear();
                return Unauthorized(new { message = "Érvénytelen munkamenet!", requireLogin = true });
            }

            Console.WriteLine($"✅ Parsed UserId: {userId}");

            // ELLENŐRIZZÜK, HOGY A FELHASZNÁLÓ LÉTEZIK-E AZ ADATBÁZISBAN
            if (!UserExists(userId))
            {
                Console.WriteLine($"❌ Felhasználó nem található az adatbázisban: {userId}");
                HttpContext.Session.Clear(); // Töröljük az érvénytelen session-t
                return Unauthorized(new { message = "A felhasználó nem található! Kérlek jelentkezz be újra.", requireLogin = true });
            }

            Console.WriteLine($"✅ Felhasználó validálva: {userId}");

            // Legalább tartalomnak vagy képnek lennie kell
            if (string.IsNullOrWhiteSpace(content) && image == null)
            {
                return BadRequest("A bejegyzésnek tartalmaznia kell szöveget vagy képet!");
            }

            string? imagePath = null;
            string? fileName = null;

            // Kép feltöltése, ha van
            if (image != null)
            {
                // Ellenőrzi a fájl típusát
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest("Csak JPG, PNG és GIF fájlok engedélyezettek!");
                }

                // Fájl méret ellenőrzése (max 5MB)
                if (image.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("A fájl mérete nem lehet nagyobb 5MB-nál!");
                }

                // Egyedi fájlnév generálása
                fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(_uploadsPath, fileName);
                imagePath = $"/uploads/{fileName}";

                // Fájl mentése
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
            }

            try
            {
                // Mentés az adatbázisba
                SavePostToDatabase(userId, content, imagePath, image?.FileName);
                return Ok("Bejegyzés sikeresen létrehozva!");
            }
            catch (Exception ex)
            {
                // Ha hiba történt, töröljük a feltöltött fájlt
                if (!string.IsNullOrEmpty(fileName))
                {
                    var filePath = Path.Combine(_uploadsPath, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
                Console.WriteLine($"❌ Post létrehozási hiba: {ex.Message}");
                return BadRequest($"Hiba: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult GetPosts()
        {
            try
            {
                var posts = GetPostsFromDatabase();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Posts lekérdezési hiba: {ex.Message}");
                return BadRequest($"Hiba: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult AddComment([FromForm] int postId, [FromForm] string content)
        {
            // DEBUG: Session információk kiírása
            Console.WriteLine($"🔍 AddComment - Session ID: {HttpContext.Session.Id}");
            Console.WriteLine($"🔍 AddComment - Session Keys: {string.Join(", ", HttpContext.Session.Keys)}");
            
            var userIdString = HttpContext.Session.GetString(SessionUserIdKey);
            Console.WriteLine($"🔍 AddComment - UserId from session: '{userIdString}'");
            
            // Ellenőrzi, hogy be van-e jelentkezve a felhasználó
            if (string.IsNullOrEmpty(userIdString))
            {
                Console.WriteLine("❌ AddComment - Nincs UserId a session-ben");
                return Unauthorized(new { message = "Bejelentkezés szükséges!", requireLogin = true });
            }

            if (!int.TryParse(userIdString, out int userId))
            {
                Console.WriteLine($"❌ AddComment - Nem sikerült parse-olni a UserId-t: '{userIdString}'");
                HttpContext.Session.Clear();
                return Unauthorized(new { message = "Érvénytelen munkamenet!", requireLogin = true });
            }

            Console.WriteLine($"✅ AddComment - Parsed UserId: {userId}");

            // ELLENŐRIZZÜK, HOGY A FELHASZNÁLÓ LÉTEZIK-E AZ ADATBÁZISBAN
            if (!UserExists(userId))
            {
                Console.WriteLine($"❌ AddComment - Felhasználó nem található az adatbázisban: {userId}");
                HttpContext.Session.Clear(); // Töröljük az érvénytelen session-t
                return Unauthorized(new { message = "A felhasználó nem található! Kérlek jelentkezz be újra.", requireLogin = true });
            }

            Console.WriteLine($"✅ AddComment - Felhasználó validálva: {userId}");

            // ELLENŐRIZZÜK, HOGY A POST LÉTEZIK-E
            if (!PostExists(postId))
            {
                return BadRequest("A bejegyzés nem található!");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("A komment nem lehet üres!");
            }

            try
            {
                SaveCommentToDatabase(postId, userId, content);
                return Ok("Komment sikeresen hozzáadva!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Komment hozzáadási hiba: {ex.Message}");
                return BadRequest($"Hiba: {ex.Message}");
            }
        }

        // ÚJ SEGÉD METÓDUSOK A VALIDÁCIÓHOZ
        private bool UserExists(int userId)
        {
            try
            {
                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    var query = "SELECT COUNT(*) FROM Users WHERE Id = @userId";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);
                        var count = Convert.ToInt32(command.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UserExists ellenőrzési hiba: {ex.Message}");
                return false;
            }
        }

        private bool PostExists(int postId)
        {
            try
            {
                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    var query = "SELECT COUNT(*) FROM Posts WHERE Id = @postId";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@postId", postId);
                        var count = Convert.ToInt32(command.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ PostExists ellenőrzési hiba: {ex.Message}");
                return false;
            }
        }

        private void SavePostToDatabase(int userId, string? content, string? imagePath, string? originalFileName)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                // Bejegyzés beszúrása
                var insertPost = @"
                    INSERT INTO Posts (UserId, Content, ImagePath, FileName) 
                    VALUES (@userId, @content, @imagePath, @fileName);";

                using (var command = new SqliteCommand(insertPost, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@content", content ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@imagePath", imagePath ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@fileName", originalFileName ?? (object)DBNull.Value);
                    command.ExecuteNonQuery();
                }
            }
        }

        private List<Post> GetPostsFromDatabase()
        {
            var posts = new List<Post>();

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                // MÓDOSÍTÁS: Csak FirstName lekérdezése, LastName kihagyása
                var query = @"
                    SELECT p.Id, p.UserId, u.FirstName, p.Content, p.ImagePath, p.FileName, p.CreatedAt
                    FROM Posts p
                    INNER JOIN Users u ON p.UserId = u.Id
                    ORDER BY p.CreatedAt DESC;";

                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // MÓDOSÍTÁS: Csak FirstName használata
                            var firstName = reader.GetString(2);

                            var post = new Post
                            {
                                Id = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                UserName = firstName, // MÓDOSÍTÁS: Csak FirstName
                                Content = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                FileName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                CreatedAt = reader.GetDateTime(6)
                            };
                            posts.Add(post);
                        }
                    }
                }

                // Kommentek betöltése minden bejegyzéshez
                foreach (var post in posts)
                {
                    post.Comments = GetCommentsForPost(connection, post.Id);
                }
            }

            return posts;
        }

        private List<Comment> GetCommentsForPost(SqliteConnection connection, int postId)
        {
            var comments = new List<Comment>();

            // MÓDOSÍTÁS: Csak FirstName lekérdezése, LastName kihagyása
            var query = @"
                SELECT c.Id, c.PostId, c.UserId, u.FirstName, c.Content, c.CreatedAt
                FROM Comments c
                INNER JOIN Users u ON c.UserId = u.Id
                WHERE c.PostId = @postId
                ORDER BY c.CreatedAt ASC;";

            using (var command = new SqliteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@postId", postId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // MÓDOSÍTÁS: Csak FirstName használata
                        var firstName = reader.GetString(3);

                        comments.Add(new Comment
                        {
                            Id = reader.GetInt32(0),
                            PostId = reader.GetInt32(1),
                            UserId = reader.GetInt32(2),
                            UserName = firstName, // MÓDOSÍTÁS: Csak FirstName
                            Content = reader.GetString(4),
                            CreatedAt = reader.GetDateTime(5)
                        });
                    }
                }
            }

            return comments;
        }

        private void SaveCommentToDatabase(int postId, int userId, string content)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                var insertComment = @"
                    INSERT INTO Comments (PostId, UserId, Content) 
                    VALUES (@postId, @userId, @content);";

                using (var command = new SqliteCommand(insertComment, connection))
                {
                    command.Parameters.AddWithValue("@postId", postId);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@content", content);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}