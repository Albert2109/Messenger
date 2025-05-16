namespace Messanger.Models.Notifications
{
    public class PrivateFileDto
    {
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
        public string Login { get; set; } = null!;
        public string Avatar { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string Timestamp { get; set; } = null!;
       
    }
}
