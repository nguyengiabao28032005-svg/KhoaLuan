using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BeautyShop.Models;
using BeautyShop.ViewModels;

namespace BeautyShop.Services
{
    public interface IInventoryService
    {
        /// <summary>
        /// Tạo phiếu nhập hàng một mặt hàng (Duy trì tương thích ngược với unit test nền tảng)
        /// </summary>
        Task<(bool Success, string Message, StockReceipt? Receipt)> CreateReceiptAsync(
            int variantId,
            int quantity,
            decimal importPrice,
            string? supplier,
            DateTime? importDate,
            int? userId,
            string? userName,
            string? note,
            int storeId = 1,
            string receiptType = "Domestic",
            int? importPortId = null,
            string? customsDeclarationNumber = null);

        /// <summary>
        /// Tạo phiếu nhập hàng nhiều sản phẩm (Yêu cầu 1: Không cho phiếu chỉ có 1 sản phẩm)
        /// - Bắt buộc >= 2 sản phẩm khác nhau
        /// - Cập nhật tồn kho cho từng sản phẩm tại kho được chọn
        /// - Cập nhật giá gốc (OriginalPrice) cho từng biến thể
        /// - Cho phép định lại giá bán lẻ mới nếu cần (Yêu cầu 10)
        /// - Quản lý công nợ, hóa đơn, xuất xứ (Yêu cầu 5)
        /// </summary>
        Task<(bool Success, string Message, StockReceipt? Receipt)> CreateMultiItemReceiptAsync(
            CreateStockReceiptViewModel model,
            int? userId,
            string? userName);

        /// <summary>
        /// Cập nhật thanh toán / trả nợ cho phiếu nhập hàng
        /// </summary>
        Task<(bool Success, string Message)> UpdatePaymentAsync(
            int receiptId,
            decimal additionalPayment,
            string? note);

        /// <summary>
        /// Gợi ý và so sánh giá nhập của sản phẩm tương tự từ các nhà cung cấp khác (Yêu cầu 9)
        /// </summary>
        Task<List<PriceComparisonItemViewModel>> GetPriceComparisonAsync(int variantId);
    }
}
