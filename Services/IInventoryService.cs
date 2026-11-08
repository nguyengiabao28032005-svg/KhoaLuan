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
        /// Duyệt phiếu đặt hàng nhà cung cấp (Yêu cầu 2: Quản lý duyệt trước khi gửi NCC)
        /// </summary>
        Task<(bool Success, string Message)> ApprovePurchaseOrderAsync(
            int receiptId,
            int? userId,
            string? userName,
            string? note);

        /// <summary>
        /// Gửi phiếu đặt hàng sang nhà cung cấp kèm ghi nhận đặt cọc (Yêu cầu 2)
        /// </summary>
        Task<(bool Success, string Message)> SendToSupplierAsync(
            int receiptId,
            decimal depositAmount,
            DateTime? depositTransferDate,
            DateTime? depositReceivedDate,
            string? paymentMethod,
            string? note);

        /// <summary>
        /// Đối chiếu phiếu nhập với phiếu đặt hàng khi hàng về và lưu vào kho (Yêu cầu 2)
        /// - Nhận số lượng thực nhập và ngày nhận hàng từng món
        /// - Phần không nhận thì hủy, và khi thống kê chỉ tính phần đã nhập
        /// - Phiếu thanh toán chỉ tính theo số thực nhập
        /// - Khớp rồi mới cập nhật tồn kho
        /// </summary>
        Task<(bool Success, string Message, StockReceipt? Receipt)> ReconcileAndReceiveReceiptAsync(
            ReconcileStockReceiptViewModel model,
            int? userId,
            string? userName);

        /// <summary>
        /// Gợi ý và so sánh giá nhập của sản phẩm tương tự từ các nhà cung cấp khác (Yêu cầu 9)
        /// </summary>
        Task<List<PriceComparisonItemViewModel>> GetPriceComparisonAsync(int variantId);

        /// <summary>
        /// Lấy đơn giá nhập lần gần nhất của biến thể theo Nhà cung cấp cụ thể
        /// </summary>
        Task<(decimal? LastPrice, string? SupplierName, DateTime? ImportDate, string? ReceiptCode)> GetLastImportPriceAsync(
            int variantId, 
            int? supplierId);

        /// <summary>
        /// So sánh giá giữa các nhà cung cấp cho một biến thể:
        /// Tách rõ Giá chào hiện tại và Giá nhập lần gần nhất, Giá tốt nhất chỉ tính trên Giá chào hiện tại.
        /// </summary>
        Task<List<VariantSupplierPriceComparisonViewModel>> GetSupplierPriceComparisonsAsync(int variantId);
    }
}
