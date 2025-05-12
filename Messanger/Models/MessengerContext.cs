using Messanger.Model;
using Microsoft.EntityFrameworkCore;

namespace Messanger.Models
{
    public class MessengerContext:DbContext
    {
        public MessengerContext(DbContextOptions<MessengerContext> options)
           : base(options)
        {
        }
        public DbSet<Users> users { get; set; }
        public DbSet<Message> Messages { get; set; }

        public DbSet<Group> Groups { get; set; } = null!;       
        public DbSet<GroupMember> GroupMembers { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

           
            modelBuilder.Entity<Message>()
                .HasOne(m => m.User)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Recipient)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Group>()
    .HasOne(g => g.Owner)
    .WithMany()                       // одноразовий зв’язок
    .HasForeignKey(g => g.OwnerId)
    .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<GroupMember>()
                .HasKey(gm => new { gm.GroupId, gm.UserId });

            modelBuilder.Entity<GroupMember>()
                .HasOne(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId);

            modelBuilder.Entity<GroupMember>()
                .HasOne(gm => gm.User)
                .WithMany()                       // не тримаємо колекцію в Users
                .HasForeignKey(gm => gm.UserId);
        }
    }
}
