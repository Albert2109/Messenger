namespace Messanger.Models.Notifications
{
    public class PrivateMessageDto
    {
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
        public string Login { get; set; } = null!;
        public string Avatar { get; set; } = null!;
        public string Text { get; set; } = null!;
        public string Timestamp { get; set; } = null!;
    }
}
