using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Controllers
{
    public class MessangerHomeController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<MessangerHomeController> _logger;

        public MessangerHomeController(MessengerContext db, ILogger<MessangerHomeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? chatId)
        {
           
            var userId = int.Parse(HttpContext.Session.GetString("UserId")!);
            var login = HttpContext.Session.GetString("Login")!;
            var email = HttpContext.Session.GetString("Email")!;
            var ava = HttpContext.Session.GetString("Ava"); 

            
            var chatQuery = _db.Messages
                .Where(m => m.UserId == userId || m.RecipientId == userId)
                .Select(m => new {
                    OtherId = m.UserId == userId ? m.RecipientId : m.UserId,
                    OtherLogin = m.UserId == userId ? m.Recipient.Login : m.User.Login,
                    m.Text,
                    m.CreatedAt
                });

            
            var chatGroups = await chatQuery
                .OrderByDescending(x => x.CreatedAt)
                .GroupBy(x => x.OtherId)
                .Select(g => g.First())
                .ToListAsync();

            var chatVms = chatGroups
                .Select(x => new ChatViewModel
                {
                    UserId = x.OtherId,
                    Login = x.OtherLogin,
                    LastMessage = x.Text,
                    LastAt = x.CreatedAt
                })
                .ToList();

            
            var msgs = new List<ChatMessageViewModel>();
            if (chatId.HasValue)
            {
                msgs = await _db.Messages
                    .Where(m => (m.UserId == userId && m.RecipientId == chatId)
                             || (m.UserId == chatId && m.RecipientId == userId))
                    .Include(m => m.User)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageViewModel
                    {
                        UserLogin = m.User.Login,
                        Text = m.Text,
                        CreatedAt = m.CreatedAt
                    })
                    .ToListAsync();
            }

            
            var vm = new HomePageViewModel
            {
                CurrentUserId = userId,
                CurrentUserLogin = login,
                CurrentUserEmail = email,
                CurrentUserAva = ava,
                Chats = chatVms,
                Messages = msgs,
                SelectedChatId = chatId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int chatId, string text)
        {
            var userId = int.Parse(HttpContext.Session.GetString("UserId")!);

            var msg = new Message
            {
                UserId = userId,
                RecipientId = chatId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };

            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", new { chatId });
        }
    }
}
