using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tạo phiếu nhập hàng và thực hiện cập nhật đồng bộ nhiều bảng qua Transaction:
        /// - Thêm StockReceipt
        /// - Cập nhật số lượng tồn kho trong bảng Inventories
        /// - Cập nhật giá gốc (OriginalPrice) cho biến thể tương ứng
        /// - Thêm bản ghi lịch sử InventoryLog
        /// </summary>
        public async Task<(bool Success, string Message, StockReceipt? Receipt)> CreateReceiptAsync(
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
            string? customsDeclarationNumber = null)
        {
            if (quantity <= 0)
            {
                return (false, "Số lượng nhập kho phải lớn hơn 0.", null);
            }

            if (importPrice <= 0)
            {
                return (false, "Giá nhập kho phải lớn hơn 0 VNĐ.", null);
            }

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null)
            {
                return (false, "Không tìm thấy biến thể sản phẩm cần nhập kho.", null);
            }

            // 1. Kiểm tra kho lưu trữ
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null)
            {
                return (false, "Không tìm thấy kho hàng được chỉ định.", null);
            }

            if (!store.IsActive)
            {
                return (false, $"Kho hàng '{store.Name}' hiện đang ngừng hoạt động, không thể nhập hàng.", null);
            }

            // Kiểm tra phạm vi kho (UserWarehouseScope) nếu người thực hiện không phải Admin
            if (userId.HasValue)
            {
                var actingUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.WarehouseScopes)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (actingUser != null && actingUser.Role?.Name != AppRoles.Admin)
                {
                    bool hasStoreScope = actingUser.WarehouseScopes.Any(ws => ws.StoreId == storeId);
                    if (!hasStoreScope)
                    {
                        return (false, $"Truy cập bị từ chối: Bạn không có phạm vi dữ liệu tại '{store.Name}', không thể lập phiếu nhập cho kho này.", null);
                    }
                }
            }

            // Chuẩn hóa loại phiếu nhập
            string normalizedReceiptType = string.Equals(receiptType?.Trim(), "Import", StringComparison.OrdinalIgnoreCase)
                ? "Import"
                : "Domestic";

            ImportPort? port = null;

            // 2. RÀNG BUỘC PHIẾU NHẬP KHẨU (IMPORT)
            if (normalizedReceiptType == "Import")
            {
                // Bắt buộc phải chọn cảng nhập
                if (!importPortId.HasValue || importPortId.Value <= 0)
                {
                    return (false, "Vui lòng chọn cảng nhập khẩu (cảng biển hoặc sân bay quốc tế) cho phiếu nhập khẩu.", null);
                }

                port = await _context.ImportPorts.FirstOrDefaultAsync(p => p.Id == importPortId.Value);
                if (port == null)
                {
                    return (false, "Không tìm thấy thông tin cảng nhập khẩu được chọn trong hệ thống.", null);
                }

                // Cảng phải đang mở tiếp nhận (AllowImport == true)
                if (!port.AllowImport)
                {
                    return (false, $"Cảng '{port.Name}' ({port.ProvinceName}) hiện đang tạm đóng tiếp nhận nhập khẩu (AllowImport = false). Không thể thông quan nhập khẩu qua cảng này.", null);
                }

                // Kho bắt buộc phải gắn tỉnh/thành
                if (string.IsNullOrWhiteSpace(store.ProvinceName) && string.IsNullOrWhiteSpace(store.ProvinceCode))
                {
                    return (false, $"Kho '{store.Name}' chưa được cấu hình thông tin tỉnh/thành phố, không thể xác minh điều kiện cùng địa bàn với cảng nhập khẩu.", null);
                }

                // Kho phải cùng tỉnh/thành với cảng nhập
                bool isSameProvince = (!string.IsNullOrEmpty(store.ProvinceCode) && !string.IsNullOrEmpty(port.ProvinceCode) && store.ProvinceCode == port.ProvinceCode) ||
                                      (!string.IsNullOrEmpty(store.ProvinceName) && !string.IsNullOrEmpty(port.ProvinceName) && (
                                          store.ProvinceName.Equals(port.ProvinceName, StringComparison.OrdinalIgnoreCase) ||
                                          store.ProvinceName.ToLower().Contains(port.ProvinceName.ToLower()) ||
                                          port.ProvinceName.ToLower().Contains(store.ProvinceName.ToLower())
                                      ));

                if (!isSameProvince)
                {
                    return (false, $"Kho tiếp nhận '{store.Name}' thuộc địa bàn '{store.ProvinceName}', không cùng tỉnh/thành phố với cảng nhập khẩu '{port.Name}' thuộc '{port.ProvinceName}'. Theo quy định, hàng hóa nhập khẩu từ cảng phải được nhập về kho lưu trữ thuộc cùng tỉnh/thành của cảng thông quan.", null);
                }
            }
            else
            {
                // Phiếu nhập trong nước thì không có ô cảng và không ràng buộc
                importPortId = null;
                customsDeclarationNumber = null;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 3. Tạo mã phiếu nhập tự động
                string typePrefix = normalizedReceiptType == "Import" ? "NK" : "TN";
                string datePrefix = DateTime.Now.ToString("yyyyMMdd");
                int todayCount = await _context.StockReceipts.CountAsync(r => r.CreatedAt.Date == DateTime.Today) + 1;
                string receiptCode = $"PN-{typePrefix}-{datePrefix}-{todayCount:D4}";

                var actualImportDate = importDate ?? DateTime.Now;

                // 4. Tạo bản ghi phiếu nhập kho
                var receipt = new StockReceipt
                {
                    ReceiptCode = receiptCode,
                    VariantId = variantId,
                    StoreId = store.Id,
                    ImportPortId = importPortId,
                    CustomsDeclarationNumber = customsDeclarationNumber?.Trim(),
                    ReceiptType = normalizedReceiptType,
                    Quantity = quantity,
                    ImportPrice = importPrice,
                    Supplier = supplier?.Trim(),
                    ImportDate = actualImportDate,
                    CreatedByUserId = userId,
                    CreatedByName = !string.IsNullOrWhiteSpace(userName) ? userName : "Nhân viên kho",
                    Note = note?.Trim(),
                    CreatedAt = DateTime.Now
                };
                _context.StockReceipts.Add(receipt);

                // 5. Cập nhật tồn kho biến thể tại kho được chọn
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.VariantId == variantId && i.StoreId == store.Id);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        StoreId = store.Id,
                        VariantId = variantId,
                        StockQuantity = quantity,
                        ReservedQuantity = 0
                    };
                    _context.Inventories.Add(inventory);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    inventory.StockQuantity += quantity;
                }

                // 6. Cập nhật giá gốc (giá vốn) cho biến thể từ phiếu nhập mới nhất
                variant.OriginalPrice = importPrice;

                // 7. Ghi nhật ký biến động kho (InventoryLog)
                string receiptTypeLabel = normalizedReceiptType == "Import" ? "Nhập khẩu" : "Nhập trong nước";
                string portLog = port != null ? $" qua {port.Name}" : "";
                string customsLog = !string.IsNullOrEmpty(customsDeclarationNumber) ? $" (Tờ khai/Vận đơn: {customsDeclarationNumber.Trim()})" : "";

                _context.InventoryLogs.Add(new InventoryLog
                {
                    InventoryId = inventory.Id,
                    Inventory = inventory,
                    ChangeType = "Import",
                    QuantityChanged = quantity,
                    Note = $"{receiptTypeLabel} #{receiptCode}{portLog} tại {store.Name}: +{quantity} sp bởi {receipt.CreatedByName}{customsLog}" + (!string.IsNullOrEmpty(supplier) ? $" (NCC: {supplier})" : ""),
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Đã tạo phiếu nhập kho ({receiptTypeLabel}) {receiptCode} thành công! Nhập +{quantity} sản phẩm \"{variant.Product?.Name}\" vào kho \"{store.Name}\". Tồn kho hiện tại: {inventory.StockQuantity}.", receipt);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi tạo phiếu nhập kho: " + (ex.InnerException?.Message ?? ex.Message), null);
            }
        }
    }
}
