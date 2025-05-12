
namespace Messanger.Models.ViewModels
{
    public class HomePageViewModel
    {
       
        public int CurrentUserId { get; set; }
        public string CurrentUserLogin { get; set; } = string.Empty;
        public string CurrentUserEmail { get; set; } = string.Empty;
        public string? CurrentUserAva { get; set; }

        
        public List<ChatViewModel> Chats { get; set; } = new();

        
        public List<GroupViewModel> Groups { get; set; } = new();

        
        public List<ChatMessageViewModel> Messages { get; set; } = new();

       
        public int? SelectedChatId { get; set; }
        
        public int? SelectedGroupId { get; set; }
    }

    public class GroupViewModel
    {
        public int GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public DateTime LastAt { get; set; }
    }
}
