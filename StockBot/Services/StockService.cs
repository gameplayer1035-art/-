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

    // 👇 升級版：使用 AI 動態生成各種圖表
    public async Task<string> GenerateDynamicChartAsync(string query, string analysis, AIService ai)
    {
        try
        {
            // 讓 AI 根據對話內容寫出圖表的 JSON 程式碼
            string chartConfig = await ai.GenerateChartConfigAsync(query, analysis);
            
            // 清理 AI 可能不小心加上的 Markdown 語法 (避免網址壞掉)
            chartConfig = chartConfig.Replace("```json", "").Replace("```", "").Trim();

            // 轉換成圖片網址 (加入 w=600&h=400&bkg=white 讓圖片清晰且背景為白)
            string encoded = Uri.EscapeDataString(chartConfig);
            return $"[https://quickchart.io/chart?c=](https://quickchart.io/chart?c=){encoded}&w=600&h=400&bkg=white";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[圖表生成錯誤] {ex.Message}");
            // 如果 AI 腦袋卡住生成失敗，退回原本的預設折線圖確保不當機
            string fallbackConfig = "{ type: 'line', data: { labels: ['週一','週二','週三','週四','週五'], datasets: [{ label: '預設走勢', data: [95, 102, 98, 105, 110] }] } }";
            return $"[https://quickchart.io/chart?c=](https://quickchart.io/chart?c=){Uri.EscapeDataString(fallbackConfig)}&w=600&h=400&bkg=white";
        }
    }

    // 搜索新聞並審核（使用 NewsAPI + AI 過濾）
    public async Task<List<string>> GetFilteredNewsAsync(string query, AIService ai)
    {
        string apiKey = "your_newsapi_key";
        string newsUrl = $"[https://newsapi.org/v2/everything?q=](https://newsapi.org/v2/everything?q=){Uri.EscapeDataString(query)}&apiKey={apiKey}";
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
