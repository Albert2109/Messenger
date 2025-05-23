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
        private readonly IChatNotifier _notifier;
        private readonly IFileService _fileService;


        public MessangerHomeController(MessengerContext db, IChatNotifier notifier, IFileService fileService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _fileService = fileService;
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

            vm.Chats = await LoadPrivateChats(me);
            vm.Groups = await LoadGroups(me);

            if (chatId.HasValue)
            {
                vm.Messages = await LoadPrivateMessages(me, chatId.Value);
            }

            return View(vm);
        }

        private async Task<List<ChatViewModel>> LoadPrivateChats(int currentUserId)
        {
            return await _db.Users
                .Where(u => _db.Messages.Any(m =>
                    (m.UserId == currentUserId && m.RecipientId == u.UserId) ||
                    (m.UserId == u.UserId && m.RecipientId == currentUserId)))
                .Select(u => new ChatViewModel
                {
                    UserId = u.UserId,
                    Login = u.Login,
                    LastMessage = _db.Messages
                        .Where(m =>
                            (m.UserId == currentUserId && m.RecipientId == u.UserId) ||
                            (m.UserId == u.UserId && m.RecipientId == currentUserId))
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Text)
                        .FirstOrDefault(),
                    LastAt = _db.Messages
                        .Where(m =>
                            (m.UserId == currentUserId && m.RecipientId == u.UserId) ||
                            (m.UserId == u.UserId && m.RecipientId == currentUserId))
                        .Max(m => m.CreatedAt)
                })
                .OrderByDescending(c => c.LastAt)
                .ToListAsync();
        }

        private async Task<List<GroupViewModel>> LoadGroups(int currentUserId)
        {
            return await _db.GroupMembers
                .Where(gm => gm.UserId == currentUserId && !gm.IsRemoved && !gm.Group.IsDeleted)
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
        }

        private async Task<List<ChatMessageViewModel>> LoadPrivateMessages(int currentUserId, int otherUserId)
        {
            return await _db.Messages
                .Where(m =>
                    m.GroupId == null &&
                    ((m.UserId == currentUserId && m.RecipientId == otherUserId) ||
                     (m.UserId == otherUserId && m.RecipientId == currentUserId)))
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
                    IsOwn = m.UserId == currentUserId
                })
                .ToListAsync();
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
            if (file == null || file.Length == 0)
                return BadRequest();

            var me = GetCurrentUserId();
            var login = HttpContext.Session.GetString("Login")!;
            var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";

            var relativeUrl = await _fileService.SaveAsync(file);
            var downloadName = file.FileName;

            var msg = new Message
            {
                UserId = me,
                RecipientId = chatId,
                FileUrl = relativeUrl,
                FileName = downloadName,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var dto = new PrivateFileDto
            {
                SenderId = me,
                RecipientId = chatId,
                Login = login,
                Avatar = ava,
                FileUrl = relativeUrl,
                FileName = downloadName,
                Timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm")
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
        public IActionResult Download(string file, string name) => _fileService.Serve($"/uploads/{file}", name);

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
