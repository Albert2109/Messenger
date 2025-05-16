using Messanger.Models.ViewModels;
using Messanger.Models;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Services
{
    public class DuplicateEmailHandler : HandlerBase<RegisterViewModel>
    {
        private readonly MessengerContext _db;
        public DuplicateEmailHandler(MessengerContext db) => _db = db;

        protected override async Task<string?> ProcessAsync(RegisterViewModel model)
        {
            bool exists = await _db.Users.AnyAsync(u => u.Email == model.EmailAddress);
            return exists
                ? "Електронна пошта вже використовується"
                : null;
        }
    }
}
