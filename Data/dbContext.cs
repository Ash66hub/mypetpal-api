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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships and other model configurations here

            modelBuilder.Entity<User>()
                  .HasKey(u => u.UserId);

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


            base.OnModelCreating(modelBuilder);
        }
    }
}


