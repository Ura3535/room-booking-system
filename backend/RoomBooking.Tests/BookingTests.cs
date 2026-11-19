using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Services;

namespace RoomBooking.Tests;

public class BookingTests
{
    private const string TestConn = "Host=localhost;Port=5433;Database=roombooking_test;Username=postgres;Password=postgres_dev_password";

    [Theory]
    [InlineData("2026-11-20T10:00:00+02:00", "2026-11-20T11:00:00+02:00", true)]
    [InlineData("2026-11-20T11:00:00+02:00", "2026-11-20T10:00:00+02:00", false)]
    [InlineData("2026-11-20T10:00:00+02:00", "2026-11-20T10:00:00+02:00", false)]
    public void ValidateInterval_WorksCorrectly(string startStr, string endStr, bool expected)
    {
        var start = DateTimeOffset.Parse(startStr);
        var end = DateTimeOffset.Parse(endStr);
        Assert.Equal(expected, BookingService.IsValidInterval(start, end));
    }

    [Fact]
    public void IntervalOverlap_AllowsAdjacent_BlocksPartialOverlap()
    {
        var s1 = DateTimeOffset.Parse("2026-11-20T10:00:00+02:00");
        var e1 = DateTimeOffset.Parse("2026-11-20T11:00:00+02:00");

        // Суміжний інтервал [11:00, 12:00) — дозволено
        var sAdj = DateTimeOffset.Parse("2026-11-20T11:00:00+02:00");
        var eAdj = DateTimeOffset.Parse("2026-11-20T12:00:00+02:00");
        Assert.False(BookingService.IsOverlapping(s1, e1, sAdj, eAdj));

        // Частковий перетин [10:30, 11:30) — заборонено
        var sPart = DateTimeOffset.Parse("2026-11-20T10:30:00+02:00");
        var ePart = DateTimeOffset.Parse("2026-11-20T11:30:00+02:00");
        Assert.True(BookingService.IsOverlapping(s1, e1, sPart, ePart));
    }

    [Fact]
    public async Task ParallelBookingRequests_OnlyOneSucceeds()
    {
        // Інтеграційний тест запускається лише коли доступний контейнер PostgreSQL
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(TestConn).Options;
        await using (var setupDb = new AppDbContext(options))
        {
            try
            {
                await setupDb.Database.EnsureDeletedAsync();
                await setupDb.Database.EnsureCreatedAsync();
                await AppDbContext.SeedAsync(setupDb);
            }
            catch
            {
                // Якщо в GitHub Actions без сервісу БД — пропускаємо інтеграційну частину
                return;
            }
        }

        var start = DateTimeOffset.Parse("2026-11-25T14:00:00+02:00");
        var end = DateTimeOffset.Parse("2026-11-25T15:30:00+02:00");

        async Task<int> TryBookAsync(int userId)
        {
            await using var db = new AppDbContext(options);
            var service = new BookingService(db);
            var res = await service.CreateBookingAsync(userId, roomId: 1, start, end, "Паралельний тест");
            return res.StatusCode;
        }

        var results = await Task.WhenAll(TryBookAsync(1), TryBookAsync(2));

        Assert.Contains(201, results);
        Assert.Contains(409, results);
    }
    [Theory]
    [InlineData(30, RoomBooking.Api.Models.BookingStatus.Active)]
    [InlineData(100, RoomBooking.Api.Models.BookingStatus.Active)]
    [InlineData(101, RoomBooking.Api.Models.BookingStatus.Pending)]
    [InlineData(250, RoomBooking.Api.Models.BookingStatus.Pending)]
    public void DetermineInitialStatus_RespectsCapacityThreshold(int capacity, RoomBooking.Api.Models.BookingStatus expected)
    {
        Assert.Equal(expected, BookingService.DetermineInitialStatus(capacity));
    }

    [Fact]
    public async Task LargeRoomApproval_EnforcesAdminRoleAndPreventsDoubleReview()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(TestConn).Options;
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            await AppDbContext.SeedAsync(db);
        }
        catch { return; }

        var service = new BookingService(db);
        var start = DateTimeOffset.Parse("2026-11-26T10:00:00+02:00");
        var end = DateTimeOffset.Parse("2026-11-26T12:00:00+02:00");

        // RoomId = 3 ("Велика фізична", 150 місць) -> має отримати статус Pending
        var createRes = await service.CreateBookingAsync(userId: 1, roomId: 3, start, end, "Конференція");
        Assert.Equal(201, createRes.StatusCode);
        Assert.Equal(RoomBooking.Api.Models.BookingStatus.Pending, createRes.Data!.Status);

        // Звичайний студент (userId = 2) не може погодити заявку -> 403 Forbidden
        var studentReview = await service.ReviewBookingAsync(createRes.Data.Id, adminUserId: 2, approve: true, comment: null);
        Assert.Equal(403, studentReview.StatusCode);

        // Адміністратор (userId = 3) погоджує заявку -> 200 OK (Active)
        var adminReview = await service.ReviewBookingAsync(createRes.Data.Id, adminUserId: 3, approve: true, comment: "Ок");
        Assert.Equal(200, adminReview.StatusCode);
        Assert.Equal(RoomBooking.Api.Models.BookingStatus.Active, adminReview.Data!.Status);

        // Повторне рішення по вже опрацьованій заявці -> 409 Conflict
        var duplicateReview = await service.ReviewBookingAsync(createRes.Data.Id, adminUserId: 3, approve: false, comment: "Скасувати");
        Assert.Equal(409, duplicateReview.StatusCode);
    }
}