
namespace Messanger.Models.ViewModels
{
    public class HomePageViewModel
    {
        
        public int CurrentUserId { get; set; }
        public string CurrentUserLogin { get; set; } = "";
        public string CurrentUserEmail { get; set; } = "";
        public string? CurrentUserAva { get; set; }     

        
        public List<ChatViewModel> Chats { get; set; } = new();

       
        public List<ChatMessageViewModel> Messages { get; set; } = new();
        public int? SelectedChatId { get; set; }
    }
}
