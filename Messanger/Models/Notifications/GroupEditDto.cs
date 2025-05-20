namespace Messanger.Models.Notifications
{
    public class GroupEditDto
    {
        public int GroupId { get; set; }
        public int MessageId { get; set; }
        public string NewText { get; set; } = null!;
    }
}
