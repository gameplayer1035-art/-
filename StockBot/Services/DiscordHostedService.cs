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
        _client.Log += LogAsync;
    }

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
            string token = _config["DiscordToken"] ?? throw new ArgumentNullException("DiscordToken is missing!");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
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

        // 👇 新增：讓使用者可以主動設定偏好的專屬指令
        if (msg.Content.StartsWith("偏好"))
        {
            string newPref = msg.Content.Replace("偏好", "").Replace(":", "").Replace("：", "").Trim();
            await memoryService.SaveUserPreferenceAsync(msg.Author.Id, newPref);
            await msg.Channel.SendMessageAsync($"✅ 大師已將您的偏好牢牢記住：**{newPref}**！現在您可以問我股票了。");
            return;
        }

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
        string sectors = userPref?.PreferredSectors ?? "";

        // 👇 修正跳針邏輯：如果沒有偏好，直接給預設值並繼續分析，不再 return 卡死！
        if (string.IsNullOrEmpty(sectors))
        {
            await mem.SaveUserPreferenceAsync(msg.Author.Id, "綜合");
            sectors = "綜合";
            await msg.Channel.SendMessageAsync("💡 提示：我尚未記錄您的偏好，目前以「綜合」角度為您分析。\n*(您可以隨時輸入「偏好：科技股」來設定)*\n\n🔍 **大師正在發功分析中，請稍候...**");
        }
        else
        {
            // 有偏好的話，先告訴使用者正在分析，避免他們以為機器人壞掉
            await msg.Channel.SendMessageAsync($"🔍 收到！大師正在為您分析 (根據您的偏好：{sectors})，請稍候...");
        }

        // 呼叫 AI 分析 (這步會花幾秒鐘)
        string analysis = await ai.GenerateStockAdviceAsync(sectors, msg.Content);
        
        if (msg.Content.Contains("圖表") || msg.Content.Contains("走勢"))
        {
            string chartUrl = await stock.GenerateChartAsync(sectors);
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
