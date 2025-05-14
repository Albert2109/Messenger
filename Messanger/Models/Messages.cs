using Messanger.Model;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messanger.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int UserId { get; set; }          
        public int? RecipientId { get; set; }        
        public int? GroupId { get; set; }       

        public string? Text { get; set; }
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; }

      
        public Users User { get; set; } = null!;
        public Users? Recipient { get; set; }
        public Group? Group { get; set; }
    }

}
