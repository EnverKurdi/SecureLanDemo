using DataStore;

static int GetPort(string[] args, int fallback)
{
    var idx = Array.FindIndex(args, a => a.Equals("--port", StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var p)) return p;
    return fallback;
}

var port = GetPort(args, 9100);
Console.WriteLine($"[DATASTORE] Starting on 127.0.0.1:{port}");

var server = new DataStoreServer("127.0.0.1", port);
await server.RunAsync();
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
