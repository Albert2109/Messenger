using Messanger.Models.ViewModels;

namespace Messanger.Services
{
    public class AvatarSizeHandler : HandlerBase<RegisterViewModel>
    {
        private const long MaxBytes = 2 * 1024 * 1024; 
        protected override Task<string?> ProcessAsync(RegisterViewModel model)
        {
            if (model.Ava != null && model.Ava.Length > MaxBytes)
                return Task.FromResult("Файл аватару надто великий (макс 2 MB)");
            return Task.FromResult<string?>(null);
        }
    }
}
