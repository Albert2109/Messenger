using System;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using Messanger.Models;
using Messanger.Models.Notifications;
using Messanger.Models.ViewModels;
using Messanger.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Controllers
{
    public class MessangerHomeController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<MessangerHomeController> _log;
        private readonly IWebHostEnvironment _env;
        private readonly IChatNotifier _notifier;

        public MessangerHomeController(
            MessengerContext db,
            ILogger<MessangerHomeController> log,
            IWebHostEnvironment env,
            IChatNotifier notifier)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? chatId)
        {
            var me = GetCurrentUserId();

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
                .Select(gm => new GroupViewModel
                {
                    GroupId = gm.GroupId,
                    Name = gm.Group.Name,
                    Avatar = gm.Group.AvatarUrl ?? "/images/default-group.png",
                    LastAt = _db.Messages
                                  .Where(ms => ms.GroupId == gm.GroupId)
                                  .OrderByDescending(ms => ms.CreatedAt)
                                  .Select(ms => (DateTime?)ms.CreatedAt)
                                  .FirstOrDefault()
                               ?? gm.Group.CreatedAt
                })
                .OrderByDescending(x => x.LastAt)
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
            var me = GetCurrentUserId();
            var login = HttpContext.Session.GetString("Login")!;
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
            var dto = new PrivateMessageDto
            {
                SenderId = me,
                RecipientId = chatId,
                Login = login,
                Avatar = ava,
                Text = text,
                Timestamp = timestamp
            };
            await _notifier.NotifyPrivateMessageAsync(dto);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(int chatId, IFormFile file)
        {
            var me = GetCurrentUserId();
            if (file == null || file.Length == 0)
                return BadRequest();
            var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";
            var login = HttpContext.Session.GetString("Login")!;
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var unique = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(uploads, unique);
            await using var fs = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(fs);
            var url = Url.Action("Download", "MessangerHome",
                                 new { file = unique, name = file.FileName })!;
            var msg = new Message
            {
                UserId = me,
                RecipientId = chatId,
                FileUrl = url,
                FileName = file.FileName,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();
            var timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm");
            var dto = new PrivateFileDto
            {
                SenderId = me,
                RecipientId = chatId,
                Login = login,
                Avatar = ava,
                FileUrl = url,
                FileName = file.FileName,
                Timestamp = timestamp
            };
            await _notifier.NotifyPrivateFileAsync(dto);


            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();

            var me = GetCurrentUserId();
            if (msg.GroupId.HasValue)
                return BadRequest();

            var other = msg.UserId == me
                ? msg.RecipientId
                : msg.UserId;

            _db.Messages.Remove(msg);
            await _db.SaveChangesAsync();
            var dto = new PrivateDeletionDto
            {
                MessageId = id,
                CurrentUserId = me,
                OtherUserId = other!.Value
            };
            await _notifier.NotifyPrivateDeletionAsync(dto);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> EditMessage(int id, string newText)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();

            var me = GetCurrentUserId();
            if (msg.GroupId.HasValue)
                return BadRequest();

            var other = msg.UserId == me
                ? msg.RecipientId
                : msg.UserId;

            msg.Text = newText;
            await _db.SaveChangesAsync();
            var dto = new PrivateEditDto
            {
                MessageId = id,
                NewText = newText,
                CurrentUserId = me,
                OtherUserId = other!.Value
            };
            await _notifier.NotifyPrivateEditAsync(dto);

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
        private int GetCurrentUserId()
        {
            var idString = HttpContext.Session.GetString("UserId")
                           ?? throw new InvalidOperationException("UserId is missing in session.");
            if (!int.TryParse(idString, out var userId))
                throw new InvalidOperationException($"Invalid UserId value in session: '{idString}'.");

            return userId;
        }
    }
}
