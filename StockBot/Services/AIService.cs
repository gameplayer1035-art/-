using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StockBot.Services;

public class AIService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private const string API_URL = "https://api.groq.com/openai/v1/chat/completions"; 

    public AIService(IConfiguration config)
    {
        _http = new HttpClient();
        // 修正原本將明碼寫在欄位索引處的錯誤，改為讀取 appsettings.json 內的 GroqApiKey 設定
        _apiKey = config["GroqApiKey"];
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    // 檢測用戶意圖
    public async Task<string> DetectIntentAsync(string userMessage)
    {
        string systemPrompt = "你是一個意圖分類器。根據用戶消息，只返回一個單詞：'stock'（股票相關）或 'chat'（閒聊）。";
        var response = await CallLLMAsync(systemPrompt, userMessage);
        return response.ToLower().Contains("stock") ? "stock" : "chat";
    }

    // 生成股票建議
    public async Task<string> GenerateStockAdviceAsync(string sectors, string query)
    {
        string prompt = $"用戶偏好：{sectors}。請根據當前市場情況，給出關於“{query}”的股票分析和建議。";
        return await CallLLMAsync(prompt, "");
    }

    // 閒聊
    public async Task<string> ChatAsync(string userName, string message)
    {
        string prompt = $"你是一個友好的股票助手，與用戶 {userName} 聊天。";
        return await CallLLMAsync(prompt, message);
    }

    // 新增：審核新聞標題是否可靠與相關的方法
    public async Task<bool> CheckArticleReliabilityAsync(string title)
    {
        string systemPrompt = "你是一個財經新聞審核員。判斷這個新聞標題是否與股票、財經或經濟高度相關，且非垃圾農場文。如果是，請只回答 'true'，否則只回答 'false'。";
        var response = await CallLLMAsync(systemPrompt, title);
        
        return response.ToLower().Contains("true");
    }

    private async Task<string> CallLLMAsync(string system, string user)
    {
        var body = new
        {
            model = "mixtral-8x7b-32768", 
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.7,
            max_tokens = 1024
        };
        var response = await _http.PostAsJsonAsync(API_URL, body);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
