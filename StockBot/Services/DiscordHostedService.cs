using Discord;
using Discord.WebSocket;
using StockBot.Data;
using Microsoft.EntityFrameworkCore;

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

        // 1. 先判断用户意图：闲聊 or 股票分析
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
        // 检查用户偏好是否已存在
        var userPref = await mem.GetUserPreferenceAsync(msg.Author.Id);
        if (userPref == null)
        {
            await msg.Channel.SendMessageAsync("请问您偏好哪种类型的股票？例如：科技股、能源股、ETF、短期交易等。");
            // 下次对话时将自动识别并保存偏好（通过 AI 抽取）
            return;
        }

        // 调用 AI 获取分析建议（结合用户偏好）
        string analysis = await ai.GenerateStockAdviceAsync(userPref.PreferredSectors, msg.Content);
        // 可能需要生成图表，先判断是否需要图表关键词
        if (msg.Content.Contains("图表") || msg.Content.Contains("走势"))
        {
            string chartUrl = await stock.GenerateChartAsync(userPref.PreferredSectors);
            var embed = new EmbedBuilder()
                .WithTitle("股票走势参考图")
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

    // 闲聊
    private async Task HandleChat(SocketMessage msg, AIService ai, MemoryService mem)
    {
        string reply = await ai.ChatAsync(msg.Author.Username, msg.Content);
        await msg.Channel.SendMessageAsync(reply);
    }
}