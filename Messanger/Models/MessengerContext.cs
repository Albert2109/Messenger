using Messanger.Model;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Models
{
    public class MessengerContext : DbContext
    {
        public MessengerContext(DbContextOptions<MessengerContext> options)
            : base(options) { }

        public DbSet<Users> Users { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<GroupMember> GroupMembers { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

           
            b.Entity<Message>()
             .HasOne(m => m.User)
             .WithMany(u => u.SentMessages)
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            
            b.Entity<Message>()
             .HasOne(m => m.Recipient)
             .WithMany(u => u.ReceivedMessages)
             .HasForeignKey(m => m.RecipientId)
             .OnDelete(DeleteBehavior.Restrict);

            
            b.Entity<Message>()
             .HasOne(m => m.Group)
             .WithMany()                              
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);

           
            b.Entity<Group>()
             .HasOne(g => g.Owner)
             .WithMany()
             .HasForeignKey(g => g.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);

            
            b.Entity<GroupMember>()
             .HasKey(gm => new { gm.GroupId, gm.UserId });

            b.Entity<GroupMember>()
             .HasOne(gm => gm.Group)
             .WithMany(g => g.Members)
             .HasForeignKey(gm => gm.GroupId);

            b.Entity<GroupMember>()
             .HasOne(gm => gm.User)
             .WithMany()                              
             .HasForeignKey(gm => gm.UserId);
        }
    }
}
