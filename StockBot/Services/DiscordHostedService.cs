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

        // 偏好設定指令
        if (msg.Content.StartsWith("偏好"))
        {
            string newPref = msg.Content.Replace("偏好", "").Replace(":", "").Replace("：", "").Trim();
            await memoryService.SaveUserPreferenceAsync(msg.Author.Id, newPref);
            await msg.Channel.SendMessageAsync($"✅ 大師已將您的偏好牢牢記住：**{newPref}**！現在您可以問我股票或時事了。");
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
        string sectors = userPref?.PreferredSectors ?? "綜合";

        // 發送提示訊息，告知使用者正在「聯網搜尋」
        if (string.IsNullOrEmpty(userPref?.PreferredSectors))
        {
            await mem.SaveUserPreferenceAsync(msg.Author.Id, "綜合");
            await msg.Channel.SendMessageAsync("💡 提示：我尚未記錄您的偏好，目前以「綜合」角度為您分析。\n*(您可以隨時輸入「偏好：科技股」來設定)*\n\n🌍 **大師正在為您聯網搜尋全球資訊，請稍候...**");
        }
        else
        {
            await msg.Channel.SendMessageAsync($"🌍 收到！大師正在為您聯網搜尋全球資訊 (根據偏好：{sectors})，請稍候...");
        }

        // 👇 關鍵更新 1. 聯網抓取最新資訊 (Google 新聞爬蟲)
        string liveNews = await stock.GetLiveNewsAsync(msg.Content);

        // 👇 關鍵更新 2. 丟給 AI 進行綜合分析 (把 liveNews 當作小抄傳進去)
        string analysis = await ai.GenerateStockAdviceAsync(sectors, msg.Content, liveNews);
        
        // 擴充關鍵字：加入大量繁簡體的畫圖指令
        if (msg.Content.Contains("图表") || msg.Content.Contains("圖表") || 
            msg.Content.Contains("走势") || msg.Content.Contains("走勢") || 
            msg.Content.Contains("图片") || msg.Content.Contains("圖片") ||
            msg.Content.Contains("統計圖") || msg.Content.Contains("圓餅圖") || 
            msg.Content.Contains("長條圖") || msg.Content.Contains("折線圖"))
        {
            // 動態呼叫 AI 產生專屬圖表
            string chartUrl = await stock.GenerateDynamicChartAsync(msg.Content, analysis, ai);
            var embed = new EmbedBuilder()
                .WithTitle("📊 專屬分析圖表與資訊")
                .WithImageUrl(chartUrl)
                .WithDescription(analysis) // 這裡的文字結尾已經會包含新聞/影片連結了
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
