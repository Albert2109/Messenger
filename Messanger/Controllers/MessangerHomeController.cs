using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Messanger.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Messanger.Controllers
{
    public class MessangerHomeController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<MessangerHomeController> _logger;
        private readonly IWebHostEnvironment _env;

        public MessangerHomeController(
            MessengerContext db,
            ILogger<MessangerHomeController> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? chatId)
        {
            var userId = int.Parse(HttpContext.Session.GetString("UserId")!);
            var vm = new HomePageViewModel
            {
                CurrentUserId = userId,
                CurrentUserLogin = HttpContext.Session.GetString("Login")!,
                CurrentUserEmail = HttpContext.Session.GetString("Email")!,
                CurrentUserAva = HttpContext.Session.GetString("Ava"),
                SelectedChatId = chatId
            };

           
            var chatList = await _db.Messages
              .Where(m => m.UserId == userId || m.RecipientId == userId)
              .Select(m => new {
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
                        UserLogin = m.User.Login,
                        UserAvatar = m.User.ava ?? "/images/default-avatar.png",
                        Text = m.Text,
                        CreatedAt = m.CreatedAt
                    })
                    .ToListAsync();
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int chatId, string text)
        {
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            _db.Messages.Add(new Message
            {
                UserId = userId,
                RecipientId = chatId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return RedirectToAction("Index", new { chatId });
        }

        [HttpGet]
        public IActionResult Download(string file, string name)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            var path = Path.Combine(uploads, file);
            if (!System.IO.File.Exists(path))
                return NotFound();
            return PhysicalFile(path, "application/octet-stream", name);
        }

         [HttpPost]
        public async Task<IActionResult> UploadFile(
            int chatId,
            IFormFile file,
            [FromServices] IHubContext<ChatHub> hubContext)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

           
            var unique  = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var path    = Path.Combine(uploads, unique);
            await using (var fs = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(fs);

            
            var downloadUrl = Url.Action(
                "Download",
                "MessangerHome",
                new { file = unique, name = file.FileName })!;

           
            var senderName   = HttpContext.Session.GetString("Login") ?? string.Empty;
            var senderEmail  = HttpContext.Session.GetString("Email") ?? string.Empty;
            var senderAvatar = HttpContext.Session.GetString("Ava") ?? "/images/default-avatar.png";

           
            await hubContext
                .Clients
                .Group(chatId.ToString())
                .SendAsync("ReceivePrivateFile", senderName, senderEmail, senderAvatar, downloadUrl, file.FileName);

            
            return Ok(new { downloadUrl, fileName = file.FileName });
        }
    }
}
