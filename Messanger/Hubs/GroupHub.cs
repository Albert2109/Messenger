
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Messanger.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Messanger.Hubs
{
    public class GroupHub : ChatHub
    {
        private readonly MessengerContext _db;

        public GroupHub(
            IWebHostEnvironment env,
            ILogger<GroupHub> logger,
            MessengerContext db
        ) : base(logger)
        {
            _db = db;
        }

        public override async Task OnConnectedAsync()
        {
           
            await base.OnConnectedAsync();

            
            var http = Context.GetHttpContext();
            if (http.Request.Query.TryGetValue("userId", out var uidVal)
                && int.TryParse(uidVal.First(), out var userId))
            {
                var groups = _db.GroupMembers
                    .Where(gm => gm.UserId == userId && !gm.Group.IsDeleted)
                    .Select(gm => $"group-{gm.GroupId}")
                    .ToList();

                foreach (var grp in groups)
                    await Groups.AddToGroupAsync(Context.ConnectionId, grp);
            }
        }

        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
           
            return base.OnDisconnectedAsync(exception);
        }
    }
}
