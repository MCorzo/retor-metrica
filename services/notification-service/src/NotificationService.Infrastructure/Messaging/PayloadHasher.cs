using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NotificationService.Infrastructure.Messaging;

public static class PayloadHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Hash<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }
}
