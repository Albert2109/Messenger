namespace Messanger.Models.Notifications
{
    public class PrivateEditDto
    {
        public int MessageId { get; set; }
        public string NewText { get; set; } = null!;
        public int CurrentUserId { get; set; }
        public int OtherUserId { get; set; }
    }
}
