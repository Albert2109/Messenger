using System.ComponentModel.DataAnnotations;

namespace Messanger.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string Login { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        public IFormFile? AvaFile { get; set; }
    }
}
