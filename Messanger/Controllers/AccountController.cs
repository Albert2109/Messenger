using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Messanger.Hubs;
using Messanger.Model;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Messanger.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Controllers
{
    public class AccountController : Controller
    {
        private readonly MessengerContext _db;
        private readonly ILogger<AccountController> _logger;
        private readonly IHubContext<ProfileHub> _profileHub;
        private readonly IEnumerable<IHandler<RegisterViewModel>> _registerHandlers;

        public AccountController(
            MessengerContext db,
            ILogger<AccountController> logger,
            IHubContext<ProfileHub> profileHub,
            IEnumerable<IHandler<RegisterViewModel>> registerHandlers)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _profileHub = profileHub ?? throw new ArgumentNullException(nameof(profileHub));
            _registerHandlers = registerHandlers ?? throw new ArgumentNullException(nameof(registerHandlers));
        }

       
        [HttpGet]
        public IActionResult Registration() => View();

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registration(RegisterViewModel model)
        {
            
            IHandler<RegisterViewModel>? first = null, prev = null;
            foreach (var handler in _registerHandlers)
            {
                if (first == null)
                    first = prev = handler;
                else
                    prev = prev.SetNext(handler);
            }

           
            var error = first is null
                ? null
                : await first.HandleAsync(model);

            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                _logger.LogWarning("Registration failed: {Error}", error);
                return View(model);
            }

            
            string? avatarPath = null;
            if (model.Ava != null && model.Ava.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Ava.FileName)}";
                var fullPath = Path.Combine(uploads, fileName);
                await using var fs = new FileStream(fullPath, FileMode.Create);
                await model.Ava.CopyToAsync(fs);

                avatarPath = "/uploads/" + fileName;
            }

            var newUser = new Users
            {
                Email = model.EmailAddress,
                Login = model.Login,
                password = HashPassword(model.Password),
                ava = avatarPath
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Successfully registered user: {Email}", model.EmailAddress);
            return RedirectToAction(nameof(Avtorization));
        }

        
        [HttpGet]
        public IActionResult Avtorization() => View();

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Avtorization(AvtorizationViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _db.Users.FirstOrDefault(u => u.Email == model.EmailAddress);
            var hashed = HashPassword(model.Password);

            if (user == null || user.password != hashed)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password");
                _logger.LogWarning("Failed login attempt: {Email}", model.EmailAddress);
                return View(model);
            }

            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("Login", user.Login);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Ava", user.ava ?? string.Empty);

            _logger.LogInformation("User {Email} logged in.", user.Email);
            return RedirectToAction("Index", "MessangerHome");
        }

       
        [HttpGet]
        public async Task<IActionResult> GetProfile(int id)
        {
            var user = await _db.Users.FindAsync(id);
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _db.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            if (model.AvaFile != null && model.AvaFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploads);

                var fn = $"{Guid.NewGuid()}{Path.GetExtension(model.AvaFile.FileName)}";
                await using var fs = new FileStream(Path.Combine(uploads, fn), FileMode.Create);
                await model.AvaFile.CopyToAsync(fs);
                user.ava = "/uploads/" + fn;
            }

            user.Login = model.Login;
            user.Email = model.Email;
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("Login", user.Login);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Ava", user.ava ?? string.Empty);

            await _profileHub.Clients.All.SendAsync(
                "ProfileUpdated",
                user.UserId,
                user.Login,
                user.Email,
                user.ava);

            return Ok();
        }

       
        [HttpGet]
        public async Task<IActionResult> Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(Array.Empty<object>());

            var users = await _db.Users
                .Where(u => u.Login.Contains(q) || u.Email.Contains(q))
                .Select(u => new
                {
                    id = u.UserId,
                    login = u.Login,
                    avatar = u.ava ?? "/images/default-avatar.png"
                })
                .Take(10)
                .ToListAsync();

            return Json(users);
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hashBytes = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
