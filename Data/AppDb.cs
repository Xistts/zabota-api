using Microsoft.EntityFrameworkCore;
using Zabota.Models;

namespace Zabota.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }


    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<BpRecord> BpRecords => Set<BpRecord>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<MedicationTime> MedicationTimes => Set<MedicationTime>();
    public DbSet<MedicationDay> MedicationDays => Set<MedicationDay>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- User ----------
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd()
#if USE_SQLSERVER
                .HasDefaultValueSql("NEWID()");
#else
                ;
#endif

            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(32).IsRequired();

            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired(false);

            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MiddleName).HasMaxLength(100);

            e.Property(x => x.Role);
            e.Property(x => x.Phone).HasMaxLength(32);

            e.Property(x => x.DateOfBirth);
            e.Property(x => x.IsActive);
            e.Property(x => x.IsVerified);
            e.Property(x => x.CreatedAtUtc);
        });

        // ---------- TaskItem ----------
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => new { x.UserId, x.IsActive, x.IsCompleted }); // частые фильтры
            e.HasOne(x => x.User).WithMany(u => u.Tasks).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- BpRecord ----------
        modelBuilder.Entity<BpRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.MeasuredAtUtc });
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.User).WithMany(u => u.BpRecords).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Medication ----------
        modelBuilder.Entity<Medication>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.User).WithMany(u => u.Medications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Recurrence).HasConversion<int>();
        });

        modelBuilder.Entity<MedicationTime>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MedicationId, x.Hour, x.Minute }).IsUnique(); // не дублировать одно и то же время
            e.HasOne(x => x.Medication).WithMany(m => m.Times).HasForeignKey(x => x.MedicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MedicationDay>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MedicationId, x.DayOfWeek }).IsUnique(); // не дублировать один день
            e.Property(x => x.DayOfWeek).HasConversion<int>();
            e.HasOne(x => x.Medication).WithMany(m => m.Days).HasForeignKey(x => x.MedicationId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Family ----------
        modelBuilder.Entity<Family>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.InviteCode).HasMaxLength(12).IsRequired();
            e.HasIndex(x => x.InviteCode).IsUnique(); // уникальный код
            e.Property(x => x.CreatedAtUtc);
        });

        // ---------- FamilyMember (User <-> Family с атрибутами) ----------
        modelBuilder.Entity<FamilyMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.UserId }).IsUnique(); // один пользователь не должен повторяться в семье
            e.Property(x => x.RoleInFamily).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.Family).WithMany(f => f.Members).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.FamilyMemberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- ChatMessage ----------
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.FamilyId, x.SentAtUtc });
            e.HasOne(x => x.Family).WithMany(f => f.Messages).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuthorUser).WithMany(u => u.ChatMessages).HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
