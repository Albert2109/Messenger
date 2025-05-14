using Messanger.Model;

namespace Messanger.Models
{
    public class Group
    {
        public int GroupId { get; set; }
        public string Name { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public int OwnerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public Users Owner { get; set; } = null!;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}
