using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Models;

namespace RoomBooking.Api.Services;

public record ServiceResult<T>(bool IsSuccess, T? Data, int StatusCode, string? Error);

public class BookingService(AppDbContext db)
{
    // Інтервали [start, end): суміжність дозволена (end1 == start2 не є перетином)
    public static bool IsOverlapping(DateTimeOffset start1, DateTimeOffset end1, DateTimeOffset start2, DateTimeOffset end2)
        => start1 < end2 && start2 < end1;

    public static bool IsValidInterval(DateTimeOffset start, DateTimeOffset end)
        => start < end;

    public async Task<ServiceResult<Booking>> CreateBookingAsync(int userId, int roomId, DateTimeOffset start, DateTimeOffset end, string purpose)
    {
        if (!IsValidInterval(start, end))
            return new(false, null, 400, "Час початку має бути строго раніше за час завершення.");

        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return new(false, null, 401, "Користувача не знайдено.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var room = await db.Rooms
            .FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"Id\" = {roomId} FOR UPDATE")
            .FirstOrDefaultAsync();

        if (room is null)
            return new(false, null, 404, "Аудиторію не знайдено.");

        var hasConflict = await db.Bookings
            .Where(b => b.RoomId == roomId && (b.Status == BookingStatus.Active || b.Status == BookingStatus.Pending))
            .AnyAsync(b => b.StartTime < end.ToUniversalTime() && start.ToUniversalTime() < b.EndTime);

        if (hasConflict)
            return new(false, null, 409, "Обраний часовий інтервал перетинається з іншим бронюванням.");

        var booking = new Booking
        {
            RoomId = roomId,
            UserId = userId,
            StartTime = start.ToUniversalTime(),
            EndTime = end.ToUniversalTime(),
            Purpose = string.IsNullOrWhiteSpace(purpose) ? "Навчальний захід" : purpose.Trim(),
            Status = BookingStatus.Active
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new(true, booking, 201, null);
    }

    public async Task<ServiceResult<Booking>> CancelBookingAsync(int bookingId, int currentUserId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return new(false, null, 404, "Бронювання не знайдено.");

        if (booking.UserId != currentUserId)
            return new(false, null, 403, "Ви не можете скасувати чуже бронювання.");

        if (booking.Status == BookingStatus.Cancelled)
            return new(false, null, 400, "Бронювання вже скасовано.");

        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();

        return new(true, booking, 200, null);
    }
}