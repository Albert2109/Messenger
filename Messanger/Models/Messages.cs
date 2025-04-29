using Messanger.Model;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messanger.Models
{
    public class Message
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int RecipientId { get; set; }

        public string? Text { get; set; }         // можна надсилати тільки файл
        public string? FileUrl { get; set; }      // посилання на файл
        public string? FileName { get; set; }     // оригинальне ім'я

        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        [InverseProperty(nameof(Users.SentMessages))]
        public Users User { get; set; } = null!;

        [ForeignKey(nameof(RecipientId))]
        [InverseProperty(nameof(Users.ReceivedMessages))]
        public Users Recipient { get; set; } = null!;
    }
}
