namespace Messanger.Models.Notifications
{
    public class GroupFileDto
    {
        public int MessageId { get; set; }
        public int GroupId { get; set; }
        public int SenderId { get; set; }
        public string Login { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Avatar { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string Timestamp { get; set; } = null!;
    }
}
