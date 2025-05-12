
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using Messanger.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Messanger.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<int, List<string>> _connections = new();
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IWebHostEnvironment env, ILogger<ChatHub> logger)
        {
            _env = env;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            _logger.LogInformation("OnConnectedAsync: ConnectionId={cid}, Query={query}", Context.ConnectionId, http.Request.QueryString);

            if (http.Request.Query.TryGetValue("userId", out var userIdVal) &&
                int.TryParse(userIdVal.First(), out var userId))
            {
                
                _connections.AddOrUpdate(
                    userId,
                    _ => new List<string> { Context.ConnectionId },
                    (_, list) => { lock (list) list.Add(Context.ConnectionId); return list; }
                );

                
                await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
                _logger.LogInformation("Added ConnectionId={cid} to group {group}", Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var userIdVal) &&
                int.TryParse(userIdVal.First(), out var userId))
            {
                
                if (_connections.TryGetValue(userId, out var list))
                {
                    lock (list) list.Remove(Context.ConnectionId);
                    if (!list.Any())
                        _connections.TryRemove(userId, out _);
                }

                
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
                _logger.LogInformation("Removed ConnectionId={cid} from group {group}", Context.ConnectionId, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

     
        public Task JoinGroup(string groupName)
        {
            _logger.LogInformation("JoinGroup: ConnectionId={cid}, Group={group}", Context.ConnectionId, groupName);
            return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public Task LeaveGroup(string groupName)
        {
            _logger.LogInformation("LeaveGroup: ConnectionId={cid}, Group={group}", Context.ConnectionId, groupName);
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
