using System.Security.Cryptography;
using System.Text;
using RoomBooking.Api.Models;

namespace RoomBooking.Api.Services;

public record TokenPayload(int UserId, string Username, UserRole Role);

public class TokenService(IConfiguration config)
{
    private string Secret => config["Auth:Secret"] ?? "dev-room-booking-secret-key-2026-knu";

    public string GenerateToken(AppUser user)
    {
        var raw = $"{user.Id}:{user.Username}:{user.Role}";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        var sig = ComputeHmac(payloadB64);
        return $"{payloadB64}.{sig}";
    }

    public TokenPayload? ValidateToken(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..].Trim();
        var parts = token.Split('.');
        if (parts.Length != 2) return null;

        var expectedSig = ComputeHmac(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(expectedSig)))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            var fields = decoded.Split(':');
            if (fields.Length != 3) return null;

            return new TokenPayload(
                int.Parse(fields[0]),
                fields[1],
                Enum.Parse<UserRole>(fields[2])
            );
        }
        catch
        {
            return null;
        }
    }

    private string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }
}