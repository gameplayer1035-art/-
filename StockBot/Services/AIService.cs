using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StockBot.Services;

public class AIService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    
    // 1. 改成你提供的第三方 API 網址 (記得結尾要加上 chat/completions 才是完整的對話端點)
    private const string API_URL = "https://free.v36.cm/v1/chat/completions"; 

    public AIService(IConfiguration config)
    {
        _http = new HttpClient();
        // 2. 這裡改為讀取名為 OpenAIApiKey 的環境變數
        _apiKey = config["OpenAIApiKey"];
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        
        // 根據你提供的 Python 範例，加上這個預設的 Header
        _http.DefaultRequestHeaders.Add("x-foo", "true"); 
    }

    public async Task<string> DetectIntentAsync(string userMessage)
    {
        string systemPrompt = "你是一個意圖分類器。根據用戶消息，只返回一個單詞：'stock'（股票相關）或 'chat'（閒聊）。";
        var response = await CallLLMAsync(systemPrompt, userMessage);
        return response.ToLower().Contains("stock") ? "stock" : "chat";
    }

    public async Task<string> GenerateStockAdviceAsync(string sectors, string query)
    {
        string prompt = $"用戶偏好：{sectors}。請根據當前市場情況，給出關於“{query}”的股票分析和建議。";
        return await CallLLMAsync(prompt, "");
    }

    public async Task<string> ChatAsync(string userName, string message)
    {
        string prompt = $"你是一個友好的股票助手，與用戶 {userName} 聊天。";
        return await CallLLMAsync(prompt, message);
    }

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
            // 3. 改成你要的 GPT 模型
            model = "gpt-4o-mini", 
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.7,
            max_tokens = 1024
        };
        
        var response = await _http.PostAsJsonAsync(API_URL, body);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorDetails = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[自訂 API 錯誤] 狀態碼: {response.StatusCode}");
            Console.WriteLine($"[自訂 API 錯誤] 詳細內容: {errorDetails}");
            
            return $"AI 系統發生錯誤，無法回應。狀態碼: {response.StatusCode}"; 
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
