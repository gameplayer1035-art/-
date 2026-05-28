using StockBot.Services; // 👈 關鍵！告訴程式去哪裡找那四個 Service
using StockBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 註冊資料庫服務 (Railway 持久化路徑)
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlite("Data Source=/app/data/stockbot.db")); 

// 註冊你的所有自訂服務
builder.Services.AddSingleton<AIService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddHostedService<DiscordHostedService>();

var app = builder.Build();

app.MapGet("/", () => "Stock Bot is running!");

app.Run();
