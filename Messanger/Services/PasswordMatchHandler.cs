using Messanger.Models.ViewModels;

namespace Messanger.Services
{
    public class PasswordMatchHandler : HandlerBase<RegisterViewModel>
    {
        protected override Task<string?> ProcessAsync(RegisterViewModel model)
            => Task.FromResult(
                model.Password == model.PovtorPassword
                    ? null
                    : "Паролі не співпадають"
            );
    }
}
