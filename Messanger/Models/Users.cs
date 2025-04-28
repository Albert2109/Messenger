using Messanger.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messanger.Model
{ 
    public class Users
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Login { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string? ava {  get; set; }

        [Required]
         public string  password { get; set; }
        [InverseProperty(nameof(Message.User))]
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();

        [InverseProperty(nameof(Message.Recipient))]
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
    }
}
