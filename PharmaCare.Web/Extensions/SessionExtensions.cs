using System.Text.Json;

namespace PharmaCare.Web.Extensions;

/// <summary>
/// Extension methods for session management
/// </summary>
public static class SessionExtensions
{
    public static void SetObjectAsJson(this ISession session, string key, object value)
    {
        session.SetString(key, JsonSerializer.Serialize(value));
    }

    public static T? GetObjectFromJson<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        try
        {
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            // If deserialization fails (e.g. data corruption or schema change), return default/null
            // This prevents the application from crashing and allows safe recovery (e.g. empty cart)
            return default;
        }
    }
}
