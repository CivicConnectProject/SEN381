using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnectMvc.Data;

public class CivicConnectDbContext : DbContext
{
    public CivicConnectDbContext(DbContextOptions<CivicConnectDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.Name).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(r => r.RequestId);
            entity.Property(r => r.Category).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Title).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(1000).IsRequired();
            entity.Property(r => r.CurrentStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(r => r.CreatedAt).IsRequired();
            entity.Property(r => r.UpdatedAt).IsRequired();
            entity.HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
