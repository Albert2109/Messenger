using System.ComponentModel.DataAnnotations.Schema;

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
        public int Id { get; set; }
        public string UserLogin { get; set; } = "";
        public string UserAvatar { get; set; } = "";
        public string? Text { get; set; }
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; }


        [NotMapped]
        public bool HasText => !string.IsNullOrEmpty(Text);

        [NotMapped]
        public bool HasFile => !string.IsNullOrEmpty(FileUrl);

        [NotMapped]
        public bool IsOwn { get; set; }
    }
}
