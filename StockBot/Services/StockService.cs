public class StockService
{
    private readonly HttpClient _http;

    public StockService() => _http = new HttpClient();

    // 使用 QuickChart 生成图表
    public async Task<string> GenerateChartAsync(string sector)
    {
        // 示例：生成一个随机走势图（实际可接入实时数据 API）
        string chartConfig = @"{
            type: 'line',
            data: {
                labels: ['周一','周二','周三','周四','周五'],
                datasets: [{ label: '" + sector + @"', data: [95, 102, 98, 105, 110] }]
            }
        }";
        string encoded = Uri.EscapeDataString(chartConfig);
        string url = $"https://quickchart.io/chart?c={encoded}";
        return url; // 直接返回图片 URL
    }

    // 搜索新闻并审核（使用 NewsAPI + AI 过滤）
    public async Task<List<string>> GetFilteredNewsAsync(string query, AIService ai)
    {
        string apiKey = "your_newsapi_key";
        string newsUrl = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}&apiKey={apiKey}";
        var response = await _http.GetFromJsonAsync<JsonElement>(newsUrl);
        var articles = new List<string>();
        foreach (var article in response.GetProperty("articles").EnumerateArray())
        {
            string title = article.GetProperty("title").GetString();
            string url = article.GetProperty("url").GetString();
            // 让 AI 审核标题是否可靠
            bool reliable = await ai.CheckArticleReliabilityAsync(title);
            if (reliable) articles.Add($"[{title}]({url})");
        }
        return articles;
    }
}