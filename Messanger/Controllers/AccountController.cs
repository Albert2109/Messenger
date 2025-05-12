using Messanger.Hubs;
using Messanger.Model;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Messanger.Controllers
{
    public class AccountController : Controller
    {
        private readonly MessengerContext _messengerContext;
        private readonly ILogger<AccountController> _logger;
        private readonly IHubContext<ProfileHub> _profileHub;
        public AccountController(MessengerContext messengerContext, ILogger<AccountController> logger, IHubContext<ProfileHub> profileHub)
        {
            _messengerContext = messengerContext;
            _logger = logger;
            _profileHub = profileHub;
        }

        [HttpGet]
        public IActionResult Avtorization()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Registration()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registration(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Модель не валідна");
                return View(model);
            }

            if (_messengerContext.users.Any(u => u.Email == model.EmailAddress))
            {
                ModelState.TryAddModelError("EmailAddress", "Електронна пошта вже використовується");
                _logger.LogWarning("Спроба реєстрації з вже існуючою електронною поштою: {Email}", model.EmailAddress);
                return View();
            }

            if (model.Password != model.PovtorPassword)
            {
                ModelState.AddModelError("PovtorPassword", "Паролі не однакові");
                _logger.LogWarning("Не однакові паролі {Password}", model.Password);
                return View();
            }

            string relativePath = null;
            if (model.Ava != null && model.Ava.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Ava.FileName)}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Ava.CopyToAsync(stream);
                }

                
                relativePath = "/uploads/" + uniqueFileName;
            }

            var user = new Users
            {
                Email = model.EmailAddress,
                Login = model.Login,
                password = HashPassword(model.Password),
                ava = relativePath 
            };

            _messengerContext.users.Add(user);
            await _messengerContext.SaveChangesAsync();

            _logger.LogInformation("Успішна реєстрація користувача: {Email}", model.EmailAddress);

            return RedirectToAction("Avtorization", "Account");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Avtorization(AvtorizationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Модель не валідна при авторизації");
                return View(model);
            }

            var user = _messengerContext.users
                .FirstOrDefault(u => u.Email == model.EmailAddress);

            var hashpassword = HashPassword(model.Password);

            if (user == null || user.password != hashpassword)
            {
                ModelState.AddModelError(string.Empty, "Неправильна електронна пошта або пароль");
                _logger.LogWarning("Невдала спроба логіну: {Email}", model.EmailAddress);
                return View(model);
            }

            
            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("Login", user.Login);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Ava", user.ava ?? "");

            _logger.LogInformation("Користувач {Email} увійшов у систему.", user.Email);
            return RedirectToAction("Index", "MessangerHome");
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile(int id)
        {
            var user = await _messengerContext.users.FindAsync(id);
            if (user == null) return NotFound();
            return Json(new
            {
                user.UserId,
                user.Login,
                user.Email,
                Ava = user.ava ?? "/images/default-avatar.png"
            });
        }
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _messengerContext.users.FindAsync(model.UserId);
            if (user == null) return NotFound();

           
            if (model.AvaFile != null)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                Directory.CreateDirectory(uploads);
                var fn = $"{Guid.NewGuid()}{Path.GetExtension(model.AvaFile.FileName)}";
                using var fs = new FileStream(Path.Combine(uploads, fn), FileMode.Create);
                await model.AvaFile.CopyToAsync(fs);
                user.ava = "/uploads/" + fn;
            }

            user.Login = model.Login;
            user.Email = model.Email;
            await _messengerContext.SaveChangesAsync();

         
            HttpContext.Session.SetString("Login", user.Login);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Ava", user.ava ?? "");

          
            await _profileHub.Clients.All.SendAsync(
                "ProfileUpdated",
                user.UserId,
                user.Login,
                user.Email,
                user.ava);

            return Ok();
        }



        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

    }
}
