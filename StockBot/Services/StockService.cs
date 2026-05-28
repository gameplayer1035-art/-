using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace StockBot.Services;

public class StockService
{
    private readonly HttpClient _http;

    public StockService() => _http = new HttpClient();

    // 使用 QuickChart 生成圖表
    public async Task<string> GenerateChartAsync(string sector)
    {
        string chartConfig = @"{
            type: 'line',
            data: {
                labels: ['週一','週二','週三','週四','週五'],
                datasets: [{ label: '" + sector + @"', data: [95, 102, 98, 105, 110] }]
            }
        }";
        string encoded = Uri.EscapeDataString(chartConfig);
        string url = $"https://quickchart.io/chart?c={encoded}";
        return await Task.FromResult(url); 
    }

    // 搜索新聞並審核（使用 NewsAPI + AI 過濾）
    public async Task<List<string>> GetFilteredNewsAsync(string query, AIService ai)
    {
        string apiKey = "your_newsapi_key";
        string newsUrl = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}&apiKey={apiKey}";
        var response = await _http.GetFromJsonAsync<JsonElement>(newsUrl);
        var articles = new List<string>();
        
        if (response.TryGetProperty("articles", out var articlesProp))
        {
            foreach (var article in articlesProp.EnumerateArray())
            {
                string title = article.GetProperty("title").GetString() ?? "";
                string url = article.GetProperty("url").GetString() ?? "";
                
                // 呼叫 AI 審核標題是否可靠
                bool reliable = await ai.CheckArticleReliabilityAsync(title);
                if (reliable) articles.Add($"[{title}]({url})");
            }
        }
        return articles;
    }
}
