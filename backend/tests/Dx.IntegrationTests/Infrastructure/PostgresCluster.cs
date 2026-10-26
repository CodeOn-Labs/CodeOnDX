using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Dx.IntegrationTests.Infrastructure;

/// <summary>
/// Throw-away PostgreSQL cluster for one test run (the dev machine has no Docker):
/// initdb into a temp folder, start on a free loopback port with trust auth, delete on dispose.
/// Binaries: env DX_PG_BIN, else C:\Program Files\PostgreSQL\18\bin, else initdb on PATH.
/// </summary>
public sealed class PostgresCluster : IAsyncDisposable
{
    public const string Superuser = "postgres";

    private readonly string _bin;
    private readonly string _dataDir;
    private readonly string _logFile;

    public int Port { get; }

    private PostgresCluster(string bin, string root, int port)
    {
        _bin = bin;
        _dataDir = Path.Combine(root, "data");
        _logFile = Path.Combine(root, "postgres.log");
        Port = port;
    }

    public string ConnectionString(string database, string user)
        => $"Host=127.0.0.1;Port={Port};Database={database};Username={user};Pooling=true;Timeout=30";

    public static async Task<PostgresCluster> StartAsync()
    {
        var bin = FindBinDirectory();
        var root = Path.Combine(Path.GetTempPath(), "dx-it-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);
        var cluster = new PostgresCluster(bin, root, FreePort());

        // builtin C.UTF-8 provider (PG 17+): Unicode-aware lower()/ILIKE without depending on OS locales.
        await cluster.RunAsync("initdb", "-D", cluster._dataDir, "-U", Superuser, "-A", "trust", "-E", "UTF8",
            "--locale-provider=builtin", "--builtin-locale=C.UTF-8");
        await cluster.RunAsync("pg_ctl", "-D", cluster._dataDir, "-l", cluster._logFile, "-w", "-t", "60",
            "-o", $"-p {cluster.Port} -c listen_addresses=127.0.0.1 -c fsync=off -c synchronous_commit=off -c full_page_writes=off",
            "start");
        return cluster;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await RunAsync("pg_ctl", "-D", _dataDir, "-m", "immediate", "-w", "stop");
        }
        catch
        {
            // already stopped
        }
        var root = Path.GetDirectoryName(_dataDir)!;
        for (var attempt = 0; attempt < 5 && Directory.Exists(root); attempt++)
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { await Task.Delay(500); }
            catch (UnauthorizedAccessException) { await Task.Delay(500); }
        }
    }

    private async Task RunAsync(string tool, params string[] args)
    {
        var exe = Path.Combine(_bin, OperatingSystem.IsWindows() ? tool + ".exe" : tool);
        // pg_ctl start leaves the server running with the inherited handles; never redirect its
        // output (the pipe would stay open) — the server log goes to the -l file instead.
        var redirect = tool != "pg_ctl";
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Cannot start {exe}");
        var stdout = redirect ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
        var stderr = redirect ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
        {
            var log = File.Exists(_logFile) ? await File.ReadAllTextAsync(_logFile) : string.Empty;
            throw new InvalidOperationException(
                $"{tool} exited with {process.ExitCode}\n{await Completed(stdout)}\n{await Completed(stderr)}\n{log}");
        }
    }

    private static async Task<string> Completed(Task<string> task)
        => await Task.WhenAny(task, Task.Delay(1000)) == task ? task.Result : string.Empty;

    private static string FindBinDirectory()
    {
        var exe = OperatingSystem.IsWindows() ? "initdb.exe" : "initdb";
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("DX_PG_BIN"),
            @"C:\Program Files\PostgreSQL\18\bin",
        };
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator));
        return candidates.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d) && File.Exists(Path.Combine(d, exe)))
               ?? throw new InvalidOperationException(
                   "PostgreSQL 18 binaries not found: set DX_PG_BIN to the folder containing initdb and pg_ctl.");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
