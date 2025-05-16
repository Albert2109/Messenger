// Hubs/GroupHub.cs
using Messanger.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class GroupHub : BaseHub
{
    private readonly MessengerContext _db;
    public GroupHub(MessengerContext db, ILogger<GroupHub> log) : base(log) => _db = db;

    
    protected override async Task<IEnumerable<string>> ResolveExtraGroups(int uid)
        => await _db.GroupMembers
                    .Where(gm => gm.UserId == uid && !gm.Group.IsDeleted && !gm.IsRemoved)
                    .Select(gm => $"group-{gm.GroupId}")
                    .ToListAsync();
}
