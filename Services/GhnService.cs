using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BeautyShop.Services
{
    public class GhnService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly string _token;
        private readonly int _shopId;
        private readonly string _baseUrl;

        public GhnService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
            _token = config["GHN:Token"]!;
            _shopId = int.Parse(config["GHN:ShopId"]!);
            _baseUrl = config["GHN:BaseUrl"]!;
        }

        // ==========================================
        // 1. LẤY DANH SÁCH TỈNH/THÀNH
        // ==========================================
        public async Task<JsonElement> GetProvincesAsync()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/shiip/public-api/master-data/province");
            req.Headers.Add("Token", _token);
            var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ==========================================
        // 2. LẤY DANH SÁCH QUẬN/HUYỆN theo Tỉnh
        // ==========================================
        public async Task<JsonElement> GetDistrictsAsync(int provinceId)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/shiip/public-api/master-data/district?province_id={provinceId}");
            req.Headers.Add("Token", _token);
            var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ==========================================
        // 3. LẤY DANH SÁCH PHƯỜNG/XÃ theo Quận
        // ==========================================
        public async Task<JsonElement> GetWardsAsync(int districtId)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/shiip/public-api/master-data/ward?district_id={districtId}");
            req.Headers.Add("Token", _token);
            var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ==========================================
        // 4. TÍNH PHÍ GIAO HÀNG (Quan trọng nhất)
        // ==========================================
        public async Task<int> CalculateFeeAsync(int toDistrictId, string toWardCode, int weightGram)
        {
            var fromDistrictId = int.Parse(_config["GHN:FromDistrictId"]!);
            var fromWardCode = _config["GHN:FromWardCode"]!;

            // Lấy service_id (loại dịch vụ giao hàng)
            var serviceId = await GetServiceIdAsync(fromDistrictId, toDistrictId);
            if (serviceId == 0) return 30000; // Trả về phí mặc định nếu lỗi

            var body = new
            {
                service_id = serviceId,
                from_district_id = fromDistrictId,
                from_ward_code = fromWardCode,
                to_district_id = toDistrictId,
                to_ward_code = toWardCode,
                weight = weightGram,
                length = 20,
                width = 15,
                height = 10
            };

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/shiip/public-api/v2/shipping-order/fee");
            req.Headers.Add("Token", _token);
            req.Headers.Add("ShopId", _shopId.ToString());
            req.Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            var json = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync());

            if (json.GetProperty("code").GetInt32() == 200)
            {
                return json.GetProperty("data").GetProperty("total").GetInt32();
            }
            return 30000; // Phí mặc định nếu API lỗi
        }

        // ==========================================
        // 5. LẤY SERVICE ID (Loại dịch vụ ship)
        // ==========================================
        private async Task<int> GetServiceIdAsync(int fromDistrictId, int toDistrictId)
        {
            var body = new { shop_id = _shopId, from_district = fromDistrictId, to_district = toDistrictId };
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/shiip/public-api/v2/shipping-order/available-services?" +
                $"shop_id={_shopId}&from_district={fromDistrictId}&to_district={toDistrictId}");
            req.Headers.Add("Token", _token);
            var res = await _http.SendAsync(req);
            var json = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync());

            try
            {
                // Lấy service đầu tiên trong danh sách (thường là Hỏa tốc hoặc Nhanh)
                return json.GetProperty("data")[0].GetProperty("service_id").GetInt32();
            }
            catch { return 0; }
        }

        // ==========================================
        // 6. TẠO ĐƠN VẬN CHUYỂN trên GHN
        // ==========================================
        public async Task<string?> CreateOrderAsync(string toName, string toPhone,
            string toAddress, int toDistrictId, string toWardCode,
            int codAmount, int weightGram, string clientOrderCode)
        {
            var fromDistrictId = int.Parse(_config["GHN:FromDistrictId"]!);
            var fromWardCode = _config["GHN:FromWardCode"]!;
            var serviceId = await GetServiceIdAsync(fromDistrictId, toDistrictId);

            var body = new
            {
                payment_type_id = 2, // 1=Shop trả ship, 2=Người mua trả ship
                note = "Hàng mỹ phẩm - Vui lòng gọi trước khi giao",
                required_note = "CHOXEMHANGKHONGTHU",
                client_order_code = clientOrderCode,
                to_name = toName,
                to_phone = toPhone,
                to_address = toAddress,
                to_ward_code = toWardCode,
                to_district_id = toDistrictId,
                cod_amount = codAmount,
                weight = weightGram,
                length = 20,
                width = 15,
                height = 10,
                service_id = serviceId,
                items = new[] { new { name = "Mỹ phẩm BeautyShop", quantity = 1, weight = weightGram } }
            };

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/shiip/public-api/v2/shipping-order/create");
            req.Headers.Add("Token", _token);
            req.Headers.Add("ShopId", _shopId.ToString());
            req.Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            var json = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync());

            if (json.GetProperty("code").GetInt32() == 200)
            {
                // Trả về mã vận đơn GHN (ví dụ: "5F5NH3LN")
                return json.GetProperty("data").GetProperty("order_code").GetString();
            }
            return null;
        }

        // ==========================================
        // 7. KIỂM TRA TRẠNG THÁI ĐƠN HÀNG trên GHN
        // ==========================================
        public async Task<string?> GetOrderStatusAsync(string ghnOrderCode)
        {
            var body = new { order_code = ghnOrderCode };
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/shiip/public-api/v2/shipping-order/detail");
            req.Headers.Add("Token", _token);
            req.Headers.Add("ShopId", _shopId.ToString());
            req.Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            var json = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync());

            if (json.GetProperty("code").GetInt32() == 200)
            {
                return json.GetProperty("data").GetProperty("status").GetString();
            }
            return null;
        }
    }
}