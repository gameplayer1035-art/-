using System.Net.Http.Json;
using System.Text.Json;

public class AIService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string API_URL = "https://api.groq.com/openai/v1/chat/completions"; // 免费 LLM

    public AIService(IConfiguration config)
    {
        _http = new HttpClient();
        _apiKey = config["GroqApiKey"];
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    // 检测用户意图
    public async Task<string> DetectIntentAsync(string userMessage)
    {
        string systemPrompt = "你是一个意图分类器。根据用户消息，只返回一个单词：'stock'（股票相关）或 'chat'（闲聊）。";
        var response = await CallLLMAsync(systemPrompt, userMessage);
        return response.ToLower().Contains("stock") ? "stock" : "chat";
    }

    // 生成股票建议
    public async Task<string> GenerateStockAdviceAsync(string sectors, string query)
    {
        string prompt = $"用户偏好：{sectors}。请根据当前市场情况，给出关于“{query}”的股票分析和建议。";
        return await CallLLMAsync(prompt, "");
    }

    // 闲聊
    public async Task<string> ChatAsync(string userName, string message)
    {
        string prompt = $"你是一个友好的股票助手，与用户 {userName} 聊天。";
        return await CallLLMAsync(prompt, message);
    }

    private async Task<string> CallLLMAsync(string system, string user)
    {
        var body = new
        {
            model = "mixtral-8x7b-32768", // Groq 免费模型
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
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}