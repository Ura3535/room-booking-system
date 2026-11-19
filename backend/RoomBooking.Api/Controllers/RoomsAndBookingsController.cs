using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Models;
using RoomBooking.Api.Services;

namespace RoomBooking.Api.Controllers;

public record CreateBookingRequest(int RoomId, DateTimeOffset StartTime, DateTimeOffset EndTime, string Purpose);
public record ReviewBookingRequest(bool Approve, string? Comment);

[ApiController]
[Route("api")]
public class RoomsAndBookingsController(AppDbContext db, BookingService bookingService, TokenService tokenService) : ControllerBase
{
    private TokenPayload? GetCurrentUser()
        => tokenService.ValidateToken(Request.Headers.Authorization);

    // FR-01: Перегляд аудиторій і доступності на інтервал [start, end)
    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms([FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end)
    {
        if (start.HasValue && end.HasValue && !BookingService.IsValidInterval(start.Value, end.Value))
            return BadRequest(new { error = "Параметр start має бути раніше за end." });

        var rooms = await db.Rooms.OrderBy(r => r.Id).ToListAsync();
        var busyRoomIds = new HashSet<int>();

        if (start.HasValue && end.HasValue)
        {
            var sUtc = start.Value.ToUniversalTime();
            var eUtc = end.Value.ToUniversalTime();
            busyRoomIds = (await db.Bookings
                .Where(b => (b.Status == BookingStatus.Active || b.Status == BookingStatus.Pending)
                            && b.StartTime < eUtc && sUtc < b.EndTime)
                .Select(b => b.RoomId)
                .Distinct()
                .ToListAsync()).ToHashSet();
        }

        var result = rooms.Select(r => new
        {
            r.Id,
            r.Name,
            r.Building,
            r.Capacity,
            r.Equipment,
            RequiresApproval = r.Capacity > BookingService.LargeRoomCapacityThreshold,
            IsAvailable = !busyRoomIds.Contains(r.Id)
        });

        return Ok(result);
    }

    // FR-02: Створення бронювання
    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized(new { error = "Потрібна авторизація." });

        var res = await bookingService.CreateBookingAsync(user.UserId, req.RoomId, req.StartTime, req.EndTime, req.Purpose);
        if (!res.IsSuccess)
            return StatusCode(res.StatusCode, new { error = res.Error });

        return StatusCode(201, res.Data);
    }

    // FR-03: Перегляд власних бронювань
    [HttpGet("bookings/my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized(new { error = "Потрібна авторизація." });

        var items = await db.Bookings
            .Include(b => b.Room)
            .Where(b => b.UserId == user.UserId)
            .OrderByDescending(b => b.StartTime)
            .Select(b => new
            {
                b.Id,
                b.RoomId,
                RoomName = b.Room.Name,
                Building = b.Room.Building,
                Capacity = b.Room.Capacity,
                b.StartTime,
                b.EndTime,
                b.Purpose,
                Status = b.Status.ToString()
            })
            .ToListAsync();

        return Ok(items);
    }

    // FR-04: Скасування власного бронювання
    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized(new { error = "Потрібна авторизація." });

        var res = await bookingService.CancelBookingAsync(id, user.UserId);
        if (!res.IsSuccess)
            return StatusCode(res.StatusCode, new { error = res.Error });

        return Ok(new { res.Data!.Id, Status = res.Data.Status.ToString() });
    }

    [HttpGet("bookings/pending")]
    public async Task<IActionResult> GetPendingBookings()
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized(new { error = "Потрібна авторизація." });
        if (user.Role != UserRole.Admin) return StatusCode(403, new { error = "Доступ лише для адміністратора." });

        var items = await db.Bookings
            .Include(b => b.Room)
            .Include(b => b.User)
            .Where(b => b.Status == BookingStatus.Pending)
            .OrderBy(b => b.StartTime)
            .Select(b => new
            {
                b.Id,
                b.RoomId,
                RoomName = b.Room.Name,
                Building = b.Room.Building,
                Capacity = b.Room.Capacity,
                AuthorName = b.User.FullName,
                AuthorUsername = b.User.Username,
                b.StartTime,
                b.EndTime,
                b.Purpose,
                Status = b.Status.ToString()
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("bookings/{id:int}/review")]
    public async Task<IActionResult> ReviewBooking(int id, [FromBody] ReviewBookingRequest req)
    {
        var user = GetCurrentUser();
        if (user is null) return Unauthorized(new { error = "Потрібна авторизація." });

        var res = await bookingService.ReviewBookingAsync(id, user.UserId, req.Approve, req.Comment);
        if (!res.IsSuccess)
            return StatusCode(res.StatusCode, new { error = res.Error });

        return Ok(new
        {
            res.Data!.Id,
            Status = res.Data.Status.ToString(),
            res.Data.ReviewedByUserId,
            res.Data.ReviewedAt,
            res.Data.ReviewComment
        });
    }
}