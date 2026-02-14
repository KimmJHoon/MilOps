using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace MilOps.Server;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("[MilOps.Server] Starting...");

        // Configuration 로드
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // .env 파일 폴백 (개발 환경)
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(envPath))
        {
            DotNetEnv.Env.Load(envPath);
        }

        var port = int.TryParse(config["Server:Port"], out var p) ? p : 9100;
        var supabaseUrl = config["Supabase:Url"];
        var supabaseKey = config["Supabase:AnonKey"];

        // 환경변수 폴백
        if (string.IsNullOrEmpty(supabaseUrl))
            supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        if (string.IsNullOrEmpty(supabaseKey))
            supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
        {
            Console.WriteLine("[MilOps.Server] ERROR: Supabase URL and AnonKey must be configured");
            Console.WriteLine("  Set in appsettings.json or environment variables: SUPABASE_URL, SUPABASE_ANON_KEY");
            return;
        }

        // 서비스 초기화
        var messageStore = new SupabaseMessageStore(supabaseUrl, supabaseKey);
        await messageStore.InitializeAsync();

        var connectionManager = new ConnectionManager();
        var fcmNotifier = new FcmNotifier();
        var messageRouter = new MessageRouter(connectionManager, messageStore, fcmNotifier);
        var server = new TcpChatServer(port, connectionManager, messageRouter);

        // Ctrl+C 종료 지원
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[MilOps.Server] Shutting down...");
        };

        try
        {
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[MilOps.Server] Stopped gracefully");
        }
    }
}
