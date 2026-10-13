using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnectMvc.Data;

/// <summary>
/// EF Core DbContext for the CivicConnect application.
/// All mappings are derived directly from the authoritative M2 schema in
/// <c>civicconnect_dev_schema.sql</c> (PostgreSQL snake_case, public schema).
/// No EnsureCreated / Migrate is called here — schema management is the DBA's responsibility.
/// </summary>
public class CivicConnectDbContext : DbContext
{
    public CivicConnectDbContext(DbContextOptions<CivicConnectDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<User>           Users           => Set<User>();
    public DbSet<Category>       Categories      => Set<Category>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Assignment>     Assignments     => Set<Assignment>();
    public DbSet<StatusHistory>  StatusHistories => Set<StatusHistory>();
    public DbSet<RequestAction>  RequestActions  => Set<RequestAction>();
    public DbSet<Resolution>     Resolutions     => Set<Resolution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── users ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", "public");
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.UserId)
                  .HasColumnName("user_id")
                  .UseIdentityAlwaysColumn();
            entity.Property(u => u.Name)
                  .HasColumnName("name")
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(u => u.Email)
                  .HasColumnName("email")
                  .HasMaxLength(255)
                  .IsRequired();
            entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("users_email_key");
            entity.Property(u => u.Role)
                  .HasColumnName("role")
                  .HasConversion<string>()
                  .HasMaxLength(50)
                  .IsRequired();
            entity.Ignore(u => u.IsActive);
        });

        // ── categories ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories", "public");
            entity.HasKey(c => c.CategoryId);
            entity.Property(c => c.CategoryId)
                  .HasColumnName("category_id")
                  .UseIdentityAlwaysColumn();
            entity.Property(c => c.Name)
                  .HasColumnName("name")
                  .HasMaxLength(100)
                  .IsRequired();
            entity.HasIndex(c => c.Name).IsUnique().HasDatabaseName("categories_name_key");
            entity.Property(c => c.Description)
                  .HasColumnName("description"); // text, nullable
        });

        // ── service_requests ───────────────────────────────────────────────────
        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("service_requests", "public");
            entity.HasKey(r => r.RequestId);
            entity.Property(r => r.RequestId)
                  .HasColumnName("request_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(r => r.RequesterId)
                  .HasColumnName("requester_id")
                  .IsRequired();

            entity.Property(r => r.CategoryId)
                  .HasColumnName("category_id")
                  .IsRequired();

            entity.Property(r => r.Title)
                  .HasColumnName("title")
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(r => r.Description)
                  .HasColumnName("description") // text — no max length
                  .IsRequired();

            // CurrentStatus has a private setter — configure backing-field access.
            entity.Property(r => r.CurrentStatus)
                  .HasColumnName("current_status")
                  .HasConversion<string>()
                  .HasMaxLength(50)
                  .IsRequired()
                  .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(r => r.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            // UpdatedAt has a private setter.
            entity.Property(r => r.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired()
                  .UsePropertyAccessMode(PropertyAccessMode.Field);

            // Version has a private setter.
            entity.Property(r => r.Version)
                  .HasColumnName("version")
                  .IsRequired()
                  .HasDefaultValue(1)
                  .IsConcurrencyToken()
                  .UsePropertyAccessMode(PropertyAccessMode.Field);

            // Computed / unmapped helper properties — excluded from EF tracking.
            entity.Ignore(r => r.CategoryName);
            entity.Ignore(r => r.AssignedStaffId);

            // FK: requester_id → users.user_id  (fk_service_request_requester)
            entity.HasOne(r => r.Requester)
                  .WithMany()
                  .HasForeignKey(r => r.RequesterId)
                  .HasConstraintName("fk_service_request_requester")
                  .OnDelete(DeleteBehavior.NoAction);

            // FK: category_id → categories.category_id  (fk_service_request_category)
            entity.HasOne(r => r.Category)
                  .WithMany(c => c.ServiceRequests)
                  .HasForeignKey(r => r.CategoryId)
                  .HasConstraintName("fk_service_request_category")
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── assignments ────────────────────────────────────────────────────────
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments", "public");
            entity.HasKey(a => a.AssignmentId);
            entity.Property(a => a.AssignmentId)
                  .HasColumnName("assignment_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(a => a.RequestId)
                  .HasColumnName("request_id")
                  .IsRequired();

            entity.Property(a => a.StaffId)
                  .HasColumnName("staff_id")
                  .IsRequired();

            entity.Property(a => a.AssignedAt)
                  .HasColumnName("assigned_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            entity.Property(a => a.Active)
                  .HasColumnName("active")
                  .IsRequired()
                  .HasDefaultValue(true);

            // FK: request_id → service_requests.request_id  (fk_assignment_request)
            entity.HasOne(a => a.Request)
                  .WithMany(r => r.Assignments)
                  .HasForeignKey(a => a.RequestId)
                  .HasConstraintName("fk_assignment_request")
                  .OnDelete(DeleteBehavior.NoAction);

            // FK: staff_id → users.user_id  (fk_assignment_staff)
            entity.HasOne(a => a.Staff)
                  .WithMany()
                  .HasForeignKey(a => a.StaffId)
                  .HasConstraintName("fk_assignment_staff")
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── status_history ─────────────────────────────────────────────────────
        // Table name is "status_history" (singular), not "status_histories".
        modelBuilder.Entity<StatusHistory>(entity =>
        {
            entity.ToTable("status_history", "public");
            entity.HasKey(sh => sh.StatusHistoryId);
            entity.Property(sh => sh.StatusHistoryId)
                  .HasColumnName("status_history_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(sh => sh.RequestId)
                  .HasColumnName("request_id")
                  .IsRequired();

            // changed_by is an int FK to users.user_id.
            // The property is named ChangedBy (not ChangedByUserId) to match the column name exactly.
            entity.Property(sh => sh.ChangedBy)
                  .HasColumnName("changed_by")
                  .IsRequired();

            entity.Property(sh => sh.PreviousStatus)
                  .HasColumnName("previous_status")
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(sh => sh.NewStatus)
                  .HasColumnName("new_status")
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(sh => sh.ChangedAt)
                  .HasColumnName("changed_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            // FK: request_id → service_requests.request_id  (fk_status_history_request)
            entity.HasOne(sh => sh.Request)
                  .WithMany(r => r.StatusHistories)
                  .HasForeignKey(sh => sh.RequestId)
                  .HasConstraintName("fk_status_history_request")
                  .OnDelete(DeleteBehavior.NoAction);

            // FK: changed_by → users.user_id  (fk_status_history_user)
            // HasForeignKey uses the ChangedBy int property; the navigation is ChangedByUser.
            entity.HasOne(sh => sh.ChangedByUser)
                  .WithMany()
                  .HasForeignKey(sh => sh.ChangedBy)
                  .HasConstraintName("fk_status_history_user")
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── request_actions ────────────────────────────────────────────────────
        modelBuilder.Entity<RequestAction>(entity =>
        {
            entity.ToTable("request_actions", "public");
            entity.HasKey(a => a.ActionId);
            entity.Property(a => a.ActionId)
                  .HasColumnName("action_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(a => a.RequestId)
                  .HasColumnName("request_id")
                  .IsRequired();

            entity.Property(a => a.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            entity.Property(a => a.Details)
                  .HasColumnName("details") // text — no max length
                  .IsRequired();

            entity.Property(a => a.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            // FK: request_id → service_requests.request_id  (fk_request_action_request)
            entity.HasOne(a => a.Request)
                  .WithMany(r => r.RequestActions)
                  .HasForeignKey(a => a.RequestId)
                  .HasConstraintName("fk_request_action_request")
                  .OnDelete(DeleteBehavior.NoAction);

            // FK: user_id → users.user_id  (fk_request_action_user)
            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .HasConstraintName("fk_request_action_user")
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── resolutions ────────────────────────────────────────────────────────
        modelBuilder.Entity<Resolution>(entity =>
        {
            entity.ToTable("resolutions", "public");
            entity.HasKey(r => r.ResolutionId);
            entity.Property(r => r.ResolutionId)
                  .HasColumnName("resolution_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(r => r.RequestId)
                  .HasColumnName("request_id")
                  .IsRequired();
            // resolutions_request_id_key: one resolution per request.
            entity.HasIndex(r => r.RequestId).IsUnique().HasDatabaseName("resolutions_request_id_key");

            // resolved_by is an int FK to users.user_id (not named ResolvedByUserId by design).
            entity.Property(r => r.ResolvedBy)
                  .HasColumnName("resolved_by")
                  .IsRequired();

            entity.Property(r => r.Details)
                  .HasColumnName("details") // text — no max length
                  .IsRequired();

            entity.Property(r => r.ResolvedAt)
                  .HasColumnName("resolved_at")
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            // FK: request_id → service_requests.request_id  (fk_resolution_request)
            entity.HasOne(r => r.Request)
                  .WithOne(sr => sr.Resolution)
                  .HasForeignKey<Resolution>(r => r.RequestId)
                  .HasConstraintName("fk_resolution_request")
                  .OnDelete(DeleteBehavior.NoAction);

            // FK: resolved_by → users.user_id  (fk_resolution_user)
            entity.HasOne(r => r.ResolvedByUser)
                  .WithMany()
                  .HasForeignKey(r => r.ResolvedBy)
                  .HasConstraintName("fk_resolution_user")
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
