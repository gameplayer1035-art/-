using Discord;
using Discord.WebSocket;
using StockBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StockBot.Services;

public class DiscordHostedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _services;
    private const ulong TARGET_CHANNEL_ID = 1509249628258959390;

    public DiscordHostedService(IConfiguration config, IServiceProvider services)
    {
        _config = config;
        _services = services;
        var discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = true
        };
        _client = new DiscordSocketClient(discordConfig);

        // 👇 關鍵新增：把 Discord 內部的日誌接到我們的主控台！
        _client.Log += LogAsync;
    }

    // 👇 關鍵新增：負責把訊息印出來的方法
    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine($"[Discord.NET] {log.ToString()}");
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.MessageReceived += HandleMessageAsync;
        
        try 
        {
            // 嘗試登入與連線
            string token = _config["DiscordToken"] ?? throw new ArgumentNullException("DiscordToken is missing!");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            // 捕捉任何致命錯誤並印出來
            Console.WriteLine($"[CRITICAL ERROR] 機器人啟動失敗: {ex.Message}");
        }
    }

    private async Task HandleMessageAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot) return;
        if (msg.Channel.Id != TARGET_CHANNEL_ID) return;

        using var scope = _services.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<AIService>();
        var stockService = scope.ServiceProvider.GetRequiredService<StockService>();
        var memoryService = scope.ServiceProvider.GetRequiredService<MemoryService>();

        string intent = await aiService.DetectIntentAsync(msg.Content);
        switch (intent)
        {
            case "stock":
                await HandleStockRequest(msg, aiService, stockService, memoryService);
                break;
            default:
                await HandleChat(msg, aiService, memoryService);
                break;
        }
    }

    private async Task HandleStockRequest(SocketMessage msg, AIService ai, StockService stock, MemoryService mem)
    {
        var userPref = await mem.GetUserPreferenceAsync(msg.Author.Id);
        if (userPref == null)
        {
            await msg.Channel.SendMessageAsync("請問您偏好哪種類型的股票？例如：科技股、能源股、ETF、短期交易等。");
            return;
        }

        string analysis = await ai.GenerateStockAdviceAsync(userPref.PreferredSectors ?? "", msg.Content);
        if (msg.Content.Contains("圖表") || msg.Content.Contains("走勢"))
        {
            string chartUrl = await stock.GenerateChartAsync(userPref.PreferredSectors ?? "");
            var embed = new EmbedBuilder()
                .WithTitle("股票走勢參考圖")
                .WithImageUrl(chartUrl)
                .WithDescription(analysis)
                .Build();
            await msg.Channel.SendMessageAsync(embed: embed);
        }
        else
        {
            await msg.Channel.SendMessageAsync(analysis);
        }
    }

    private async Task HandleChat(SocketMessage msg, AIService ai, MemoryService mem)
    {
        string reply = await ai.ChatAsync(msg.Author.Username, msg.Content);
        await msg.Channel.SendMessageAsync(reply);
    }
}
