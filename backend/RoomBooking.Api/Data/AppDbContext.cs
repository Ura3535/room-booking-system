using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Models;

namespace RoomBooking.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().Property(u => u.Role).HasConversion<string>();
        modelBuilder.Entity<Booking>().Property(b => b.Status).HasConversion<string>();
        modelBuilder.Entity<Booking>().HasIndex(b => new { b.RoomId, b.StartTime, b.EndTime });
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new AppUser { Id = 1, Username = "student1", FullName = "Іван Петренко (Студент)", Role = UserRole.Student },
                new AppUser { Id = 2, Username = "student2", FullName = "Олена Коваль (Студентка)", Role = UserRole.Student },
                new AppUser { Id = 3, Username = "admin", FullName = "Дмитро Сидоренко (Адмін)", Role = UserRole.Admin }
            );
        }

        if (!await db.Rooms.AnyAsync())
        {
            db.Rooms.AddRange(
                new Room { Id = 1, Name = "Ауд. 101", Building = "Головний корпус", Capacity = 30, Equipment = "Проєктор, дошка" },
                new Room { Id = 2, Name = "Ауд. 205", Building = "Головний корпус", Capacity = 80, Equipment = "Smart-дошка, мікрофон" },
                new Room { Id = 3, Name = "Велика фізична", Building = "Корпус №2", Capacity = 150, Equipment = "Аудіосистема, 2 проєктори" },
                new Room { Id = 4, Name = "Актова зала", Building = "Головний корпус", Capacity = 250, Equipment = "Сцена, звук, проєктор" }
            );
        }

        await db.SaveChangesAsync();
    }
}