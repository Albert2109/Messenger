using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Messanger.Models;

namespace Messanger.Controllers
{
    public class ChatController : Controller
    {
        private readonly MessengerContext _context;
        public ChatController(MessengerContext context) => _context = context;

        [HttpGet]
        public JsonResult SearchUsers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(Array.Empty<object>());

            var users = _context.Users
                .Where(u => u.Login.Contains(query) || u.Email.Contains(query))
                .Select(u => new {
                    id = u.UserId,
                    userName = u.Login,
                    email = u.Email
                })
                .ToList();
            return Json(users);
        }
    }
}