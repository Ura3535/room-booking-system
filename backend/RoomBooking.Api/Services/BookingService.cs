using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Models;

namespace RoomBooking.Api.Services;

public record ServiceResult<T>(bool IsSuccess, T? Data, int StatusCode, string? Error);

public class BookingService(AppDbContext db)
{
    public const int LargeRoomCapacityThreshold = 100;

    public static bool IsOverlapping(DateTimeOffset start1, DateTimeOffset end1, DateTimeOffset start2, DateTimeOffset end2)
        => start1 < end2 && start2 < end1;

    public static bool IsValidInterval(DateTimeOffset start, DateTimeOffset end)
        => start < end;

    public static BookingStatus DetermineInitialStatus(int roomCapacity)
        => roomCapacity > LargeRoomCapacityThreshold ? BookingStatus.Pending : BookingStatus.Active;

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
            return new(false, null, 409, "Обраний часовий інтервал перетинається з іншим активним або очікуючим бронюванням.");

        var booking = new Booking
        {
            RoomId = roomId,
            UserId = userId,
            StartTime = start.ToUniversalTime(),
            EndTime = end.ToUniversalTime(),
            Purpose = string.IsNullOrWhiteSpace(purpose) ? "Навчальний захід" : purpose.Trim(),
            Status = DetermineInitialStatus(room.Capacity)
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

        if (booking.Status != BookingStatus.Active && booking.Status != BookingStatus.Pending)
            return new(false, null, 400, $"Неможливо скасувати бронювання у стані {booking.Status}.");

        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();

        return new(true, booking, 200, null);
    }

    public async Task<ServiceResult<Booking>> ReviewBookingAsync(int bookingId, int adminUserId, bool approve, string? comment)
    {
        var admin = await db.Users.FindAsync(adminUserId);
        if (admin is null)
            return new(false, null, 401, "Користувача не знайдено.");

        if (admin.Role != UserRole.Admin)
            return new(false, null, 403, "Погоджувати або відхиляти заявки може лише адміністратор.");

        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return new(false, null, 404, "Заявку на бронювання не знайдено.");

        if (booking.Status != BookingStatus.Pending)
            return new(false, null, 409, $"Заявку вже опрацьовано (поточний стан: {booking.Status}).");

        booking.Status = approve ? BookingStatus.Active : BookingStatus.Rejected;
        booking.ReviewedByUserId = adminUserId;
        booking.ReviewedAt = DateTimeOffset.UtcNow;
        booking.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        await db.SaveChangesAsync();
        return new(true, booking, 200, null);
    }
}