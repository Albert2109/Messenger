
using Messanger.Models.Notifications;
using System.Threading.Tasks;

namespace Messanger.Services
{
    public interface IChatNotifier
    {
        Task NotifyPrivateMessageAsync(PrivateMessageDto dto);
        Task NotifyGroupMessageAsync(GroupMessageDto dto);
        Task NotifyPrivateFileAsync(PrivateFileDto dto);
        Task NotifyGroupFileAsync(GroupFileDto dto);
        Task NotifyPrivateDeletionAsync(PrivateDeletionDto dto);
        Task NotifyPrivateEditAsync(PrivateEditDto dto);
        Task NotifyGroupDeletionAsync(GroupDeletionDto dto);
        Task NotifyGroupEditAsync(GroupEditDto dto);
    }
}
