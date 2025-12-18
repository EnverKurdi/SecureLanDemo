using ServerApp;

static int GetArgPort(string[] args, string name, int fallback)
{
    var idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var p)) return p;
    return fallback;
}

var listenPort = GetArgPort(args, "--listenPort", 9200);
var hsmPort = GetArgPort(args, "--hsmPort", 9000);
var dataPort = GetArgPort(args, "--dataPort", 9100);
var listenIp = GetArgValue(args, "--listenIp", "0.0.0.0");

Console.WriteLine($"[SERVER] TLS listener on {listenIp}:{listenPort}");
Console.WriteLine($"[SERVER] HSM at 127.0.0.1:{hsmPort}");
Console.WriteLine($"[SERVER] DataStore at 127.0.0.1:{dataPort}");

var app = new ServerApplication(
    serverIp: listenIp,
    serverPort: listenPort,
    hsmIp: "127.0.0.1",
    hsmPort: hsmPort,
    dataIp: "127.0.0.1",
    dataPort: dataPort
);

await app.RunAsync();
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

static string GetArgValue(string[] args, string name, string fallback)
{
    var idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
    return fallback;
}
