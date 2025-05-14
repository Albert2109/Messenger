using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Messanger.Hubs;
using Messanger.Models;
using Messanger.Models.ViewModels;

namespace Messanger.Controllers
{
    public class MessangerHomeController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<MessangerHomeController> _log;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<ChatHub> _hub;

        public MessangerHomeController(
            MessengerContext db,
            ILogger<MessangerHomeController> log,
            IWebHostEnvironment env,
            IHubContext<ChatHub> hub)
        {
            _db = db;
            _log = log;
            _env = env;
            _hub = hub;
        }

        

        [HttpGet]
        public async Task<IActionResult> Index(int? chatId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var me))
                return RedirectToAction("Avtorization", "Account");

            var vm = new HomePageViewModel
            {
                CurrentUserId = me,
                CurrentUserLogin = HttpContext.Session.GetString("Login") ?? "",
                CurrentUserEmail = HttpContext.Session.GetString("Email") ?? "",
                CurrentUserAva = HttpContext.Session.GetString("Ava"),
                SelectedChatId = chatId,
                SelectedGroupId = null
            };


            vm.Chats = await _db.Users
                .Where(u => _db.Messages.Any(m =>
                    (m.UserId == me && m.RecipientId == u.UserId) ||
                    (m.UserId == u.UserId && m.RecipientId == me)))
                .Select(u => new ChatViewModel
                {
                    UserId = u.UserId,
                    Login = u.Login,
                    LastMessage = _db.Messages
                        .Where(m =>
                            (m.UserId == me && m.RecipientId == u.UserId) ||
                            (m.UserId == u.UserId && m.RecipientId == me))
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Text)
                        .FirstOrDefault(),
                    LastAt = _db.Messages
                        .Where(m =>
                            (m.UserId == me && m.RecipientId == u.UserId) ||
                            (m.UserId == u.UserId && m.RecipientId == me))
                        .Max(m => m.CreatedAt)
                })
                .OrderByDescending(c => c.LastAt)
                .ToListAsync();


            vm.Groups = await _db.GroupMembers
                .Where(gm => gm.UserId == me && !gm.IsRemoved && !gm.Group.IsDeleted)
                .Select(gm => new {
                    gm.GroupId,
                    gm.Group.Name,
                    Avatar = gm.Group.AvatarUrl ?? "/images/default-group.png",
                    LastAt = _db.Messages
                                .Where(ms => ms.GroupId == gm.GroupId)
                                .OrderByDescending(ms => ms.CreatedAt)
                                .Select(ms => (DateTime?)ms.CreatedAt)
                                .FirstOrDefault()
                             ?? gm.Group.CreatedAt
                })
                .OrderByDescending(x => x.LastAt)
                .Select(x => new GroupViewModel
                {
                    GroupId = x.GroupId,
                    Name = x.Name,
                    Avatar = x.Avatar,
                    LastAt = x.LastAt
                })
                .ToListAsync();


            if (chatId.HasValue)
            {
                vm.Messages = await _db.Messages
                    .Where(m =>
                        m.GroupId == null &&
                        ((m.UserId == me && m.RecipientId == chatId) ||
                         (m.UserId == chatId && m.RecipientId == me)))
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
                        IsOwn = m.UserId == me
                    })
                    .ToListAsync();
            }

            return View(vm);
        }


        
        [HttpPost]
        public async Task<IActionResult> SendMessage(int chatId, string text)
        {
            var me = int.Parse(HttpContext.Session.GetString("UserId")!);
            var login = HttpContext.Session.GetString("Login")!;
            var email = HttpContext.Session.GetString("Email")!;
            var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";

            var msg = new Message
            {
                UserId = me,
                RecipientId = chatId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm");


            await _hub.Clients.Groups(me.ToString(), chatId.ToString())
          
           .SendAsync("ReceivePrivateMessage",
                      me,                      
                      login,
                      ava,
                      text,
                      timestamp);

            return Ok();
        }


        
        [HttpPost]
        public async Task<IActionResult> UploadFile(
            [FromForm] int chatId,
            IFormFile file)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var me))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest();

            var unique = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var path = Path.Combine(uploads, unique);
            await using var fs = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(fs);

            var url = Url.Action("Download", "MessangerHome",
                                 new { file = unique, name = file.FileName })!;

            var msg = new Message
            {
                UserId = me,
                RecipientId = chatId,
                GroupId = null,
                FileUrl = url,
                FileName = file.FileName,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var ts = msg.CreatedAt.ToLocalTime().ToString("HH:mm");

            await _hub.Clients.Groups(me.ToString(), chatId.ToString())
                      .SendAsync("ReceivePrivateFile",
                                 me, url, file.FileName, ts);

            return Ok();
        }

       
        [HttpPost]
        public Task<IActionResult> DeleteMessage(int id) =>
            MutatePrivate(id, (m, me) => _db.Messages.Remove(m), "MessageDeleted");

        [HttpPost]
        public Task<IActionResult> EditMessage(int id, string newText) =>
            MutatePrivate(id, (m, me) => m.Text = newText, "MessageEdited", newText);

        private async Task<IActionResult> MutatePrivate(
            int id,
            Action<Message, int> action,
            string hubEvent,
            params object[] args)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg is null) return NotFound();
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var me)
                || msg.UserId != me)
                return Unauthorized();


            if (msg.GroupId.HasValue)
                return BadRequest();

            action(msg, me);
            await _db.SaveChangesAsync();

            var other = msg.UserId == me
                ? msg.RecipientId
                : msg.UserId;

            if (args.Length == 1)
                await _hub.Clients.Groups(me.ToString(), other.ToString())
                          .SendAsync(hubEvent, id, args[0]);
            else
                await _hub.Clients.Groups(me.ToString(), other.ToString())
                          .SendAsync(hubEvent, id, args);


            return Ok();
        }

        
        [HttpGet]
        public IActionResult Download(string file, string name)
        {
            var path = Path.Combine(_env.WebRootPath, "uploads", file);
            return System.IO.File.Exists(path)
                ? PhysicalFile(path, "application/octet-stream", name)
                : NotFound();
        }
    }
}