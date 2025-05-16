using Messanger.Models.ViewModels;
using Messanger.Services;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Threading.Tasks;

namespace Messanger.Services
{
  
    public class ModelStateHandler : HandlerBase<RegisterViewModel>
    {
        private readonly IActionContextAccessor _actionContextAccessor;

        public ModelStateHandler(IActionContextAccessor actionContextAccessor)
        {
            _actionContextAccessor = actionContextAccessor;
        }

        protected override Task<string?> ProcessAsync(RegisterViewModel model)
        {
            var modelState = _actionContextAccessor
                                .ActionContext?
                                .ModelState;

            
            if (modelState == null || modelState.IsValid)
                return Task.FromResult<string?>(null);


            return Task.FromResult<string?>("Дані моделі невірні");
        }
    }
}