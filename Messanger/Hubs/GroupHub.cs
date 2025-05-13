using Messanger.Models;
using Microsoft.AspNetCore.SignalR;

namespace Messanger.Hubs
{
    public class GroupHub : ChatHub
    {
        private readonly MessengerContext _db;
        public GroupHub(IWebHostEnvironment env,
                       ILogger<GroupHub> logger,
                       MessengerContext db)
           : base(env, logger)             
        {
            _db = db;
        }
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();  

           
            var http = Context.GetHttpContext()!;
            if (http.Request.Query.TryGetValue("userId", out var val) &&
                int.TryParse(val.First(), out var userId))
            {
                var groupIds = _db.GroupMembers
                                  .Where(gm => gm.UserId == userId && !gm.Group.IsDeleted)
                                  .Select(gm => gm.GroupId);

                foreach (var gid in groupIds)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"group-{gid}");
            }
        }

        public override async Task OnDisconnectedAsync(System.Exception? ex)
        {
            
            await base.OnDisconnectedAsync(ex);
        }
    }
}
