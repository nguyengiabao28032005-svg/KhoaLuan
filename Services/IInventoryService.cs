using System;
using System.Threading.Tasks;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public interface IInventoryService
    {
        /// <summary>
        /// Tạo phiếu nhập hàng (nhập trong nước hoặc nhập khẩu) và tự động cập nhật:
        /// 1. Kiểm tra ràng buộc cảng nhập khẩu nếu là phiếu "Import" (chỉ tỉnh có cảng đang mở mới được nhập khẩu).
        /// 2. Tồn kho biến thể (+quantity) tại kho được chỉ định
        /// 3. Ghi nhận giá vốn gốc của biến thể (OriginalPrice = importPrice)
        /// 4. Lưu lịch sử biến động kho (InventoryLog)
        /// Toàn bộ thao tác thực thi trong một Database Transaction.
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
    }
}
