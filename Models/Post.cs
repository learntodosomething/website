namespace _02post.Models
{
    public class Post
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Content { get; set; }
        public string? ImagePath { get; set; }
        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }

    public class Comment
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}