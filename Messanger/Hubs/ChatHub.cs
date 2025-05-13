// ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Messanger.Hubs
{
    public class ChatHub : Hub
    {
       
        private static readonly ConcurrentDictionary<int, ConcurrentBag<string>> _connections
            = new ConcurrentDictionary<int, ConcurrentBag<string>>();

        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var uidVal)
                && int.TryParse(uidVal.First(), out var userId))
            {
               
                var bag = _connections.GetOrAdd(userId, _ => new ConcurrentBag<string>());
                bag.Add(Context.ConnectionId);

               
                await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());

                await Clients.All.SendAsync("UserOnline", userId);

                _logger.LogInformation("User {userId} connected (ConnectionId={cid})", userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var uidVal)
                && int.TryParse(uidVal.First(), out var userId))
            {
                
                if (_connections.TryGetValue(userId, out var bag))
                {
                    var remaining = new ConcurrentBag<string>(bag.Where(id => id != Context.ConnectionId));
                    _connections[userId] = remaining;
                    if (!remaining.Any())
                    {
                        _connections.TryRemove(userId, out _);
                        
                        await Clients.All.SendAsync("UserOffline", userId);
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
                _logger.LogInformation("User {userId} disconnected (ConnectionId={cid})", userId, Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

       
        public Task JoinGroup(string groupName)
            => Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        public Task LeaveGroup(string groupName)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
