using StockBot.Services;
using StockBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 👇 1. 修改資料庫路徑：直接放在根目錄，避免找不到 data 資料夾的問題
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlite("Data Source=stockbot.db")); 

builder.Services.AddSingleton<AIService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddHostedService<DiscordHostedService>();

var app = builder.Build();

// 👇 2. 自動建立資料庫與資料表 (這步超重要！沒有它大師會失憶)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => "Stock Bot is running!");

app.Run();
