using Microsoft.EntityFrameworkCore;
using StockBot.Data;

var builder = WebApplication.CreateBuilder(args);

// 注册服务
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlite("Data Source=/app/data/stockbot.db")); // Railway 持久化路径

builder.Services.AddSingleton<AIService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddHostedService<DiscordHostedService>();

var app = builder.Build();
app.MapGet("/", () => "Stock Bot is running!");
app.Run();
