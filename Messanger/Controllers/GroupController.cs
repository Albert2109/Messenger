using Messanger.Models;
using Messanger.Models.Notifications;
using Messanger.Models.ViewModels;
using Messanger.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Controllers;

[Route("Group")]
public class GroupController : Controller
{
    private readonly MessengerContext _db;
    private readonly IHubContext<GroupHub> _hub;
    private readonly IChatNotifier _notifier;
    private readonly IFileService _fileService;

    public GroupController(MessengerContext db, IHubContext<GroupHub> hub, IChatNotifier notifier, 
        IFileService fileService)
    {
        _db = db;
        _hub = hub;
        _notifier = notifier;
        _fileService = fileService;
    }


    [HttpPost("Create")]
    public async Task<IActionResult> Create(string name, IFormFile? avatar, [FromForm] int[] memberIds)
    {
        var ownerId = GetCurrentUserId();
        var avatarUrl = avatar is null ? null : await _fileService.SaveAsync(avatar, "groups");

        var g = new Group { Name = name, AvatarUrl = avatarUrl, OwnerId = ownerId };
        _db.Groups.Add(g);
        await _db.SaveChangesAsync();

        var ids = memberIds.Append(ownerId).Distinct();
        foreach (var uid in ids)
        {
            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = g.GroupId,
                UserId = uid,
                Role = uid == ownerId ? GroupRole.Owner : GroupRole.Member
            });
        }
        await _db.SaveChangesAsync();

        await _hub.Groups.AddToGroupAsync(ownerId.ToString(), $"group-{g.GroupId}");
        await _hub.Clients.Groups(ids.Select(i => i.ToString())).SendAsync("GroupCreated", g.GroupId, g.Name, g.AvatarUrl);
        await _hub.Clients.Group($"group-{g.GroupId}").SendAsync("GroupMemberAdded", g.GroupId, ownerId);

        return Ok(new { g.GroupId });
    }


    [HttpPost("{groupId:int}/AddMember")]
    public async Task<IActionResult> AddMember(int groupId, int userId)
    {
        var currentId = GetCurrentUserId();

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


    [HttpPost("{groupId:int}/RemoveMember")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId)
    {
        var currentId = GetCurrentUserId();

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


    [HttpPost("{groupId:int}/Rename")]
    public async Task<IActionResult> Rename(int groupId, string name)
    {
        var currentId = GetCurrentUserId();

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


    [HttpPost("{groupId:int}/Avatar")]
    public async Task<IActionResult> ChangeAvatar(int groupId, IFormFile file)
    {
        var currentId = GetCurrentUserId();
        var gm = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == currentId && !m.IsRemoved);
        if (gm is null || gm.Role > GroupRole.Admin) return Forbid();

        var g = await _db.Groups.FindAsync(groupId);
        if (g is null) return NotFound();

        g.AvatarUrl = await _fileService.SaveAsync(file, "groups");
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}").SendAsync("GroupAvatarChanged", groupId, g.AvatarUrl);

        return Ok();
    }


    [HttpPost("{groupId:int}/TransferOwner")]
    public async Task<IActionResult> TransferOwner(int groupId, int newOwnerId)
    {
        var currentId = GetCurrentUserId();

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


    [HttpPost("{groupId:int}/Leave")]
    public async Task<IActionResult> Leave(int groupId)
    {
        var currentId = GetCurrentUserId();

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


    [HttpPost("{groupId:int}/Delete")]
    public async Task<IActionResult> Delete(int groupId)
    {
        var currentId = GetCurrentUserId();

        var g = await _db.Groups.FindAsync(groupId);
        if (g is null) return NotFound();
        if (g.OwnerId != currentId) return Forbid();

        g.IsDeleted = true;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"group-{groupId}")
                 .SendAsync("GroupDeleted", groupId);
        return Ok();
    }
    [HttpGet("Chat/{groupId:int}")]
    public async Task<IActionResult> Chat(int groupId)
    {
        var userId = GetCurrentUserId();


        var vm = new HomePageViewModel
        {
            CurrentUserId = userId,
            CurrentUserLogin = HttpContext.Session.GetString("Login") ?? "",
            CurrentUserEmail = HttpContext.Session.GetString("Email") ?? "",
            CurrentUserAva = HttpContext.Session.GetString("Ava"),
            SelectedGroupId = groupId
        };


        var allMessages = await _db.Messages
            .Where(m => m.GroupId == null && (m.UserId == userId || m.RecipientId == userId))
            .Select(m => new {
                OtherId = m.UserId == userId ? m.RecipientId : m.UserId,
                OtherLogin = m.UserId == userId ? m.Recipient.Login : m.User.Login,
                m.Text,
                m.CreatedAt
            })
            .ToListAsync();


        vm.Chats = allMessages
            .GroupBy(x => x.OtherId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ChatViewModel
            {
                UserId = (int)x.OtherId,
                Login = x.OtherLogin,
                LastMessage = x.Text,
                LastAt = x.CreatedAt
            })
            .ToList();


        vm.Groups = await _db.GroupMembers
            .Where(gm => gm.UserId == userId && !gm.IsRemoved && !gm.Group.IsDeleted)
            .Select(gm => new {
                gm.GroupId,
                gm.Group.Name,
                Avatar = gm.Group.AvatarUrl ?? "/images/default-group.png",
                LastAt = _db.Messages
                            .Where(ms => ms.GroupId == gm.GroupId)
                            .OrderByDescending(ms => ms.CreatedAt)
                            .Select(ms => (DateTime?)ms.CreatedAt)
                            .FirstOrDefault() ?? gm.Group.CreatedAt,
                Role = gm.Role.ToString()
            })
            .OrderByDescending(x => x.LastAt)
            .Select(x => new GroupViewModel
            {
                GroupId = x.GroupId,
                Name = x.Name,
                Avatar = x.Avatar,
                LastAt = x.LastAt,
                Role = x.Role
            })
            .ToListAsync();


        vm.Messages = await _db.Messages
            .Where(m => m.GroupId == groupId)
            .Include(m => m.User)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageViewModel
            {
                Id = m.Id,
                UserId = m.UserId,
                UserLogin = m.User.Login,
                UserAvatar = m.User.ava ?? "/images/default-avatar.png",
                Text = m.Text,
                FileUrl = m.FileUrl,
                FileName = m.FileName,
                CreatedAt = m.CreatedAt,
                IsOwn = m.UserId == userId
            })
            .ToListAsync();

        return View("Chat", vm);
    }

    [HttpPost("{groupId:int}/SendMessage")]
    public async Task<IActionResult> SendMessage(int groupId, string text)
    {
        var me = GetCurrentUserId();
        var login = HttpContext.Session.GetString("Login")!;
        var email = HttpContext.Session.GetString("Email")!;
        var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";

        var msg = new Message
        {
            UserId = me,
            GroupId = groupId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm");


        var dto = new GroupMessageDto
        {
            MessageId = msg.Id,
            GroupId = groupId,
            SenderId = me,
            Login = login,
            Email = email,
            Avatar = ava,
            Text = text,
            Timestamp = timestamp
        };
        await _notifier.NotifyGroupMessageAsync(dto);

        return Ok();
    }

    [HttpPost("{groupId:int}/UploadFile")]
    public async Task<IActionResult> UploadFile(int groupId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest();

        var me = GetCurrentUserId();
        var relativeUrl = await _fileService.SaveAsync(file, "groups");

        var msg = new Message
        {
            UserId = me,
            GroupId = groupId,
            FileUrl = relativeUrl,
            FileName = file.FileName,
            CreatedAt = DateTime.UtcNow
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new GroupFileDto
        {
            MessageId = msg.Id,
            GroupId = groupId,
            SenderId = me,
            Login = HttpContext.Session.GetString("Login")!,
            Email = HttpContext.Session.GetString("Email")!,
            Avatar = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png",
            FileUrl = relativeUrl,
            FileName = file.FileName,
            Timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm")
        };
        await _notifier.NotifyGroupFileAsync(dto);

        return Ok();
    }

    [HttpPost("DeleteMessage/{id:int}")]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var msg = await _db.Messages.FindAsync(id);
        if (msg == null || !msg.GroupId.HasValue)
            return NotFound();

        var me = GetCurrentUserId();

        _db.Messages.Remove(msg);
        await _db.SaveChangesAsync();


        var dto = new GroupDeletionDto
        {
            GroupId = msg.GroupId.Value,
            MessageId = id
        };
        await _notifier.NotifyGroupDeletionAsync(dto);

        return Ok();
    }


    [HttpPost("EditMessage/{id:int}")]
    public async Task<IActionResult> EditMessage(int id, string newText)
    {
        var msg = await _db.Messages.FindAsync(id);
        if (msg == null || !msg.GroupId.HasValue)
            return NotFound();

        var me = GetCurrentUserId();


        msg.Text = newText;
        await _db.SaveChangesAsync();


        var dto = new GroupEditDto
        {
            GroupId = msg.GroupId.Value,
            MessageId = id,
            NewText = newText
        };
        await _notifier.NotifyGroupEditAsync(dto);

        return Ok();
    }



    [HttpGet("{groupId:int}/Download")]
    public IActionResult Download(int groupId, string file, string name) => _fileService.Serve($"/uploads/{file}", name);

    [HttpGet("{groupId:int}/Members")]
    public async Task<IActionResult> Members(int groupId)
    {

        var members = await _db.GroupMembers
            .Where(gm => gm.GroupId == groupId && !gm.IsRemoved)
            .Include(gm => gm.User)
            .Select(gm => new {
                id = gm.UserId,
                login = gm.User.Login,
                role = gm.Role.ToString(),
                avatar = gm.User.ava ?? "/images/default-avatar.png"
            })
            .ToListAsync();

        return Json(members);
    }
    private int GetCurrentUserId()
    {
        var idString = HttpContext.Session.GetString("UserId")
                       ?? throw new InvalidOperationException("UserId is missing in session.");
        if (!int.TryParse(idString, out var userId))
            throw new InvalidOperationException($"Invalid UserId value in session: '{idString}'.");

        return userId;
    }
}