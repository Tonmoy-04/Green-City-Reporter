using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GreenCityReporter.Models;

namespace GreenCityReporter.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Users DbSet is provided by IdentityDbContext
        public DbSet<Report> Reports { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StatusHistory> StatusHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<Donation> Donations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.PaymentMethod, d.TransactionId }).IsUnique();
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.Provider, d.TransactionId }).IsUnique().HasFilter("[Provider] = 'SSLCommerz'");
            modelBuilder.Entity<Donation>().HasIndex(d => d.CheckoutKey).IsUnique().HasFilter("[CheckoutKey] IS NOT NULL");
            modelBuilder.Entity<Donation>().HasIndex(d => d.ReceiptToken).IsUnique().HasFilter("[ReceiptToken] IS NOT NULL");
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.Provider, d.IsSandbox, d.BankTransactionId }).IsUnique().HasFilter("[BankTransactionId] IS NOT NULL");
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.Provider, d.Status, d.LastCheckedAt });
            modelBuilder.Entity<Donation>().HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);


            // Configure defining relationship behavior to avoid cascading delete conflicts where necessary
            modelBuilder.Entity<Report>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StatusHistory>()
                .HasOne(sh => sh.Updater)
                .WithMany(u => u.StatusHistories)
                .HasForeignKey(sh => sh.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
