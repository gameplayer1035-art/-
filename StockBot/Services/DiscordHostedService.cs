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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.MessageReceived += HandleMessageAsync;
        await _client.LoginAsync(TokenType.Bot, _config["DiscordToken"]);
        await _client.StartAsync();
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot) return;
        if (msg.Channel.Id != TARGET_CHANNEL_ID) return;

        using var scope = _services.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<AIService>();
        var stockService = scope.ServiceProvider.GetRequiredService<StockService>();
        var memoryService = scope.ServiceProvider.GetRequiredService<MemoryService>();

        // 1. 先判斷用戶意圖：閒聊 or 股票分析
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

    // 处理股票请求
    private async Task HandleStockRequest(SocketMessage msg, AIService ai, StockService stock, MemoryService mem)
    {
        // 檢查用戶偏好是否已存在
        var userPref = await mem.GetUserPreferenceAsync(msg.Author.Id);
        if (userPref == null)
        {
            await msg.Channel.SendMessageAsync("請問您偏好哪種類型的股票？例如：科技股、能源股、ETF、短期交易等。");
            return;
        }

        // 調用 AI 獲取分析建議（結合用戶偏好）
        string analysis = await ai.GenerateStockAdviceAsync(userPref.PreferredSectors ?? "", msg.Content);
        // 可能需要生成圖表，先判斷是否需要圖表關鍵詞
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

    // 閒聊
    private async Task HandleChat(SocketMessage msg, AIService ai, MemoryService mem)
    {
        string reply = await ai.ChatAsync(msg.Author.Username, msg.Content);
        await msg.Channel.SendMessageAsync(reply);
    }
}
