using System;
using System.Collections.Generic;
using System.Linq;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Services
{
    public class AprioriService
    {
        private readonly ApplicationDbContext _context;

        public AprioriService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gộp các sản phẩm được mua nhiều nhất thành Combo.
        /// (Yêu cầu mới: Tính số lần mua CỦA TỪNG SẢN PHẨM độc lập, sau đó gộp các sp phổ biến lại, 
        /// không bắt buộc chúng phải từng được mua chung trong 1 đơn).
        /// </summary>
        /// <param name="minSupport">Số lần mua tối thiểu CỦA TỪNG MÓN để được xét vào Combo (vd: 2 lần)</param>
        /// <param name="maxComboSize">Số sản phẩm tối đa gộp vào 1 combo (vd: 3 món)</param>
        /// <param name="maxCombos">Số combo tối đa hiện ra trang chủ (vd: 4 combo)</param>
        public List<ComboSuggestion> FindFrequentCombos(int minSupport = 2, int maxComboSize = 3, int maxCombos = 4)
        {
            // Bước 1: Lấy toàn bộ đơn hàng đã hoàn thành cùng chi tiết sản phẩm
            var orders = _context.Orders
                .Where(o => o.OrderStatus == "Completed" || o.OrderStatus == "Confirmed")
                .Include(o => o.OrderDetails!)
                    .ThenInclude(d => d.Variant!)
                        .ThenInclude(v => v!.Product)
                .ToList();

            if (!orders.Any()) return new List<ComboSuggestion>();

            // Lấy danh sách các ProductId từ mỗi đơn hàng
            var transactions = orders
                .Select(o => o.OrderDetails?
                    .Where(d => d.Variant?.Product != null)
                    .Select(d => d.Variant!.Product!.Id)
                    .Distinct()
                    .ToList() ?? new List<int>())
                .Where(t => t.Any())
                .ToList();

            // Bước 2: Đếm số lần xuất hiện của TỪNG sản phẩm ĐỘC LẬP (trên các đơn hàng)
            var itemSupport = new Dictionary<int, int>();
            foreach (var t in transactions)
            {
                foreach (var item in t)
                {
                    if (!itemSupport.ContainsKey(item)) itemSupport[item] = 0;
                    itemSupport[item]++;
                }
            }

            // Lọc ra các sản phẩm được mua >= minSupport lần
            var frequentItems = itemSupport
                .Where(kv => kv.Value >= minSupport)
                .Select(kv => kv.Key)
                .OrderBy(id => id)
                .ToList();

            // Nếu không có đủ 2 món hàng đạt chuẩn thì không tạo được combo
            if (frequentItems.Count < 2) return new List<ComboSuggestion>();

            // Lấy thông tin các sản phẩm từ Database
            var productMap = _context.Products
                .Where(p => frequentItems.Contains(p.Id))
                .Include(p => p.ProductVariants)
                .Include(p => p.Brand)
                .ToDictionary(p => p.Id);

            // Bước 3: Tạo TẤT CẢ các tổ hợp (Combo) kích thước từ 2 đến maxComboSize
            var generatedCombos = new Dictionary<List<int>, int>(ListComparer.Instance);

            for (int k = 2; k <= maxComboSize; k++)
            {
                if (frequentItems.Count < k) break;
                GenerateCombinations(frequentItems, 0, k, new List<int>(), generatedCombos, itemSupport);
            }

            // Bước 4: Lọc, sắp xếp và trả về danh sách combo
            var result = generatedCombos
                // Ưu tiên combo có nhiều sản phẩm, nếu bằng nhau thì ưu tiên điểm support cao
                .OrderByDescending(kv => kv.Key.Count) 
                .ThenByDescending(kv => kv.Value)      
                .Take(maxCombos)
                .Select(kv =>
                {
                    var products = kv.Key
                        .Where(id => productMap.ContainsKey(id))
                        .Select(id => productMap[id])
                        .ToList();

                    // Tính tổng giá gốc
                    decimal originalTotal = products.Sum(p =>
                        p.ProductVariants?.FirstOrDefault()?.RetailPrice ?? 0);

                    // Tính % giảm giá: 2 món = 8%, 3 món = 12%, 4 món = 15%
                    int discountPct = products.Count == 2 ? 8 :
                                     products.Count == 3 ? 12 : 15;

                    // Tính giá Combo và làm tròn đến hàng nghìn (vnd)
                    decimal comboPrice = Math.Round((originalTotal * (1 - discountPct / 100m)) / 1000m) * 1000m;

                    return new ComboSuggestion
                    {
                        Products = products,
                        OriginalTotal = originalTotal,
                        ComboPrice = comboPrice,
                        DiscountPercent = discountPct,
                        Support = kv.Value / products.Count // Support hiển thị là TB lượt mua
                    };
                })
                .Where(c => c.Products.Count >= 2 && c.OriginalTotal > 0)
                .ToList();

            return result;
        }

        /// <summary>
        /// Hàm đệ quy sinh ra các tổ hợp sản phẩm (không quan tâm thứ tự)
        /// </summary>
        private void GenerateCombinations(List<int> items, int startIndex, int comboSize, List<int> currentCombo, Dictionary<List<int>, int> result, Dictionary<int, int> itemSupport)
        {
            if (currentCombo.Count == comboSize)
            {
                // Điểm của combo = Tổng số lần mua của tất cả các món đồ (càng mua nhiều điểm càng cao)
                int score = currentCombo.Sum(id => itemSupport[id]);
                result[new List<int>(currentCombo)] = score;
                return;
            }

            for (int i = startIndex; i < items.Count; i++)
            {
                currentCombo.Add(items[i]);
                GenerateCombinations(items, i + 1, comboSize, currentCombo, result, itemSupport);
                currentCombo.RemoveAt(currentCombo.Count - 1);
            }
        }
    }

    // Comparer để dùng List<int> làm key trong Dictionary
    public class ListComparer : IEqualityComparer<List<int>>
    {
        public static readonly ListComparer Instance = new();
        public bool Equals(List<int>? x, List<int>? y) =>
            x != null && y != null && x.SequenceEqual(y);
        public int GetHashCode(List<int> obj) =>
            obj.Aggregate(17, (acc, val) => acc * 31 + val);
    }
}