using System.Text.Json;
using IpRoyalService;

namespace IpRoyalControl;

public static class StatusReader
{
    public static ConnectionStatus? Read(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<ConnectionStatus>(File.ReadAllText(path)) : null; }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }
}
