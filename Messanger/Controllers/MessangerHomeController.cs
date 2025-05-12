using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Messanger.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Linq;

namespace Messanger.Controllers
{
    public class MessangerHomeController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<MessangerHomeController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<ChatHub> _hubContext;

        public MessangerHomeController(
            MessengerContext db,
            ILogger<MessangerHomeController> logger,
            IWebHostEnvironment env,
            IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _logger = logger;
            _env = env;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? chatId)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            var vm = new HomePageViewModel
            {
                CurrentUserId = userId,
                CurrentUserLogin = HttpContext.Session.GetString("Login") ?? string.Empty,
                CurrentUserEmail = HttpContext.Session.GetString("Email") ?? string.Empty,
                CurrentUserAva = HttpContext.Session.GetString("Ava"),
                SelectedChatId = chatId
            };

            
            var chatList = await _db.Messages
                .Where(m => m.UserId == userId || m.RecipientId == userId)
                .Select(m => new
                {
                    OtherId = m.UserId == userId ? m.RecipientId : m.UserId,
                    OtherLogin = m.UserId == userId ? m.Recipient.Login : m.User.Login,
                    m.Text,
                    m.CreatedAt
                })
                .OrderByDescending(x => x.CreatedAt)
                .GroupBy(x => x.OtherId)
                .Select(g => g.First())
                .ToListAsync();

            vm.Chats = chatList.Select(x => new ChatViewModel
            {
                UserId = x.OtherId,
                Login = x.OtherLogin,
                LastMessage = x.Text,
                LastAt = x.CreatedAt
            }).ToList();

           
            if (chatId.HasValue)
            {
                vm.Messages = await _db.Messages
            .Where(m => (m.UserId == userId && m.RecipientId == chatId) ||
                        (m.UserId == chatId && m.RecipientId == userId))
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
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromForm] int chatId, [FromForm] string text)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var login = HttpContext.Session.GetString("Login") ?? "Anonymous";
            var email = HttpContext.Session.GetString("Email") ?? string.Empty;
            var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";
            _logger.LogInformation("SendMessage endpoint: chatId={chatId}, text={text}", chatId, text);
            var msg = new Message
            {
                UserId = userId,
                RecipientId = chatId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm");
            await _hubContext.Clients
    .Groups(new[] { chatId.ToString(), userId.ToString() })
    .SendAsync("ReceiveMessage", login, email, ava, text, timestamp);


            return Ok(new { login, email, ava, text, timestamp });
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile([FromForm] int chatId, IFormFile file)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest();

            var unique = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var path = Path.Combine(uploads, unique);
            await using var fs = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(fs);

            var downloadUrl = Url.Action(
                "Download", "MessangerHome",
                new { file = unique, name = file.FileName })!;

            var msg = new Message
            {
                UserId = userId,
                RecipientId = chatId,
                FileUrl = downloadUrl,
                FileName = file.FileName,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var login = HttpContext.Session.GetString("Login") ?? "Anonymous";
            var email = HttpContext.Session.GetString("Email") ?? string.Empty;
            var ava = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";
            var timestamp = msg.CreatedAt.ToLocalTime().ToString("HH:mm");

            await _hubContext.Clients
    .Groups(new[] { chatId.ToString(), userId.ToString() })
    .SendAsync("ReceivePrivateFile", login, email, ava, downloadUrl, file.FileName, timestamp);


            return Ok(new { downloadUrl, fileName = file.FileName, timestamp });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var currentUserId))
                return Unauthorized();

            if (msg.UserId != currentUserId) return Unauthorized();

          
            var otherId = msg.UserId == currentUserId ? msg.RecipientId : msg.UserId;

            _db.Messages.Remove(msg);
            await _db.SaveChangesAsync();

           
            await _hubContext.Clients
                .Groups(new[] { currentUserId.ToString(), otherId.ToString() })
                .SendAsync("MessageDeleted", id);

            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> EditMessage(int id, string newText)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var currentUserId))
                return Unauthorized();

            if (msg.UserId != currentUserId) return Unauthorized();

           
            var otherId = msg.UserId == currentUserId ? msg.RecipientId : msg.UserId;

            msg.Text = newText;
            _db.Messages.Update(msg);
            await _db.SaveChangesAsync();

           
            await _hubContext.Clients
                .Groups(new[] { currentUserId.ToString(), otherId.ToString() })
                .SendAsync("MessageEdited", id, newText);

            return Ok();
        }


        [HttpGet]
        public IActionResult Download(string file, string name)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            var path = Path.Combine(uploads, file);
            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/octet-stream", name);
        }
    }
}
