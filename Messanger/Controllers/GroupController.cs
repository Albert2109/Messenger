using Messanger.Hubs;
using Messanger.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Controllers;

[Route("Group")]
public class GroupController : Controller
{
    private readonly MessengerContext _db;
    private readonly IHubContext<GroupHub> _hub;
    private readonly IWebHostEnvironment _env;

    public GroupController(MessengerContext db,
                           IHubContext<GroupHub> hub,
                           IWebHostEnvironment env)
    {
        _db = db;
        _hub = hub;
        _env = env;
    }

    // ─────────── CREATE ───────────
    [HttpPost("Create")]
    public async Task<IActionResult> Create(string name, IFormFile? avatar,
                                            [FromForm] int[] memberIds)
    {
        var ownerId = int.Parse(HttpContext.Session.GetString("UserId")!);
        var avatarUrl = avatar is null ? null : await SaveFile(avatar);

        var g = new Group { Name = name, AvatarUrl = avatarUrl, OwnerId = ownerId };
        _db.Groups.Add(g);
        await _db.SaveChangesAsync();

        var ids = memberIds.Append(ownerId).Distinct();
        foreach (var uid in ids)
            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = g.GroupId,
                UserId = uid,
                Role = uid == ownerId ? GroupRole.Owner : GroupRole.Member
            });
        await _db.SaveChangesAsync();

        await _hub.Clients.Groups(ids.Select(i => i.ToString()))
                 .SendAsync("GroupCreated", g.GroupId, g.Name, g.AvatarUrl);

        return Ok(new { g.GroupId });
    }

    // ─────────── ADD MEMBER ───────────
    [HttpPost("{groupId:int}/AddMember")]
    public async Task<IActionResult> AddMember(int groupId, int userId)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var me = await _db.GroupMembers
                 .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentId && !gm.IsRemoved);

        if (me is null || me.Role > GroupRole.Admin) return Forbid();
        if (await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && !gm.IsRemoved))
            return BadRequest("User already in group");

        _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId });
        await _db.SaveChangesAsync();

        await _hub.Groups.AddToGroupAsync(userId.ToString(), $"group-{groupId}");
        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupMemberAdded", groupId, userId);
        return Ok();
    }

    // ─────────── REMOVE MEMBER ───────────
    [HttpPost("{groupId:int}/RemoveMember")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var me = await _db.GroupMembers.FirstOrDefaultAsync(g =>
                       g.GroupId == groupId && g.UserId == currentId && !g.IsRemoved);
        var victim = await _db.GroupMembers.FirstOrDefaultAsync(g =>
                       g.GroupId == groupId && g.UserId == userId && !g.IsRemoved);

        if (me is null || victim is null) return NotFound();
        if (me.Role > GroupRole.Admin || victim.Role == GroupRole.Owner)
            return Forbid();

        victim.IsRemoved = true;
        await _db.SaveChangesAsync();

        await _hub.Groups.RemoveFromGroupAsync(userId.ToString(), $"group-{groupId}");
        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupMemberRemoved", groupId, userId);
        return Ok();
    }

    // ─────────── RENAME ───────────
    [HttpPost("{groupId:int}/Rename")]
    public async Task<IActionResult> Rename(int groupId, string name)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var gm = await _db.GroupMembers.FirstOrDefaultAsync(m =>
                 m.GroupId == groupId && m.UserId == currentId && !m.IsRemoved);

        if (gm is null || gm.Role > GroupRole.Admin) return Forbid();

        var g = await _db.Groups.FindAsync(groupId);
        if (g is null) return NotFound();

        g.Name = name;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupRenamed", groupId, name);
        return Ok();
    }

    // ─────────── CHANGE AVATAR ───────────
    [HttpPost("{groupId:int}/Avatar")]
    public async Task<IActionResult> ChangeAvatar(int groupId, IFormFile file)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var gm = await _db.GroupMembers.FirstOrDefaultAsync(m =>
                 m.GroupId == groupId && m.UserId == currentId && !m.IsRemoved);
        if (gm is null || gm.Role > GroupRole.Admin) return Forbid();

        var g = await _db.Groups.FindAsync(groupId);
        if (g is null) return NotFound();

        g.AvatarUrl = await SaveFile(file);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupAvatarChanged", groupId, g.AvatarUrl);
        return Ok();
    }

    // ─────────── TRANSFER OWNER ───────────
    [HttpPost("{groupId:int}/TransferOwner")]
    public async Task<IActionResult> TransferOwner(int groupId, int newOwnerId)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var owner = await _db.GroupMembers.FirstOrDefaultAsync(m =>
                     m.GroupId == groupId && m.UserId == currentId && m.Role == GroupRole.Owner);
        var target = await _db.GroupMembers.FirstOrDefaultAsync(m =>
                     m.GroupId == groupId && m.UserId == newOwnerId && !m.IsRemoved);

        if (owner is null || target is null) return Forbid();

        owner.Role = GroupRole.Admin;
        target.Role = GroupRole.Owner;

        var g = await _db.Groups.FindAsync(groupId);
        g!.OwnerId = newOwnerId;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupOwnerTransferred", groupId, newOwnerId);
        return Ok();
    }

    // ─────────── LEAVE ───────────
    [HttpPost("{groupId:int}/Leave")]
    public async Task<IActionResult> Leave(int groupId)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var gm = await _db.GroupMembers.FirstOrDefaultAsync(m =>
                 m.GroupId == groupId && m.UserId == currentId && !m.IsRemoved);
        if (gm is null) return NotFound();
        if (gm.Role == GroupRole.Owner)
            return BadRequest("Owner must transfer ownership before leaving");

        gm.IsRemoved = true;
        await _db.SaveChangesAsync();

        await _hub.Groups.RemoveFromGroupAsync(currentId.ToString(), $"group-{groupId}");
        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupMemberLeft", groupId, currentId);
        return Ok();
    }

    // ─────────── DELETE ───────────
    [HttpPost("{groupId:int}/Delete")]
    public async Task<IActionResult> Delete(int groupId)
    {
        var currentId = int.Parse(HttpContext.Session.GetString("UserId")!);

        var g = await _db.Groups.FindAsync(groupId);
        if (g is null) return NotFound();
        if (g.OwnerId != currentId) return Forbid();

        g.IsDeleted = true;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupDeleted", groupId);
        return Ok();
    }

    // ─────────── util ───────────
    private async Task<string> SaveFile(IFormFile f)
    {
        var uploads = Path.Combine(_env.WebRootPath, "uploads", "groups");
        Directory.CreateDirectory(uploads);

        var unique = $"{Guid.NewGuid()}{Path.GetExtension(f.FileName)}";
        var path = Path.Combine(uploads, unique);

        await using var fs = new FileStream(path, FileMode.Create);
        await f.CopyToAsync(fs);

        return $"/uploads/groups/{unique}";
    }
}
