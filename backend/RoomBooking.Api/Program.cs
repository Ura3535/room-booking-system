using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();

public partial class Program { }