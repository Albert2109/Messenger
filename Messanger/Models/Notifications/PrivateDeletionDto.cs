namespace Messanger.Models.Notifications
{
    public class PrivateDeletionDto
    {
        public int MessageId { get; set; }
        public int CurrentUserId { get; set; }
        public int OtherUserId { get; set; }
    }
}
