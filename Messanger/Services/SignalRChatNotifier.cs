using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Messanger.Hubs;
using Messanger.Models.Notifications;

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

        public Task NotifyPrivateMessageAsync(PrivateMessageDto dto)
            => _chatHub.Clients
                .Groups(dto.SenderId.ToString(), dto.RecipientId.ToString())
                .SendAsync("ReceivePrivateMessage",
                           dto.SenderId,
                           dto.Login,
                           dto.Avatar,
                           dto.Text,
                           dto.Timestamp);

        public Task NotifyGroupMessageAsync(GroupMessageDto dto)
            => _groupHub.Clients
                .Group($"group-{dto.GroupId}")
                .SendAsync("ReceiveGroupMessage",
                           dto.MessageId,
                           dto.SenderId,
                           dto.Login,
                           dto.Email,
                           dto.Avatar,
                           dto.Text,
                           dto.Timestamp);

        public Task NotifyPrivateFileAsync(PrivateFileDto dto)
            => _chatHub.Clients
                .Groups(dto.SenderId.ToString(), dto.RecipientId.ToString())
                .SendAsync("ReceivePrivateFile",
                           dto.SenderId,
                           dto.Login,
                           dto.Avatar,
                           dto.FileUrl,
                           dto.FileName,
                           dto.Timestamp);

        public Task NotifyGroupFileAsync(GroupFileDto dto)
            => _groupHub.Clients
                .Group($"group-{dto.GroupId}")
                .SendAsync("ReceiveGroupFile",
                           dto.MessageId,
                           dto.SenderId,
                           dto.Login,
                           dto.Email,
                           dto.Avatar,
                           dto.FileUrl,
                           dto.FileName,
                           dto.Timestamp);

        public Task NotifyPrivateDeletionAsync(PrivateDeletionDto dto)
            => _chatHub.Clients
                .Groups(dto.CurrentUserId.ToString(), dto.OtherUserId.ToString())
                .SendAsync("MessageDeleted", dto.MessageId);

        public Task NotifyPrivateEditAsync(PrivateEditDto dto)
            => _chatHub.Clients
                .Groups(dto.CurrentUserId.ToString(), dto.OtherUserId.ToString())
                .SendAsync("MessageEdited", dto.MessageId, dto.NewText);

        public Task NotifyGroupDeletionAsync(GroupDeletionDto dto)
            => _groupHub.Clients
                .Group($"group-{dto.GroupId}")
                .SendAsync("GroupMessageDeleted", dto.MessageId);

        public Task NotifyGroupEditAsync(GroupEditDto dto)
            => _groupHub.Clients
                .Group($"group-{dto.GroupId}")
                .SendAsync("GroupMessageEdited", dto.MessageId, dto.NewText);
    }
}
