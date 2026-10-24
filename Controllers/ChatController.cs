using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace BeautyShop.Controllers
{
    public class ChatController : Controller
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public ChatController(IConfiguration config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _http = httpFactory.CreateClient();
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Message))
                return Json(new { reply = "Vui long nhap cau hoi." });

            var apiKey = _config["Gemini:ApiKey"];

            var systemPrompt = "Ban la tro ly tu van my pham chuyen nghiep cua BeautyShop - cua hang my pham chinh hang. Nhiem vu: tu van san pham lam dep, duong da, trang diem, cham soc toc. Goi y san pham phu hop voi loai da va nhu cau. Huong dan cach su dung san pham. Tra loi cau hoi ve chinh sach doi tra, van chuyen. Tra loi ngan gon, than thien, chuyen nghiep bang tieng Viet, hoan thanh cau tra loi (4-8 cau). Neu cau hoi khong lien quan den my pham, nhe nhang huong khach ve chu de nay.";

            var body = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = req.Message } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 2048,
                    thinkingConfig = new { thinkingBudget = 0 }
                }
            };

            var json = JsonSerializer.Serialize(body);

            // gemini-1.5-flash / gemini-2.5-flash 
            var models = new[]
            {
                "gemini-3.5-flash",
                "gemini-3.7-flash",
                "gemini-3.6-flash",
                "gemini-flash-latest"
            };

            try
            {
                string lastError = "Khong goi duoc Gemini.";
                foreach (var model in models)
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
                    using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Content = httpContent;
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey ?? string.Empty);

                    var response = await _http.SendAsync(request);
                    var responseStr = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        lastError = $"[DEBUG] HTTP {(int)response.StatusCode}: {responseStr}";
                        continue;
                    }

                    var doc = JsonDocument.Parse(responseStr);
                    var reply = ReadGeminiText(doc.RootElement);

                    return Json(new { reply = string.IsNullOrWhiteSpace(reply) ? "Xin loi, toi chua hieu cau hoi." : reply });
                }

                return Json(new { reply = lastError });
            }
            catch (Exception ex)
            {
                return Json(new { reply = $"[DEBUG] Loi: {ex.Message}" });
            }
        }

        static string? ReadGeminiText(JsonElement root)
        {
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts))
                return null;

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                    continue;
                if (part.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
            }

            return sb.ToString().Trim();
        }
    }

    public class ChatRequest
    {
        public string? Message { get; set; }
    }
}
