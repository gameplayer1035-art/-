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

    // 👇 1. 擴大意圖雷達：讓國際局勢、經濟、新聞都觸發分析模式
    public async Task<string> DetectIntentAsync(string userMessage)
    {
        string systemPrompt = "你是一個精準的意圖分類器。根據用戶消息，判斷用戶是否在詢問：股票、投資、經濟、國際局勢、新聞時事、匯率、公司發展等。如果是，請只返回 'stock'；如果是純粹打招呼或無意義閒聊(例如：你好、早安)，請只返回 'chat'。";
        var response = await CallLLMAsync(systemPrompt, userMessage);
        return response.ToLower().Contains("stock") ? "stock" : "chat";
    }

    // 👇 2. 終極分析大師 Prompt：加入 liveNews 參數，並強制給出新聞與 YouTube 連結
    public async Task<string> GenerateStockAdviceAsync(string sectors, string query, string liveNews)
    {
        string prompt = $@"你是一位「全方位國際財經與時事分析大師」。
用戶偏好/背景：{sectors}
用戶問題：{query}

【全球網路即時資訊 (這是剛剛從各大網站爬取的最熱門資訊，請務必參考)】：
{liveNews}

【任務要求】：
1. 分析廣度與深度：無論用戶問國際局勢、股票起伏、還是特定新聞，請結合上面的「即時資訊」給出專業、客觀且具備深度的分析。
2. 數據引用：如果新聞中有提到具體數字（股價、經濟數據、匯率等），請一定要寫出來。
3. 影片與來源連結：在回答的最下方，請你主動提供以下資訊供用戶延伸閱讀：
   - 📺 **YouTube 相關影片**：請提供 1~2 個 YouTube 搜尋連結。格式例如：[點我前往 YouTube 觀看『XXX』相關影片](https://www.youtube.com/results?search_query=XXX) (XXX請替換為適合的中文或英文搜尋關鍵字)。
   - 📰 **新聞來源連結**：請從上方的即時資訊中，挑選最重要的 1~2 篇新聞，附上其真實網址。
4. 語氣：專業、自信、一針見血，善用條列式與粗體字讓排版易讀。";

        return await CallLLMAsync(prompt, "");
    }

    public async Task<string> ChatAsync(string userName, string message)
    {
        string prompt = $"你是一個友好的智能助手，與用戶 {userName} 聊天。";
        return await CallLLMAsync(prompt, message);
    }

    public async Task<bool> CheckArticleReliabilityAsync(string title)
    {
        string systemPrompt = "你是一個新聞審核員。判斷這個標題是否與時事、財經、經濟或科技高度相關。如果是，回答 'true'，否則回答 'false'。";
        var response = await CallLLMAsync(systemPrompt, title);
        return response.ToLower().Contains("true");
    }

    // 👇 教 AI 寫出各種圖表 (圓餅圖、長條圖、折線圖) 的設定檔
    public async Task<string> GenerateChartConfigAsync(string query, string analysis)
    {
        string systemPrompt = @"你是一個專業的數據視覺化專家。請根據用戶的問題與分析，生成一個給 QuickChart.io 使用的 Chart.js JSON 設定檔。
規則：
1. 只能輸出純 JSON 格式，絕對不要加上 ```json 或任何其他說明文字！
2. 根據問題選擇最適合的圖表類型：走勢用 'line'，比較用 'bar'，比例用 'pie'。
3. 數據請根據分析內容產生合理的虛擬或推估數據。
4. 必須包含 type, data (含 labels, datasets) 等基本屬性。";
        
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
            max_tokens = 2000 // 👇 增加 Token 到 2000，讓長篇分析與連結不會被截斷
        };
        
        var response = await _http.PostAsJsonAsync(API_URL, body);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorDetails = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[自訂 API 錯誤] 狀態碼: {response.StatusCode}");
            Console.WriteLine($"[自訂 API 錯誤] 詳細內容: {errorDetails}");
            return $"AI 系統發生錯誤。狀態碼: {response.StatusCode}"; 
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
