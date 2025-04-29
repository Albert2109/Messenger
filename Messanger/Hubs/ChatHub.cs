using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using Messanger.Models;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Messanger.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<int, List<string>> _connections = new();
        private readonly IWebHostEnvironment _env;

        public ChatHub(IWebHostEnvironment env) => _env = env;

        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var vals)
                && int.TryParse(vals.First(), out var userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? e)
        {
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var vals)
                && int.TryParse(vals.First(), out var userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
            }
            await base.OnDisconnectedAsync(e);
        }


        public async Task SendPrivateMessage(int toUserId, string text)
        {
            var senderName = Context.User?.Identity?.Name ?? "Anonymous";
            var senderEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var senderAvatar = Context.User?.FindFirst("avatarUrl")?.Value ?? "/images/default-avatar.png";

            if (_connections.TryGetValue(toUserId, out var conns))
            {
                foreach (var conn in conns)
                    await Clients.Client(conn)
                        .SendAsync("ReceivePrivateMessage", senderName, senderEmail, senderAvatar, text);
            }
        }

        public async Task SendPrivateFile(int toUserId, byte[] fileData, string fileName)
        {
            var unique = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var path = Path.Combine(uploads, unique);
            await File.WriteAllBytesAsync(path, fileData);
            var url = $"/MessangerHome/Download?file={unique}&name={Uri.EscapeDataString(fileName)}";

            var senderName = Context.User?.Identity?.Name ?? "Anonymous";
            var senderEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var senderAvatar = Context.User?.FindFirst("avatarUrl")?.Value ?? "/images/default-avatar.png";

            var tasks = new List<Task>();

            
            if (_connections.TryGetValue(toUserId, out var conns))
            {
                foreach (var conn in conns)
                    tasks.Add(Clients.Client(conn)
                        .SendAsync("ReceivePrivateFile", senderName, senderEmail, senderAvatar, url, fileName));
            }
           
            tasks.Add(Clients.Caller
                .SendAsync("ReceivePrivateFile", senderName, senderEmail, senderAvatar, url, fileName));

            await Task.WhenAll(tasks);
        }


    }
}