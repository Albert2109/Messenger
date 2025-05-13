using Messanger.Model;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messanger.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int UserId { get; set; }          // відправник
        public int? RecipientId { get; set; }        // приватний чат   (nullable)
        public int? GroupId { get; set; }        // груповий чат    (nullable)

        public string? Text { get; set; }
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; }

        /* навігаційні */
        public Users User { get; set; } = null!;
        public Users? Recipient { get; set; }
        public Group? Group { get; set; }
    }

}
