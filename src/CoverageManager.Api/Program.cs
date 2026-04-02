using System.Net.WebSockets;
using Serilog;
using CoverageManager.Core.Engines;
using CoverageManager.Api.Services;
using CoverageManager.Connector;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/coverage-manager-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Core singletons
    var positionManager = new PositionManager();
    var priceCache = new PriceCache();
    var exposureEngine = new ExposureEngine(positionManager);
    var dealStore = new DealStore();

    builder.Services.AddSingleton(positionManager);
    builder.Services.AddSingleton(priceCache);
    builder.Services.AddSingleton(exposureEngine);
    builder.Services.AddSingleton(dealStore);

    // Supabase HTTP client
    builder.Services.AddHttpClient<SupabaseService>();
    builder.Services.AddSingleton<SupabaseService>(sp =>
        new SupabaseService(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SupabaseService)),
            sp.GetRequiredService<ILogger<SupabaseService>>()));

    // Broadcast service (WebSocket push)
    builder.Services.AddSingleton<ExposureBroadcastService>();

    // MT5 Manager connection (reads accounts from Supabase, connects, snapshots positions)
    builder.Services.AddSingleton<MT5ManagerConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<SupabaseService>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5ManagerConnection(
            sp.GetRequiredService<ILogger<MT5ManagerConnection>>(),
            positionManager,
            priceCache,
            dealStore,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty(),
            async accounts => await supabase.UpsertTradingAccountsAsync(accounts));
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5ManagerConnection>());

    // MT5 Coverage connection (LP account — reads coverage positions)
    builder.Services.AddSingleton<MT5CoverageConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<SupabaseService>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5CoverageConnection(
            sp.GetRequiredService<ILogger<MT5CoverageConnection>>(),
            positionManager,
            priceCache,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty());
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5CoverageConnection>());

    // Data sync service (persists deals to Supabase, detects modifications)
    builder.Services.AddHostedService<DataSyncService>(sp =>
        new DataSyncService(
            sp.GetRequiredService<SupabaseService>(),
            dealStore,
            sp.GetRequiredService<ILogger<DataSyncService>>()));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS for React dev server
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    var app = builder.Build();

    // Load symbol mappings from Supabase on startup
    using (var scope = app.Services.CreateScope())
    {
        var supabase = scope.ServiceProvider.GetRequiredService<SupabaseService>();
        var mappings = await supabase.GetMappingsAsync();
        positionManager.LoadMappings(mappings);
        Log.Information("Loaded {Count} symbol mappings from Supabase", mappings.Count);
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseWebSockets();

    // WebSocket endpoint for real-time exposure updates
    app.Map("/ws/exposure", async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var broadcastService = context.RequestServices.GetRequiredService<ExposureBroadcastService>();
        var clientId = Guid.NewGuid().ToString();

        broadcastService.AddClient(clientId, ws);

        // Keep connection alive — read until client disconnects
        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (WebSocketException) { }
        finally
        {
            broadcastService.RemoveClient(clientId);
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    });

    app.MapControllers();

    Log.Information("Coverage Manager API starting on http://localhost:5000");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
