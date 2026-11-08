using System;
using System.Collections.Generic;
using System.Linq;
using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Services
{
    /// <summary>
    /// Hệ thống gợi ý sản phẩm dựa trên thuật toán
    /// User-Based Collaborative Filtering (KNN + Cosine Similarity).
    ///
    /// Nguyên lý hoạt động:
    ///   1. Xây dựng ma trận User × Product từ bảng UserBehaviors
    ///      (mỗi ô = điểm tương tác: View=1, AddToCart=2, Purchase=5)
    ///   2. Tính Cosine Similarity giữa user hiện tại và tất cả users khác
    ///   3. Chọn K người dùng có similarity cao nhất (KNN)
    ///   4. Gợi ý các sản phẩm mà K users đó tương tác nhưng user hiện tại chưa có
    /// </summary>
    public class RecommendationService
    {
        private readonly ApplicationDbContext _context;

        // Trọng số điểm cho từng loại hành vi
        private const double WEIGHT_VIEW        = 1.0;
        private const double WEIGHT_ADD_TO_CART = 2.0;
        private const double WEIGHT_PURCHASE    = 5.0;

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gợi ý sản phẩm cho user dựa trên Collaborative Filtering.
        /// Nếu không đủ dữ liệu (ít user, ít hành vi), fallback về sản phẩm bán chạy.
        /// </summary>
        /// <param name="userId">ID của user hiện tại</param>
        /// <param name="topK">Số user gần nhất để tham chiếu (KNN)</param>
        /// <param name="maxResults">Số sản phẩm gợi ý tối đa</param>
        public List<Product> GetRecommendations(int userId, int topK = 5, int maxResults = 8)
        {
            // ========================================================
            // BƯỚC 1: Tải toàn bộ hành vi người dùng từ DB
            // ========================================================
            var allBehaviors = _context.UserBehaviors
                .AsNoTracking()
                .ToList();

            // Lấy danh sách tất cả users có hành vi (trừ user hiện tại)
            var allUserIds = allBehaviors
                .Select(b => b.UserId)
                .Distinct()
                .ToList();

            // Lấy hành vi của user hiện tại
            var currentUserBehaviors = allBehaviors
                .Where(b => b.UserId == userId)
                .ToList();

            // Nếu user hiện tại chưa có hành vi nào → fallback
            if (!currentUserBehaviors.Any())
                return GetFallbackProducts(maxResults);

            // ========================================================
            // BƯỚC 2: Xây dựng ma trận User × Product
            // Mỗi ô = tổng điểm tương tác (View/AddToCart/Purchase)
            // ========================================================
            // Lấy tất cả productIds có trong hệ thống (từ behaviors)
            var allProductIds = allBehaviors
                .Select(b => b.ProductId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (allProductIds.Count == 0)
                return GetFallbackProducts(maxResults);

            // Tạo index nhanh: productId → vị trí trong vector
            var productIndex = allProductIds
                .Select((pid, idx) => new { pid, idx })
                .ToDictionary(x => x.pid, x => x.idx);

            int vectorSize = allProductIds.Count;

            // Hàm tính vector điểm cho 1 user từ danh sách behaviors
            double[] BuildVector(IEnumerable<UserBehavior> behaviors)
            {
                var vector = new double[vectorSize];
                foreach (var b in behaviors)
                {
                    if (!productIndex.ContainsKey(b.ProductId)) continue;
                    int idx = productIndex[b.ProductId];
                    vector[idx] += b.ActionType switch
                    {
                        "View"       => WEIGHT_VIEW,
                        "AddToCart"  => WEIGHT_ADD_TO_CART,
                        "Purchase"   => WEIGHT_PURCHASE,
                        _            => WEIGHT_VIEW
                    };
                }
                return vector;
            }

            // Vector của user hiện tại
            double[] currentVector = BuildVector(currentUserBehaviors);

            // ========================================================
            // BƯỚC 3: Tính Cosine Similarity với từng user khác
            //
            //           A · B
            // sim(A,B) = -------
            //           |A| × |B|
            // ========================================================
            var similarities = new List<(int UserId, double Similarity)>();

            foreach (var otherUserId in allUserIds)
            {
                if (otherUserId == userId) continue;

                var otherBehaviors = allBehaviors
                    .Where(b => b.UserId == otherUserId)
                    .ToList();

                double[] otherVector = BuildVector(otherBehaviors);

                double sim = CosineSimilarity(currentVector, otherVector);

                // Chỉ xét user có similarity > 0 (có điểm chung)
                if (sim > 0)
                    similarities.Add((otherUserId, sim));
            }

            // ========================================================
            // BƯỚC 4: KNN – Chọn K user gần nhất
            // ========================================================
            var kNearestUsers = similarities
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.UserId)
                .ToList();

            if (!kNearestUsers.Any())
                return GetFallbackProducts(maxResults);

            // ========================================================
            // BƯỚC 5: Gợi ý sản phẩm
            // Lấy sản phẩm mà K users gần nhất đã tương tác
            // nhưng user hiện tại CHƯA tương tác
            // ========================================================
            // Tập sản phẩm user hiện tại đã biết
            var seenProductIds = currentUserBehaviors
                .Select(b => b.ProductId)
                .ToHashSet();

            // Gom sản phẩm từ K neighbors, tính tổng weighted score
            var candidateScores = allBehaviors
                .Where(b => kNearestUsers.Contains(b.UserId) && !seenProductIds.Contains(b.ProductId))
                .GroupBy(b => b.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    // Weighted score: similarity của neighbor × điểm hành vi
                    Score = g.Sum(b =>
                    {
                        double neighborSim = similarities
                            .FirstOrDefault(s => s.UserId == b.UserId).Similarity;
                        double actionWeight = b.ActionType switch
                        {
                            "View"      => WEIGHT_VIEW,
                            "AddToCart" => WEIGHT_ADD_TO_CART,
                            "Purchase"  => WEIGHT_PURCHASE,
                            _           => WEIGHT_VIEW
                        };
                        return neighborSim * actionWeight;
                    })
                })
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.ProductId)
                .ToList();

            if (!candidateScores.Any())
                return GetFallbackProducts(maxResults);

            // Load thông tin đầy đủ của các sản phẩm được gợi ý
            var recommendedProducts = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActive && candidateScores.Contains(p.Id))
                .ToList();

            // Sắp xếp lại theo thứ tự score đã tính
            recommendedProducts = recommendedProducts
                .OrderBy(p => candidateScores.IndexOf(p.Id))
                .ToList();

            // Nếu ít hơn maxResults → bổ sung bằng fallback
            if (recommendedProducts.Count < maxResults)
            {
                var existingIds = recommendedProducts.Select(p => p.Id).ToHashSet();
                var fallback = GetFallbackProducts(maxResults - recommendedProducts.Count, existingIds);
                recommendedProducts.AddRange(fallback);
            }

            return recommendedProducts;
        }

        // ============================================================
        // CÔNG THỨC COSINE SIMILARITY
        // ============================================================
        /// <summary>
        /// Tính cosine similarity giữa 2 vector.
        /// Kết quả nằm trong [0, 1]: 1 = giống hoàn toàn, 0 = không liên quan.
        /// </summary>
        private static double CosineSimilarity(double[] vectorA, double[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Vectors must have the same dimension.");

            double dotProduct  = 0;
            double magnitudeA  = 0;
            double magnitudeB  = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            // Tránh chia cho 0 khi vector toàn 0
            if (magnitudeA == 0 || magnitudeB == 0) return 0;

            return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }

        // ============================================================
        // FALLBACK: Sản phẩm bán chạy khi chưa đủ dữ liệu
        // ============================================================
        /// <summary>
        /// Trả về sản phẩm bán chạy nhất (dựa trên UserBehaviors Purchase)
        /// để dùng khi không đủ dữ liệu CF.
        /// </summary>
        private List<Product> GetFallbackProducts(int count, HashSet<int>? excludeIds = null)
        {
            excludeIds ??= new HashSet<int>();

            // Tính điểm bán chạy từ hành vi Purchase
            var popularIds = _context.UserBehaviors
                .AsNoTracking()
                .Where(b => b.ActionType == "Purchase")
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Select(x => x.ProductId)
                .Take(count * 2)
                .ToList();

            var query = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActive && !excludeIds.Contains(p.Id));

            if (popularIds.Any())
                query = query.Where(p => popularIds.Contains(p.Id));

            return query
                .OrderByDescending(p => p.Id)
                .Take(count)
                .ToList();
        }
    }
}
