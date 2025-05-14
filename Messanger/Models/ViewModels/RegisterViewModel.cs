using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Messanger.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required, EmailAddress]
        public string EmailAddress { get; set; }

        [Required, StringLength(50)]
        public string Login { get; set; }

        [Required]
        public IFormFile Ava { get; set; }  

        [Required]
        public string Password { get; set; }

        [Required]
        public string PovtorPassword { get; set; }
    }
}
