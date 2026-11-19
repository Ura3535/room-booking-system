using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoomBooking.Api.Data;
using RoomBooking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<BookingService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await AppDbContext.SeedAsync(db);
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..8];
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    var sw = Stopwatch.StartNew();

    try
    {
        await next();
        sw.Stop();
        app.Logger.LogInformation("[{Time}] CorrelationId={Id} {Method} {Path} -> {Status} ({Duration} ms)",
            DateTimeOffset.UtcNow.ToString("O"), correlationId, context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
    catch (Exception ex) when (ex is NpgsqlException || ex.InnerException is NpgsqlException)
    {
        sw.Stop();
        app.Logger.LogError("[{Time}] ALERT CorrelationId={Id} DB_UNAVAILABLE {Method} {Path} -> 503 ({Duration} ms): {Msg}",
            DateTimeOffset.UtcNow.ToString("O"), correlationId, context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds, ex.Message);

        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Сервіс тимчасово недоступний через втрату зʼєднання з базою даних.",
            correlationId
        });
    }
});

app.MapGet("/health", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "Healthy", database = "Connected", timestamp = DateTimeOffset.UtcNow })
        : Results.Json(new { status = "Unhealthy", database = "Disconnected", timestamp = DateTimeOffset.UtcNow }, statusCode: 503);
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();

public partial class Program { }