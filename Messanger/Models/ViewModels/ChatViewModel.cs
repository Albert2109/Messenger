namespace Messanger.Models.ViewModels
{
    public class ChatViewModel
    {
       
        public int UserId { get; set; }
       
        public string Login { get; set; } = string.Empty;
       
        public string LastMessage { get; set; } = string.Empty;
       
        public DateTime LastAt { get; set; }
    }
    public class ChatMessageViewModel
    {
       
        public string UserLogin { get; set; } = string.Empty;
       
        public string Text { get; set; } = string.Empty;
        public string UserAvatar { get; set; } = "/images/default-avatar.png";


        public DateTime CreatedAt { get; set; }
    }
}
