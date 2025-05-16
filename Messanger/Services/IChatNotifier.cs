
using System.Threading.Tasks;

namespace Messanger.Services
{
    public interface IChatNotifier
    {
        Task NotifyPrivateMessageAsync(
            
            int senderId,
            int recipientId,
            string login,
            string avatar,
            string text,
            string timestamp
        );

        Task NotifyGroupMessageAsync(
            int messageId,
            int groupId,
            int senderId,
            string login,
            string email,
            string avatar,
            string text,
            string timestamp
        );

        Task NotifyPrivateFileAsync(
           
            int senderId,
            int recipientId,
            string login,
            string avatar,
            string fileUrl,
            string fileName,
            string timestamp
        );

        Task NotifyGroupFileAsync(
            int messageId,
            int groupId,
            int senderId,
            string login,
            string email,
            string avatar,
            string fileUrl,
            string fileName,
            string timestamp
        );

        Task NotifyPrivateDeletionAsync(
            int messageId,
            int currentUserId,
            int otherUserId
        );

        Task NotifyPrivateEditAsync(
            int messageId,
            string newText,
            int currentUserId,
            int otherUserId
        );

        Task NotifyGroupDeletionAsync(
            int groupId,
            int messageId
        );

        Task NotifyGroupEditAsync(
            int groupId,
            int messageId,
            string newText
        );
    }
}
