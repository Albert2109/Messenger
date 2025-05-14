using Messanger.Model;

namespace Messanger.Models
{
    public class GroupMember
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public GroupRole Role { get; set; } = GroupRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsRemoved { get; set; }

        public Group Group { get; set; } = null!;
        public Users User { get; set; } = null!;
    }

    public enum GroupRole { Owner , Admin , Member  }
}
