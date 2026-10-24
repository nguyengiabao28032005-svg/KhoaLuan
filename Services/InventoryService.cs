using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;

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
        /// Tạo phiếu nhập hàng một mặt hàng (Duy trì tương thích ngược với unit test nền tảng)
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
                if (!importPortId.HasValue || importPortId.Value <= 0)
                {
                    return (false, "Vui lòng chọn cảng nhập khẩu (cảng biển hoặc sân bay quốc tế) cho phiếu nhập khẩu.", null);
                }

                port = await _context.ImportPorts.FirstOrDefaultAsync(p => p.Id == importPortId.Value);
                if (port == null)
                {
                    return (false, "Không tìm thấy thông tin cảng nhập khẩu được chọn trong hệ thống.", null);
                }

                if (!port.AllowImport)
                {
                    return (false, $"Cảng '{port.Name}' ({port.ProvinceName}) hiện đang tạm đóng tiếp nhận nhập khẩu (AllowImport = false). Không thể thông quan nhập khẩu qua cảng này.", null);
                }

                if (string.IsNullOrWhiteSpace(store.ProvinceName) && string.IsNullOrWhiteSpace(store.ProvinceCode))
                {
                    return (false, $"Kho '{store.Name}' chưa được cấu hình thông tin tỉnh/thành phố, không thể xác minh điều kiện cùng địa bàn với cảng nhập khẩu.", null);
                }

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
                importPortId = null;
                customsDeclarationNumber = null;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string typePrefix = normalizedReceiptType == "Import" ? "NK" : "TN";
                string datePrefix = DateTime.Now.ToString("yyyyMMdd");
                int todayCount = await _context.StockReceipts.CountAsync(r => r.CreatedAt.Date == DateTime.Today) + 1;
                string receiptCode = $"PN-{typePrefix}-{datePrefix}-{todayCount:D4}";

                var actualImportDate = importDate ?? DateTime.Now;
                decimal totalAmount = quantity * importPrice;

                // Tìm hoặc gán SupplierId nếu tên nhà cung cấp khớp
                int? foundSupplierId = null;
                if (!string.IsNullOrWhiteSpace(supplier))
                {
                    var matchedSupplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name.ToLower() == supplier.Trim().ToLower());
                    foundSupplierId = matchedSupplier?.Id;
                }

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
                    SupplierId = foundSupplierId,
                    Supplier = supplier?.Trim(),
                    InvoiceNumber = null,
                    OriginCountry = normalizedReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Việt Nam",
                    PaymentStatus = "Paid",
                    TotalAmount = totalAmount,
                    PaidAmount = totalAmount,
                    DebtAmount = 0,
                    ImportDate = actualImportDate,
                    CreatedByUserId = userId,
                    CreatedByName = !string.IsNullOrWhiteSpace(userName) ? userName : "Nhân viên kho",
                    Note = note?.Trim(),
                    CreatedAt = DateTime.Now
                };
                _context.StockReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                // Tạo chi tiết mặt hàng StockReceiptItem
                var receiptItem = new StockReceiptItem
                {
                    StockReceiptId = receipt.Id,
                    VariantId = variantId,
                    Quantity = quantity,
                    ImportPrice = importPrice,
                    TotalPrice = totalAmount,
                    OldRetailPrice = variant.RetailPrice,
                    NewRetailPrice = null,
                    CreatedAt = DateTime.Now
                };
                _context.StockReceiptItems.Add(receiptItem);

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

        /// <summary>
        /// Tạo phiếu nhập hàng nhiều sản phẩm (Yêu cầu 1: Không cho phiếu chỉ có 1 sản phẩm)
        /// </summary>
        public async Task<(bool Success, string Message, StockReceipt? Receipt)> CreateMultiItemReceiptAsync(
            CreateStockReceiptViewModel model,
            int? userId,
            string? userName)
        {
            // ===== 1. RÀNG BUỘC YÊU CẦU 1: PHIẾU NHẬP PHẢI GỒM TỪ 2 SẢN PHẨM TRỞ LÊN =====
            if (model.Items == null || model.Items.Count < 2)
            {
                return (false, "Theo quy định quản lý nhập hàng theo lô/nhãn hiệu, một phiếu nhập phải bao gồm từ 2 sản phẩm trở lên (không cho phép tạo phiếu chỉ có một sản phẩm). Vui lòng thêm các sản phẩm khác thuộc cùng chuyến nhập (ví dụ: son, phấn, chì kẻ...).", null);
            }

            // Kiểm tra số sản phẩm/biến thể phân biệt
            var distinctVariantIds = model.Items.Select(i => i.VariantId).Distinct().ToList();
            if (distinctVariantIds.Count < 2)
            {
                return (false, "Một lần nhập phải gồm từ 2 sản phẩm/biến thể khác nhau trở lên, vì thực tế khi nhập cùng một nhãn hiệu sẽ nhập nhiều mặt hàng cùng lúc.", null);
            }

            // Kiểm tra từng sản phẩm
            foreach (var item in model.Items)
            {
                if (item.Quantity <= 0)
                {
                    return (false, "Số lượng nhập của mỗi sản phẩm phải lớn hơn 0.", null);
                }
                if (item.ImportPrice <= 0)
                {
                    return (false, "Đơn giá nhập của mỗi sản phẩm phải lớn hơn 0 VNĐ.", null);
                }
            }

            // ===== 2. KIỂM TRA KHO TIẾP NHẬN (YÊU CẦU 7) =====
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == model.StoreId);
            if (store == null)
            {
                return (false, "Không tìm thấy kho hàng được chỉ định tiếp nhận.", null);
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
                    bool hasStoreScope = actingUser.WarehouseScopes.Any(ws => ws.StoreId == model.StoreId);
                    if (!hasStoreScope)
                    {
                        return (false, $"Truy cập bị từ chối: Bạn không có phạm vi dữ liệu tại '{store.Name}', không thể lập phiếu nhập cho kho này.", null);
                    }
                }
            }

            // ===== 3. PHÂN BIỆT NHẬP TRONG NƯỚC VÀ QUỐC TẾ (YÊU CẦU 6) =====
            string normalizedReceiptType = string.Equals(model.ReceiptType?.Trim(), "Import", StringComparison.OrdinalIgnoreCase)
                ? "Import"
                : "Domestic";

            ImportPort? port = null;

            if (normalizedReceiptType == "Import")
            {
                if (!model.ImportPortId.HasValue || model.ImportPortId.Value <= 0)
                {
                    return (false, "Hàng quốc tế bắt buộc phải thông quan qua sân bay hoặc cảng biển quốc tế. Vui lòng chọn cảng nhập khẩu.", null);
                }

                port = await _context.ImportPorts.FirstOrDefaultAsync(p => p.Id == model.ImportPortId.Value);
                if (port == null)
                {
                    return (false, "Không tìm thấy thông tin cảng nhập khẩu được chọn trong hệ thống.", null);
                }

                if (!port.AllowImport)
                {
                    return (false, $"Cảng '{port.Name}' ({port.ProvinceName}) hiện đang tạm đóng tiếp nhận nhập khẩu. Không thể thông quan nhập khẩu qua cảng này.", null);
                }

                if (string.IsNullOrWhiteSpace(store.ProvinceName) && string.IsNullOrWhiteSpace(store.ProvinceCode))
                {
                    return (false, $"Kho '{store.Name}' chưa được cấu hình thông tin tỉnh/thành phố, không thể xác minh điều kiện cùng địa bàn với cảng thông quan.", null);
                }

                bool isSameProvince = (!string.IsNullOrEmpty(store.ProvinceCode) && !string.IsNullOrEmpty(port.ProvinceCode) && store.ProvinceCode == port.ProvinceCode) ||
                                      (!string.IsNullOrEmpty(store.ProvinceName) && !string.IsNullOrEmpty(port.ProvinceName) && (
                                          store.ProvinceName.Equals(port.ProvinceName, StringComparison.OrdinalIgnoreCase) ||
                                          store.ProvinceName.ToLower().Contains(port.ProvinceName.ToLower()) ||
                                          port.ProvinceName.ToLower().Contains(store.ProvinceName.ToLower())
                                      ));

                if (!isSameProvince)
                {
                    return (false, $"Kho tiếp nhận '{store.Name}' ({store.ProvinceName}) không cùng tỉnh/thành phố với cảng nhập khẩu '{port.Name}' ({port.ProvinceName}). Hàng nhập khẩu từ cảng phải được tiếp nhận ban đầu về kho tổng hoặc kho trung chuyển thuộc cùng tỉnh/thành của cảng thông quan.", null);
                }
            }
            else
            {
                model.ImportPortId = null;
                model.CustomsDeclarationNumber = null;
            }

            // ===== 4. NHÀ CUNG CẤP & XUẤT XỨ (YÊU CẦU 2 & 5) =====
            Supplier? supplier = null;
            if (model.SupplierId.HasValue && model.SupplierId.Value > 0)
            {
                supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(model.SupplierName))
            {
                supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name.ToLower() == model.SupplierName.Trim().ToLower());
            }

            string finalSupplierName = supplier?.Name ?? model.SupplierName?.Trim() ?? "Nhà cung cấp chưa đặt tên";
            string originCountry = !string.IsNullOrWhiteSpace(model.OriginCountry) 
                ? model.OriginCountry.Trim() 
                : (supplier != null ? supplier.Country : (normalizedReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Việt Nam"));

            // ===== 5. TÍNH TOÁN THANH TOÁN VÀ CÔNG NỢ (YÊU CẦU 5) =====
            decimal totalAmount = model.Items.Sum(i => i.Quantity * i.ImportPrice);
            decimal paidAmount = 0;
            decimal debtAmount = 0;
            string paymentStatus = model.PaymentStatus?.Trim() ?? "Paid";

            if (paymentStatus == "Paid")
            {
                paidAmount = totalAmount;
                debtAmount = 0;
            }
            else if (paymentStatus == "Unpaid")
            {
                paidAmount = 0;
                debtAmount = totalAmount;
            }
            else // Partial
            {
                paidAmount = Math.Clamp(model.PaidAmount, 0, totalAmount);
                debtAmount = totalAmount - paidAmount;
                if (paidAmount == totalAmount) paymentStatus = "Paid";
                else if (paidAmount == 0) paymentStatus = "Unpaid";
            }

            // Kiểm tra danh sách biến thể trong DB
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => distinctVariantIds.Contains(v.Id))
                .ToListAsync();

            if (variants.Count < distinctVariantIds.Count)
            {
                return (false, "Một số sản phẩm/biến thể được chọn không tồn tại trong hệ thống.", null);
            }

            // Bắt đầu Transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string typePrefix = normalizedReceiptType == "Import" ? "NK" : "TN";
                string datePrefix = DateTime.Now.ToString("yyyyMMdd");
                int todayCount = await _context.StockReceipts.CountAsync(r => r.CreatedAt.Date == DateTime.Today) + 1;
                string receiptCode = $"PN-{typePrefix}-{datePrefix}-{todayCount:D4}";

                var actualImportDate = model.ImportDate;
                var firstItem = model.Items.First();

                var receipt = new StockReceipt
                {
                    ReceiptCode = receiptCode,
                    VariantId = firstItem.VariantId,
                    StoreId = store.Id,
                    ImportPortId = model.ImportPortId,
                    CustomsDeclarationNumber = model.CustomsDeclarationNumber?.Trim(),
                    ReceiptType = normalizedReceiptType,
                    Quantity = model.Items.Sum(i => i.Quantity),
                    ImportPrice = firstItem.ImportPrice,
                    SupplierId = supplier?.Id,
                    Supplier = finalSupplierName,
                    InvoiceNumber = model.InvoiceNumber?.Trim(),
                    OriginCountry = originCountry,
                    PaymentStatus = paymentStatus,
                    TotalAmount = totalAmount,
                    PaidAmount = paidAmount,
                    DebtAmount = debtAmount,
                    ImportDate = actualImportDate,
                    CreatedByUserId = userId,
                    CreatedByName = !string.IsNullOrWhiteSpace(userName) ? userName : "Nhân viên kho",
                    Note = model.Note?.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.StockReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                // Lưu từng dòng chi tiết sản phẩm và cập nhật tồn kho, giá vốn, giá bán lẻ (Yêu cầu 1 & 10)
                int totalQuantity = 0;
                List<string> itemLogSummaries = new();

                foreach (var item in model.Items)
                {
                    var variant = variants.First(v => v.Id == item.VariantId);
                    decimal lineTotal = item.Quantity * item.ImportPrice;
                    decimal? oldPrice = variant.RetailPrice;

                    var receiptItem = new StockReceiptItem
                    {
                        StockReceiptId = receipt.Id,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity,
                        ImportPrice = item.ImportPrice,
                        TotalPrice = lineTotal,
                        OldRetailPrice = oldPrice,
                        NewRetailPrice = item.NewRetailPrice,
                        CreatedAt = DateTime.Now
                    };
                    _context.StockReceiptItems.Add(receiptItem);

                    // Cập nhật tồn kho biến thể tại kho được chọn
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.VariantId == item.VariantId && i.StoreId == store.Id);

                    if (inventory == null)
                    {
                        inventory = new Inventory
                        {
                            StoreId = store.Id,
                            VariantId = item.VariantId,
                            StockQuantity = item.Quantity,
                            ReservedQuantity = 0
                        };
                        _context.Inventories.Add(inventory);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        inventory.StockQuantity += item.Quantity;
                    }

                    // Cập nhật giá vốn tham chiếu
                    variant.OriginalPrice = item.ImportPrice;

                    // YÊU CẦU 10: ĐỊNH LẠI GIÁ BÁN LẺ NẾU CÓ THAY ĐỔI
                    if (item.NewRetailPrice.HasValue && item.NewRetailPrice.Value > 0)
                    {
                        variant.RetailPrice = item.NewRetailPrice.Value;
                    }

                    // Ghi nhật ký biến động kho
                    string customsLog = !string.IsNullOrEmpty(model.CustomsDeclarationNumber) ? $" (TK: {model.CustomsDeclarationNumber.Trim()})" : "";
                    string priceChangeLog = (item.NewRetailPrice.HasValue && item.NewRetailPrice.Value > 0) 
                        ? $" | Giá bán mới: {item.NewRetailPrice.Value:N0}đ (Cũ: {oldPrice?.ToString("N0") ?? "Chưa có"}đ)" 
                        : "";

                    _context.InventoryLogs.Add(new InventoryLog
                    {
                        InventoryId = inventory.Id,
                        Inventory = inventory,
                        ChangeType = "Import",
                        QuantityChanged = item.Quantity,
                        Note = $"{normalizedReceiptType} #{receiptCode} tại {store.Name}: +{item.Quantity} {variant.Product?.Name} ({variant.SKU}) bởi {receipt.CreatedByName}{customsLog} [Giá nhập: {item.ImportPrice:N0}đ{priceChangeLog}] (NCC: {finalSupplierName})",
                        CreatedAt = DateTime.Now
                    });

                    totalQuantity += item.Quantity;
                    itemLogSummaries.Add($"{variant.Product?.Name}: +{item.Quantity}");
                }

                // Nếu chọn nhà cung cấp và sản phẩm chưa có trong bảng Cung ứng -> Tự động thêm vào bảng Cung ứng
                if (supplier != null)
                {
                    var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
                    foreach (var pid in productIds)
                    {
                        bool exists = await _context.SupplierProducts.AnyAsync(sp => sp.SupplierId == supplier.Id && sp.ProductId == pid);
                        if (!exists)
                        {
                            var sampleItem = model.Items.FirstOrDefault(i => variants.Any(v => v.Id == i.VariantId && v.ProductId == pid));
                            _context.SupplierProducts.Add(new SupplierProduct
                            {
                                SupplierId = supplier.Id,
                                ProductId = pid,
                                SuppliedPrice = sampleItem?.ImportPrice,
                                Notes = $"Tự động ghi nhận từ phiếu nhập {receiptCode}",
                                CreatedAt = DateTime.Now
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string receiptTypeLabel = normalizedReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Nhập trong nước";
                return (true, $"Tạo thành công phiếu nhập {receiptCode} ({receiptTypeLabel}) gồm {model.Items.Count} mặt hàng (tổng {totalQuantity} sản phẩm) vào kho \"{store.Name}\". Tổng giá trị: {totalAmount:N0} VNĐ. Trạng thái: {(paymentStatus == "Paid" ? "Đã thanh toán" : paymentStatus == "Unpaid" ? "Chưa thanh toán (Công nợ)" : "Thanh toán một phần")}.", receipt);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi lưu phiếu nhập kho: " + (ex.InnerException?.Message ?? ex.Message), null);
            }
        }

        /// <summary>
        /// Cập nhật thanh toán và công nợ phiếu nhập (Yêu cầu 5)
        /// </summary>
        public async Task<(bool Success, string Message)> UpdatePaymentAsync(int receiptId, decimal additionalPayment, string? note)
        {
            if (additionalPayment <= 0)
            {
                return (false, "Số tiền thanh toán phải lớn hơn 0 VNĐ.");
            }

            var receipt = await _context.StockReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null)
            {
                return (false, "Không tìm thấy phiếu nhập cần thanh toán.");
            }

            if (receipt.PaymentStatus == "Paid" || receipt.DebtAmount <= 0)
            {
                return (false, $"Phiếu nhập {receipt.ReceiptCode} đã được thanh toán toàn bộ, không còn công nợ.");
            }

            receipt.PaidAmount += additionalPayment;
            if (receipt.PaidAmount >= receipt.TotalAmount)
            {
                receipt.PaidAmount = receipt.TotalAmount;
                receipt.DebtAmount = 0;
                receipt.PaymentStatus = "Paid";
            }
            else
            {
                receipt.DebtAmount = receipt.TotalAmount - receipt.PaidAmount;
                receipt.PaymentStatus = "Partial";
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                receipt.Note = string.IsNullOrEmpty(receipt.Note) 
                    ? $"[Thanh toán {DateTime.Now:dd/MM/yyyy}: +{additionalPayment:N0}đ - {note}]"
                    : $"{receipt.Note} | [Thanh toán {DateTime.Now:dd/MM/yyyy}: +{additionalPayment:N0}đ - {note}]";
            }

            await _context.SaveChangesAsync();
            return (true, $"Đã cập nhật thanh toán +{additionalPayment:N0}đ cho phiếu nhập {receipt.ReceiptCode}. Công nợ còn lại: {receipt.DebtAmount:N0} VNĐ.");
        }

        /// <summary>
        /// Gợi ý và so sánh giá nhập của sản phẩm tương tự từ các nhà cung cấp khác (Yêu cầu 9)
        /// </summary>
        public async Task<List<PriceComparisonItemViewModel>> GetPriceComparisonAsync(int variantId)
        {
            var targetVariant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (targetVariant == null) return new List<PriceComparisonItemViewModel>();

            var results = new List<PriceComparisonItemViewModel>();

            // 1. Lấy lịch sử giá nhập của chính biến thể này từ các phiếu nhập trước
            var pastReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.VariantId == variantId && !string.IsNullOrEmpty(i.StockReceipt.Supplier))
                .OrderByDescending(i => i.StockReceipt.ImportDate)
                .Take(10)
                .ToListAsync();

            foreach (var item in pastReceiptItems)
            {
                results.Add(new PriceComparisonItemViewModel
                {
                    SupplierName = item.StockReceipt?.Supplier ?? "N/A",
                    ProductName = targetVariant.Product?.Name,
                    SKU = targetVariant.SKU,
                    Volume = targetVariant.Volume,
                    ColorName = targetVariant.ColorName,
                    ImportPrice = item.ImportPrice,
                    ImportDate = item.StockReceipt?.ImportDate,
                    SourceType = $"Đơn nhập {item.StockReceipt?.ReceiptCode} ({item.StockReceipt?.ImportDate:dd/MM/yyyy})"
                });
            }

            // 2. Lấy giá từ bảng trung gian Cung ứng (SupplierProduct) của sản phẩm này
            var suppliedPrices = await _context.SupplierProducts
                .Include(sp => sp.Supplier)
                .Include(sp => sp.Product)
                .Where(sp => sp.ProductId == targetVariant.ProductId && sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0)
                .ToListAsync();

            foreach (var sp in suppliedPrices)
            {
                // Tránh trùng nếu đã có trong lịch sử gần nhất
                if (!results.Any(r => r.SupplierName.Equals(sp.Supplier?.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new PriceComparisonItemViewModel
                    {
                        SupplierName = sp.Supplier?.Name ?? "N/A",
                        SupplierPhone = sp.Supplier?.Phone,
                        SupplierAddress = sp.Supplier?.Address,
                        ProductName = targetVariant.Product?.Name,
                        SKU = targetVariant.SKU,
                        Volume = targetVariant.Volume,
                        ColorName = targetVariant.ColorName,
                        ImportPrice = sp.SuppliedPrice!.Value,
                        ImportDate = sp.CreatedAt,
                        SourceType = "Bảng giá cung ứng tham chiếu"
                    });
                }
            }

            // 3. Nếu chưa đủ so sánh, tìm thêm các sản phẩm cùng ngành hàng (Category) có cùng kích cỡ/dung tích
            if (results.Count < 3 && targetVariant.Product != null)
            {
                int catId = targetVariant.Product.CategoryId;
                var similarVariants = await _context.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => v.Product.CategoryId == catId && v.Id != variantId && (v.Volume == targetVariant.Volume || v.ColorName == targetVariant.ColorName))
                    .Take(5)
                    .ToListAsync();

                var similarVariantIds = similarVariants.Select(v => v.Id).ToList();

                var similarReceiptItems = await _context.StockReceiptItems
                    .Include(i => i.StockReceipt)
                    .Include(i => i.Variant)
                        .ThenInclude(v => v.Product)
                    .Where(i => similarVariantIds.Contains(i.VariantId) && !string.IsNullOrEmpty(i.StockReceipt.Supplier))
                    .OrderByDescending(i => i.StockReceipt.ImportDate)
                    .Take(5)
                    .ToListAsync();

                foreach (var item in similarReceiptItems)
                {
                    results.Add(new PriceComparisonItemViewModel
                    {
                        SupplierName = item.StockReceipt?.Supplier ?? "N/A",
                        ProductName = item.Variant?.Product?.Name,
                        SKU = item.Variant?.SKU,
                        Volume = item.Variant?.Volume,
                        ColorName = item.Variant?.ColorName,
                        ImportPrice = item.ImportPrice,
                        ImportDate = item.StockReceipt?.ImportDate,
                        SourceType = $"Sản phẩm tương đương (cùng loại/kích thước)"
                    });
                }
            }

            return results;
        }
    }
}
