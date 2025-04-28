using System.ComponentModel.DataAnnotations;

namespace Messanger.Models.ViewModels
{
    public class AvtorizationViewModel
    {
        [Required, EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

}
