
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Messanger.Hubs;

namespace Messanger.Services
{
    public class SignalRChatNotifier : IChatNotifier
    {
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly IHubContext<GroupHub> _groupHub;

        public SignalRChatNotifier(
            IHubContext<ChatHub> chatHub,
            IHubContext<GroupHub> groupHub)
        {
            _chatHub = chatHub;
            _groupHub = groupHub;
        }

        public Task NotifyPrivateMessageAsync(
            
            int senderId,
            int recipientId,
            string login,
            string avatar,
            string text,
            string timestamp)
        {
            return _chatHub.Clients
                .Groups(senderId.ToString(), recipientId.ToString())
                .SendAsync("ReceivePrivateMessage",
                          
                           senderId,
                           login,
                           avatar,
                           text,
                           timestamp);
        }

        public Task NotifyGroupMessageAsync(
            int messageId,
            int groupId,
            int senderId,
            string login,
            string email,
            string avatar,
            string text,
            string timestamp)
        {
            return _groupHub.Clients
                .Group($"group-{groupId}")
                .SendAsync("ReceiveGroupMessage",
                           messageId,
                           senderId,
                           login,
                           email,
                           avatar,
                           text,
                           timestamp);
        }

        public Task NotifyPrivateFileAsync(
           
            int senderId,
            int recipientId,
            string login,
            string avatar,
            string fileUrl,
            string fileName,
            string timestamp)
        {
            return _chatHub.Clients
                .Groups(senderId.ToString(), recipientId.ToString())
                .SendAsync("ReceivePrivateFile",
                           
                           senderId,
                           login,
                           avatar,
                           fileUrl,
                           fileName,
                           timestamp);
        }

        public Task NotifyGroupFileAsync(
            int messageId,
            int groupId,
            int senderId,
            string login,
            string email,
            string avatar,
            string fileUrl,
            string fileName,
            string timestamp)
        {
            return _groupHub.Clients
                .Group($"group-{groupId}")
                .SendAsync("ReceiveGroupFile",
                           messageId,
                           senderId,
                           login,
                           email,
                           avatar,
                           fileUrl,
                           fileName,
                           timestamp);
        }

        public Task NotifyPrivateDeletionAsync(
            int messageId,
            int currentUserId,
            int otherUserId)
        {
            return _chatHub.Clients
                .Groups(currentUserId.ToString(), otherUserId.ToString())
                .SendAsync("MessageDeleted", messageId);
        }

        public Task NotifyPrivateEditAsync(
            int messageId,
            string newText,
            int currentUserId,
            int otherUserId)
        {
            return _chatHub.Clients
                .Groups(currentUserId.ToString(), otherUserId.ToString())
                .SendAsync("MessageEdited", messageId, newText);
        }

        public Task NotifyGroupDeletionAsync(
            int groupId,
            int messageId)
        {
            return _groupHub.Clients
                .Group($"group-{groupId}")
                .SendAsync("GroupMessageDeleted", messageId);
        }

        public Task NotifyGroupEditAsync(
            int groupId,
            int messageId,
            string newText)
        {
            return _groupHub.Clients
                .Group($"group-{groupId}")
                .SendAsync("GroupMessageEdited", messageId, newText);
        }
    }
}
