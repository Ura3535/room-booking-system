using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Models;
using RoomBooking.Api.Services;

namespace RoomBooking.Api.Controllers;

public record CreateBookingRequest(int RoomId, DateTimeOffset StartTime, DateTimeOffset EndTime, string Purpose);

[ApiController]
[Route("api")]
public class RoomsAndBookingsController(AppDbContext db, BookingService bookingService) : ControllerBase
{
    // Допоміжний метод: зчитуємо X-User-Id із заголовка (за замовчуванням student1 = 1)
    private int GetCurrentUserId()
        => Request.Headers.TryGetValue("X-User-Id", out var val) && int.TryParse(val, out var id) ? id : 1;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
        => Ok(await db.Users.OrderBy(u => u.Id).ToListAsync());

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
            IsAvailable = !busyRoomIds.Contains(r.Id)
        });

        return Ok(result);
    }

    // FR-02: Створення бронювання
    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var userId = GetCurrentUserId();
        var res = await bookingService.CreateBookingAsync(userId, req.RoomId, req.StartTime, req.EndTime, req.Purpose);
        if (!res.IsSuccess)
            return StatusCode(res.StatusCode, new { error = res.Error });

        return StatusCode(201, res.Data);
    }

    // FR-03: Перегляд власних бронювань
    [HttpGet("bookings/my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = GetCurrentUserId();
        var items = await db.Bookings
            .Include(b => b.Room)
            .Where(b => b.UserId == userId)
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
        var userId = GetCurrentUserId();
        var res = await bookingService.CancelBookingAsync(id, userId);
        if (!res.IsSuccess)
            return StatusCode(res.StatusCode, new { error = res.Error });

        return Ok(new { res.Data!.Id, Status = res.Data.Status.ToString() });
    }
}