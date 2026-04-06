using Microsoft.EntityFrameworkCore;
using mypetpal.Models;

namespace mypetpal.dbContext
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserPet> UserPets { get; set; }
        public DbSet<PetAttributes> PetAttributes { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<DecorInstance> DecorInstances { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<VisitInvitation> VisitInvitations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships and other model configurations here
            modelBuilder.Entity<Friendship>()
                  .HasKey(f => f.Id);

            modelBuilder.Entity<User>()
                  .HasKey(u => u.UserId);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Id)
                .IsUnique();

            modelBuilder.Entity<UserPet>()
               .HasOne(up => up.PetAttributes)
               .WithOne()
               .HasForeignKey<UserPet>(up => up.PetId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPet>()
               .HasOne<User>() 
               .WithMany()
               .HasForeignKey(up => up.UserId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPet>()
                .HasOne<User>() 
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PetAttributes>()
                .HasKey(pa => pa.PetId);

            modelBuilder.Entity<PetAttributes>()
                .HasIndex(pa => pa.Id)
                .IsUnique();


            modelBuilder.Entity<DecorInstance>()
                .HasKey(d => d.Id);

            modelBuilder.Entity<DecorInstance>()
                .HasIndex(d => d.UserId);

            modelBuilder.Entity<UserSettings>()
                .HasKey(u => u.UserId);

            modelBuilder.Entity<VisitInvitation>()
                .HasKey(v => v.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}


