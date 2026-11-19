using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Api.Data;
using RoomBooking.Api.Services;

namespace RoomBooking.Api.Controllers;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.Password == req.Password);
        if (user is null)
            return Unauthorized(new { error = "Невірний логін або пароль." });

        var token = tokenService.GenerateToken(user);
        return Ok(new
        {
            token,
            user = new { user.Id, user.Username, user.FullName, Role = user.Role.ToString() }
        });
    }
}