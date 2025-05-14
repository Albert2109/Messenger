    using Messanger.Hubs;
    using Messanger.Models;
    using Messanger.Models.ViewModels;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;

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

        
            var ids = memberIds.Append(ownerId).Distinct().ToArray();

            foreach (var uid in ids)
                _db.GroupMembers.Add(new GroupMember
                {
                    GroupId = g.GroupId,
                    UserId = uid,
                    Role = uid == ownerId ? GroupRole.Owner : GroupRole.Member
                });
            await _db.SaveChangesAsync();

            //---------------------------------------
    
            await _hub.Groups.AddToGroupAsync(ownerId.ToString(), $"group-{g.GroupId}");

        
       
            await _hub.Clients.Groups(ids.Select(i => i.ToString()))
                     .SendAsync("GroupCreated", g.GroupId, g.Name, g.AvatarUrl);

            await _hub.Clients.Group($"group-{g.GroupId}")
                     .SendAsync("GroupMemberAdded", g.GroupId, ownerId);

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
        [HttpGet("Chat/{groupId:int}")]
        public async Task<IActionResult> Chat(int groupId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                return RedirectToAction("Avtorization", "Account");

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
        // ─────────── SEND GROUP MESSAGE ───────────
        [HttpPost("{groupId:int}/SendMessage")]
        public async Task<IActionResult> SendMessage(int groupId, string text)
        {
            var me = int.Parse(HttpContext.Session.GetString("UserId")!);
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

            
            await _hub.Clients.Group($"group-{groupId}")
                      .SendAsync("ReceiveGroupMessage",
                                 msg.Id,    
                                 me,        
                                 login,
                                 email,
                                 ava,
                                 text,
                                 timestamp);

            return Ok();
        }
        // ─────────── UPLOAD GROUP FILE ───────────
        [HttpPost("{groupId:int}/UploadFile")]
        public async Task<IActionResult> UploadFile(int groupId, IFormFile file)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var me))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest();

           
            var unique = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);
            var path = Path.Combine(uploadsRoot, unique);
            await using var fs = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(fs);

            
            var url = Url.Action("Download", "Group", new { groupId, file = unique, name = file.FileName })!;

            
            var msg = new Message
            {
                UserId = me,
                GroupId = groupId,
                FileUrl = url,
                FileName = file.FileName,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var ts = msg.CreatedAt.ToLocalTime().ToString("HH:mm");

            
            await _hub.Clients.Group($"group-{groupId}")
                .SendAsync("ReceiveGroupFile",
                           msg.Id, me,
                           HttpContext.Session.GetString("Login")!,
                           HttpContext.Session.GetString("Email")!,
                           HttpContext.Session.GetString("Ava")!,
                           url, file.FileName, ts);

            return Ok();
        }

        // ─────────── DELETE GROUP MESSAGE ───────────
        [HttpPost("DeleteMessage/{id:int}")]
        public Task<IActionResult> DeleteMessage(int id)
            => MutateGroup(id,
                  (m, me) => _db.Messages.Remove(m),
                  "GroupMessageDeleted");

        // ─────────── EDIT GROUP MESSAGE ───────────
        [HttpPost("EditMessage/{id:int}")]
        public Task<IActionResult> EditMessage(int id, string newText)
            => MutateGroup(id,
                  (m, me) => m.Text = newText,
                  "GroupMessageEdited", newText);

        
        private async Task<IActionResult> MutateGroup(
            int id,
            Action<Message, int> action,
            string hubEvent,
            params object[] args)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg is null || !msg.GroupId.HasValue)
                return NotFound();

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var me)
                || msg.UserId != me)
                return Unauthorized();

            action(msg, me);
            await _db.SaveChangesAsync();


        if (args.Length > 0)
                    {
            var text = args[0];
            await _hub.Clients.Group($"group-{msg.GroupId}")
                            .SendAsync(hubEvent, id, text);
                   }
                else
                    {
            await _hub.Clients.Group($"group-{msg.GroupId}")
                           .SendAsync(hubEvent, id);
                    }

        return Ok();
        }
        [HttpGet("{groupId:int}/Download")]
        public IActionResult Download(int groupId, string file, string name)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            var path = Path.Combine(uploads, file);
            if (!System.IO.File.Exists(path))
                return NotFound();
            return PhysicalFile(path, "application/octet-stream", name);
        }
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
}
