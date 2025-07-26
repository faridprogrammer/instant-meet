using InstantMeet.Framework.Data.Model;
using InstantMeet.Framework.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InstantMeet.Framework.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Participant> Participants { get; set; }


    public string GetAbsoluteDatabasePath()
    {
        var connectionString = Database.GetConnectionString();
        var builder = new SqliteConnectionStringBuilder(connectionString);
        string dbPath = builder.DataSource;

        return Path.GetFullPath(dbPath);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
        .HasIndex(d => d.UserName)
        .IsUnique();

        modelBuilder.Entity<Room>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<Room>()
        .HasOne(dd => dd.User)
        .WithMany(dd => dd.Rooms)
        .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Participant>()
            .HasOne(p => p.Room)
            .WithMany(r => r.Participants)
            .HasForeignKey(p => p.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}