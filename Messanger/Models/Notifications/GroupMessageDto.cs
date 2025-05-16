namespace Messanger.Models.Notifications
{
    public class GroupMessageDto
    {
        public int MessageId { get; set; }
        public int GroupId { get; set; }
        public int SenderId { get; set; }
        public string Login { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Avatar { get; set; } = null!;
        public string Text { get; set; } = null!;
        public string Timestamp { get; set; } = null!;

    }
}
