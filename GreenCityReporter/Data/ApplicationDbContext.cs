using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GreenCityReporter.Models;

namespace GreenCityReporter.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
        }

        // Users DbSet is provided by IdentityDbContext
        public DbSet<Report> Reports { get; set; }
        public DbSet<ReportSupport> ReportSupports { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StatusHistory> StatusHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<Donation> Donations { get; set; }
        public DbSet<Department> Departments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ReportSupport>().HasKey(s => new { s.ReportId, s.UserId });
            modelBuilder.Entity<ReportSupport>().HasOne(s => s.Report).WithMany(r => r.Supports)
                .HasForeignKey(s => s.ReportId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ReportSupport>().HasOne(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ReportSupport>().HasIndex(s => new { s.UserId, s.CreatedAt });
            modelBuilder.Entity<Report>().HasIndex(r => new { r.CategoryId, r.CurrentStatus, r.Latitude, r.Longitude });
            var isPostgreSql = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
            var providerTransactionFilter = isPostgreSql ? "\"Provider\" = 'SSLCommerz'" : "[Provider] = 'SSLCommerz'";
            var nullableColumnFilter = (string columnName) => isPostgreSql
                ? $"\"{columnName}\" IS NOT NULL"
                : $"[{columnName}] IS NOT NULL";

            modelBuilder.Entity<Donation>().HasIndex(d => new { d.PaymentMethod, d.TransactionId }).IsUnique();
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.Provider, d.TransactionId }).IsUnique().HasFilter(providerTransactionFilter);
            modelBuilder.Entity<Donation>().HasIndex(d => d.CheckoutKey).IsUnique().HasFilter(nullableColumnFilter("CheckoutKey"));
            modelBuilder.Entity<Donation>().HasIndex(d => d.ReceiptToken).IsUnique().HasFilter(nullableColumnFilter("ReceiptToken"));
            modelBuilder.Entity<Donation>().HasIndex(d => new { d.Provider, d.IsSandbox, d.BankTransactionId }).IsUnique().HasFilter(nullableColumnFilter("BankTransactionId"));
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

            modelBuilder.Entity<Report>()
                .HasOne(r => r.Department)
                .WithMany(d => d.Reports)
                .HasForeignKey(r => r.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.AiSuggestedCategory)
                .WithMany()
                .HasForeignKey(r => r.AiSuggestedCategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Category>()
                .HasOne(c => c.DefaultDepartment)
                .WithMany(d => d.DefaultCategories)
                .HasForeignKey(c => c.DefaultDepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
