using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StockBot.Services;

public class AIService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    
    // 第三方 API 網址
    private const string API_URL = "https://free.v36.cm/v1/chat/completions"; 

    public AIService(IConfiguration config)
    {
        _http = new HttpClient();
        _apiKey = config["OpenAIApiKey"];
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
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
        // 👇 這裡新增了強制要求 AI 給出文章、新聞或影片連結的指令！
        string prompt = $"用戶偏好：{sectors}。請根據當前市場情況，給出關於“{query}”的股票分析和建議。\n\n【重要規定】：請務必在回答的最後一段，提供 2~3 個實用的相關真實連結，例如 Yahoo Finance 的該股票頁面、鉅亨網(cnYES)的相關新聞網址，或者是推薦的 YouTube 財經教學/分析影片搜尋連結，讓用戶可以點擊觀看更多資訊。";
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

    // 👇 教 AI 寫出各種圖表 (圓餅圖、長條圖、折線圖) 的設定檔
    public async Task<string> GenerateChartConfigAsync(string query, string analysis)
    {
        string systemPrompt = @"你是一個專業的數據視覺化專家。請根據用戶的問題與分析，生成一個給 QuickChart.io 使用的 Chart.js JSON 設定檔。
規則：
1. 【極度重要】只能輸出純 JSON 格式，絕對不要加上 ```json 或任何其他說明文字！
2. 根據問題選擇最適合的圖表類型 (type)：走勢用 'line'，比較用 'bar' (長條圖)，比例或分配用 'pie' (圓餅圖)。
3. 數據 (data) 請根據分析內容產生合理的虛擬或推估數據（例如近五天的價格、或是各家公司的市佔率）。
4. 設定檔必須包含 type, data (含 labels, datasets) 等基本屬性。";
        
        string prompt = $"用戶問題：{query}\n分析內容：{analysis}";
        return await CallLLMAsync(systemPrompt, prompt);
    }

    private async Task<string> CallLLMAsync(string system, string user)
    {
        var body = new
        {
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
