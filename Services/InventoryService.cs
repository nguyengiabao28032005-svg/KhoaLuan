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
        private readonly ReceiptSettings _receiptSettings;

        public InventoryService(ApplicationDbContext context, Microsoft.Extensions.Options.IOptions<ReceiptSettings>? receiptSettings = null)
        {
            _context = context;
            _receiptSettings = receiptSettings?.Value ?? new ReceiptSettings();
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

            // Kiểm tra từng sản phẩm theo số dòng
            for (int i = 0; i < model.Items.Count; i++)
            {
                var item = model.Items[i];
                if (item.Quantity <= 0)
                {
                    return (false, $"Dòng {i + 1}: Số lượng nhập phải lớn hơn 0.", null);
                }
                if (item.ImportPrice <= 0)
                {
                    return (false, $"Dòng {i + 1}: Đơn giá nhập phải lớn hơn 0 VNĐ.", null);
                }
            }

            // ===== KIỂM TRA NGÀY NHẬP HÀNG (YÊU CẦU ĐIỀU CHỈNH GIAI ĐOẠN 1) =====
            var maxDays = _receiptSettings.MaxPastDaysAllowed > 0 ? _receiptSettings.MaxPastDaysAllowed : 30;
            var now = DateTime.Now;
            if (model.ImportDate > now)
            {
                return (false, $"Lỗi nghiệp vụ: Ngày nhập hàng ({model.ImportDate:dd/MM/yyyy HH:mm}) không được là ngày trong tương lai so với giờ hệ thống ({now:dd/MM/yyyy HH:mm}).", null);
            }
            if (model.ImportDate < now.AddDays(-maxDays))
            {
                return (false, $"Lỗi nghiệp vụ: Ngày nhập hàng ({model.ImportDate:dd/MM/yyyy}) không được lùi quá {maxDays} ngày so với hiện tại.", null);
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

            if (supplier == null)
            {
                return (false, "Vui lòng chọn nhà cung cấp hợp lệ cho phiếu nhập hàng.", null);
            }

            string supplierCountry = supplier.Country?.Trim() ?? "Việt Nam";
            bool isSupplierDomestic = supplierCountry.Equals("Việt Nam", StringComparison.OrdinalIgnoreCase);

            if (normalizedReceiptType == "Import")
            {
                if (isSupplierDomestic)
                {
                    return (false, $"Lỗi nghiệp vụ: Phiếu nhập khẩu quốc tế bắt buộc nhà cung cấp phải thuộc nước ngoài (không được là Việt Nam). Nhà cung cấp '{supplier.Name}' là đối tác tại Việt Nam.", null);
                }

                // Nếu không truyền xuất xứ (null hoặc rỗng), tự động lấy theo quốc gia của NCC
                if (string.IsNullOrWhiteSpace(model.OriginCountry))
                {
                    model.OriginCountry = supplierCountry;
                }
                else if (model.OriginCountry.Trim().Equals("Việt Nam", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Lỗi nghiệp vụ: Hàng nhập khẩu quốc tế không thể có xuất xứ là 'Việt Nam'. Xuất xứ phải là nước ngoài.", null);
                }
            }
            else // Domestic
            {
                if (!isSupplierDomestic)
                {
                    return (false, $"Lỗi nghiệp vụ: Phiếu nhập trong nước chỉ áp dụng cho nhà cung cấp tại Việt Nam. Nhà cung cấp '{supplier.Name}' là đối tác quốc tế ({supplierCountry}), vui lòng chuyển hình thức sang 'Nhập khẩu quốc tế' để qua cảng thông quan.", null);
                }
                model.OriginCountry = "Việt Nam";
            }

            string finalSupplierName = supplier.Name;
            string originCountry = normalizedReceiptType == "Import" ? model.OriginCountry! : "Việt Nam";

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

                bool isDirectReceive = string.Equals(model.ReceiptStage, "Received", StringComparison.OrdinalIgnoreCase);
                string receiptStage = isDirectReceive ? "Received" : "PendingApproval";
                string codePrefix = isDirectReceive ? "PN" : "PO"; // PO = Purchase Order (Đặt hàng NCC), PN = Phiếu nhập

                var receipt = new StockReceipt
                {
                    ReceiptCode = receiptCode.Replace("PN-", $"{codePrefix}-"),
                    VariantId = firstItem.VariantId,
                    StoreId = store.Id,
                    ImportPortId = model.ImportPortId,
                    CustomsDeclarationNumber = model.CustomsDeclarationNumber?.Trim(),
                    ReceiptType = normalizedReceiptType,
                    ReceiptStage = receiptStage,
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
                    DepositAmount = model.DepositAmount,
                    DepositTransferDate = model.DepositTransferDate,
                    DepositReceivedDate = model.DepositReceivedDate,
                    PaymentMethod = model.PaymentMethod,
                    ImportDate = actualImportDate,
                    CreatedByUserId = userId,
                    CreatedByName = !string.IsNullOrWhiteSpace(userName) ? userName : "Nhân viên kho",
                    Note = model.Note?.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.StockReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                // Lưu từng dòng chi tiết sản phẩm và cập nhật tồn kho (nếu nhập trực tiếp)
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
                        ActualQuantity = isDirectReceive ? item.Quantity : 0,
                        CancelledQuantity = 0,
                        ReceivedDate = isDirectReceive ? actualImportDate : null,
                        ImportPrice = item.ImportPrice,
                        TotalPrice = lineTotal,
                        OldRetailPrice = oldPrice,
                        NewRetailPrice = item.NewRetailPrice,
                        CreatedAt = DateTime.Now
                    };
                    _context.StockReceiptItems.Add(receiptItem);

                    // CHỈ CẬP NHẬT TỒN KHO NẾU ĐÂY LÀ NHẬP KHO TRỰC TIẾP
                    // Nếu là Phiếu đặt hàng (PendingApproval), chưa lưu vào kho cho đến khi hàng về đối chiếu
                    if (isDirectReceive)
                    {
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
                            Note = $"{normalizedReceiptType} #{receipt.ReceiptCode} tại {store.Name}: +{item.Quantity} {variant.Product?.Name} ({variant.SKU}) bởi {receipt.CreatedByName}{customsLog} [Giá nhập: {item.ImportPrice:N0}đ{priceChangeLog}] (NCC: {finalSupplierName})",
                            CreatedAt = DateTime.Now
                        });
                    }

                    totalQuantity += item.Quantity;
                    itemLogSummaries.Add($"{variant.Product?.Name}: {item.Quantity} sp");
                }

                // Nếu chọn nhà cung cấp và biến thể chưa có trong bảng Cung ứng -> Tự động thêm vào bảng Cung ứng
                if (supplier != null)
                {
                    foreach (var item in model.Items)
                    {
                        var existingSp = await _context.SupplierProducts.FirstOrDefaultAsync(sp => sp.SupplierId == supplier.Id && sp.VariantId == item.VariantId);
                        if (existingSp == null)
                        {
                            _context.SupplierProducts.Add(new SupplierProduct
                            {
                                SupplierId = supplier.Id,
                                VariantId = item.VariantId,
                                SuppliedPrice = item.ImportPrice,
                                Notes = $"Tự động ghi nhận liên kết cung ứng từ chứng từ {receipt.ReceiptCode}",
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string receiptTypeLabel = normalizedReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Nhập trong nước";
                string actionSuccessMsg = isDirectReceive
                    ? $"Tạo thành công phiếu nhập {receipt.ReceiptCode} ({receiptTypeLabel}) gồm {model.Items.Count} mặt hàng (tổng {totalQuantity} sản phẩm) vào kho \"{store.Name}\"."
                    : $"Lập thành công đơn đặt hàng với nhà cung cấp {receipt.ReceiptCode} ({receiptTypeLabel}) gồm {model.Items.Count} mặt hàng (tổng {totalQuantity} sản phẩm). Đang chờ Quản lý duyệt trước khi gửi NCC.";

                return (true, actionSuccessMsg, receipt);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi lưu chứng từ: " + (ex.InnerException?.Message ?? ex.Message), null);
            }
        }

        /// <summary>
        /// Quản lý duyệt phiếu đặt hàng trước khi gửi nhà cung cấp (Yêu cầu 2)
        /// </summary>
        public async Task<(bool Success, string Message)> ApprovePurchaseOrderAsync(
            int receiptId,
            int? userId,
            string? userName,
            string? note)
        {
            var receipt = await _context.StockReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null) return (false, "Không tìm thấy phiếu đặt hàng.");

            if (receipt.ReceiptStage != "PendingApproval")
            {
                return (false, $"Phiếu {receipt.ReceiptCode} hiện ở trạng thái '{receipt.ReceiptStage}', không thể duyệt lại.");
            }

            receipt.ReceiptStage = "Approved";
            receipt.ApprovedByUserId = userId;
            receipt.ApprovedByName = !string.IsNullOrWhiteSpace(userName) ? userName : "Quản lý kho";
            receipt.ApprovedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(note))
            {
                receipt.Note = string.IsNullOrWhiteSpace(receipt.Note) ? note : $"{receipt.Note} | Quản lý duyệt: {note}";
            }

            await _context.SaveChangesAsync();
            return (true, $"Quản lý {receipt.ApprovedByName} đã duyệt phiếu đặt hàng {receipt.ReceiptCode}. Sẵn sàng gửi đến nhà cung cấp.");
        }

        /// <summary>
        /// Gửi phiếu đặt hàng sang nhà cung cấp kèm ghi nhận đặt cọc (Yêu cầu 2)
        /// </summary>
        public async Task<(bool Success, string Message)> SendToSupplierAsync(
            int receiptId,
            decimal depositAmount,
            DateTime? depositTransferDate,
            DateTime? depositReceivedDate,
            string? paymentMethod,
            string? note)
        {
            var receipt = await _context.StockReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null) return (false, "Không tìm thấy phiếu đặt hàng.");

            if (receipt.ReceiptStage != "Approved")
            {
                return (false, $"Phiếu {receipt.ReceiptCode} chưa được quản lý duyệt (trạng thái: {receipt.ReceiptStage}), không được phép gửi sang NCC.");
            }

            receipt.ReceiptStage = "Ordered";
            if (depositAmount > 0)
            {
                receipt.DepositAmount = depositAmount;
                receipt.DepositTransferDate = depositTransferDate ?? DateTime.Now;
                receipt.DepositReceivedDate = depositReceivedDate ?? DateTime.Now;
                receipt.PaymentMethod = paymentMethod ?? "Chuyển khoản ngân hàng";
                receipt.PaidAmount = Math.Min(receipt.TotalAmount, receipt.DepositAmount);
                receipt.DebtAmount = Math.Max(0, receipt.TotalAmount - receipt.PaidAmount);
                receipt.PaymentStatus = (receipt.DebtAmount <= 0) ? "Paid" : "Partial";
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                receipt.Note = string.IsNullOrWhiteSpace(receipt.Note) ? note : $"{receipt.Note} | Gửi NCC: {note}";
            }

            await _context.SaveChangesAsync();
            return (true, $"Đã gửi phiếu đặt hàng {receipt.ReceiptCode} đến nhà cung cấp {receipt.Supplier}. Trạng thái: Chờ nhà cung cấp giao hàng.");
        }

        /// <summary>
        /// Đối chiếu phiếu nhập với phiếu đặt hàng khi nhà cung cấp giao hàng (Yêu cầu 2)
        /// - Nhà cung cấp giao hàng kèm phiếu xuất kho
        /// - Đối chiếu số lượng đặt vs. thực nhận (ví dụ đặt 10 nhận 5)
        /// - Phần không nhận thì hủy, và khi thống kê chỉ tính phần đã nhập
        /// - Phiếu thanh toán chỉ tính theo số thực nhập
        /// - Khớp rồi mới cập nhật tồn kho
        /// </summary>
        public async Task<(bool Success, string Message, StockReceipt? Receipt)> ReconcileAndReceiveReceiptAsync(
            ReconcileStockReceiptViewModel model,
            int? userId,
            string? userName)
        {
            if (model == null) return (false, "Dữ liệu đối chiếu không hợp lệ.", null);

            var receipt = await _context.StockReceipts
                .Include(r => r.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v!.Product)
                .Include(r => r.Store)
                .FirstOrDefaultAsync(r => r.Id == model.ReceiptId);

            if (receipt == null) return (false, "Không tìm thấy phiếu đặt hàng cần đối chiếu.", null);

            if (receipt.ReceiptStage == "Received")
            {
                return (false, $"Phiếu {receipt.ReceiptCode} đã được đối chiếu và nhập kho trước đó.", null);
            }

            if (string.IsNullOrWhiteSpace(model.SupplierDeliveryNoteCode))
            {
                return (false, "Vui lòng nhập Mã số phiếu xuất kho của Nhà cung cấp kèm theo hàng để đối chiếu chứng từ.", null);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int totalActualQuantity = 0;
                int totalCancelledQuantity = 0;
                decimal totalActualAmount = 0;
                int storeId = receipt.StoreId;

                foreach (var line in receipt.Items)
                {
                    var inputItem = model.Items.FirstOrDefault(m => m.VariantId == line.VariantId);
                    int actualQty = inputItem != null ? inputItem.ActualQuantity : line.Quantity;
                    DateTime recDate = inputItem?.ReceivedDate ?? DateTime.Now;

                    if (actualQty < 0) actualQty = 0;
                    if (actualQty > line.Quantity)
                    {
                        // Cho phép nhận nhiều hơn nếu NCC giao bù hoặc giao khuyến mãi
                    }

                    int cancelledQty = Math.Max(0, line.Quantity - actualQty);

                    line.ActualQuantity = actualQty;
                    line.CancelledQuantity = cancelledQty;
                    line.ReceivedDate = recDate;
                    line.TotalPrice = actualQty * line.ImportPrice; // Tính lại thành tiền theo số thực nhận

                    totalActualQuantity += actualQty;
                    totalCancelledQuantity += cancelledQty;
                    totalActualAmount += line.TotalPrice;

                    // Chỉ tăng tồn kho cho số lượng thực nhận
                    if (actualQty > 0)
                    {
                        var inventory = await _context.Inventories
                            .FirstOrDefaultAsync(i => i.VariantId == line.VariantId && i.StoreId == storeId);

                        if (inventory == null)
                        {
                            inventory = new Inventory
                            {
                                StoreId = storeId,
                                VariantId = line.VariantId,
                                StockQuantity = actualQty,
                                ReservedQuantity = 0
                            };
                            _context.Inventories.Add(inventory);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            inventory.StockQuantity += actualQty;
                        }

                        // Cập nhật giá vốn tham chiếu
                        if (line.Variant != null)
                        {
                            line.Variant.OriginalPrice = line.ImportPrice;
                            if (inputItem?.NewRetailPrice.HasValue == true && inputItem.NewRetailPrice.Value > 0)
                            {
                                line.Variant.RetailPrice = inputItem.NewRetailPrice.Value;
                                line.NewRetailPrice = inputItem.NewRetailPrice.Value;
                            }
                        }

                        // Ghi nhật ký biến động kho
                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            InventoryId = inventory.Id,
                            Inventory = inventory,
                            ChangeType = "Import",
                            QuantityChanged = actualQty,
                            Note = $"Thực nhập theo phiếu #{receipt.ReceiptCode} (PXK NCC: {model.SupplierDeliveryNoteCode}): Đặt {line.Quantity}, thực nhận {actualQty}{(cancelledQty > 0 ? $", hủy {cancelledQty}" : "")} bởi {userName ?? "Thủ kho"}",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                // Cập nhật phiếu đặt hàng thành phiếu nhập kho hoàn chỉnh
                receipt.ReceiptStage = "Received";
                receipt.SupplierDeliveryNoteCode = model.SupplierDeliveryNoteCode.Trim();
                if (!string.IsNullOrWhiteSpace(model.InvoiceNumber)) receipt.InvoiceNumber = model.InvoiceNumber.Trim();
                receipt.ImportDate = DateTime.Now;

                // TỔNG TIỀN THANH TOÁN CHỈ TÍNH THEO SỐ THỰC NHẬP (YÊU CẦU 2)
                receipt.TotalAmount = totalActualAmount;
                receipt.Quantity = totalActualQuantity; // Lưu tổng thực nhập

                // Tính toán công nợ và thanh toán theo số thực nhập
                decimal totalPaidSoFar = receipt.DepositAmount;
                if (totalPaidSoFar >= receipt.TotalAmount)
                {
                    receipt.PaidAmount = receipt.TotalAmount;
                    receipt.DebtAmount = 0;
                    receipt.PaymentStatus = "Paid";
                }
                else
                {
                    receipt.PaidAmount = totalPaidSoFar;
                    receipt.DebtAmount = receipt.TotalAmount - totalPaidSoFar;
                    receipt.PaymentStatus = totalPaidSoFar > 0 ? "Partial" : "Unpaid";
                }

                receipt.PaymentTransferDate = model.PaymentTransferDate;
                receipt.PaymentReceivedDate = model.PaymentReceivedDate;
                if (!string.IsNullOrWhiteSpace(model.PaymentMethod)) receipt.PaymentMethod = model.PaymentMethod;

                string cancelSummary = totalCancelledQuantity > 0 ? $" (Hủy {totalCancelledQuantity} sản phẩm không nhận)" : "";
                receipt.Note = string.IsNullOrWhiteSpace(model.Note) 
                    ? $"{receipt.Note} | Đối chiếu khớp PXK {model.SupplierDeliveryNoteCode}{cancelSummary}" 
                    : $"{receipt.Note} | Đối chiếu PXK {model.SupplierDeliveryNoteCode}: {model.Note}{cancelSummary}";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Đối chiếu thành công phiếu {receipt.ReceiptCode} với Phiếu xuất kho NCC #{model.SupplierDeliveryNoteCode}. Đã nhập {totalActualQuantity} sản phẩm vào kho, hủy {totalCancelledQuantity} sản phẩm. Tổng tiền thanh toán: {receipt.TotalAmount:N0} đ.", receipt);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi đối chiếu và lưu kho: " + (ex.InnerException?.Message ?? ex.Message), null);
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
                    SupplierId = item.StockReceipt?.SupplierId,
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

            // 2. Lấy giá từ bảng trung gian Cung ứng (SupplierProduct) của biến thể này
            var suppliedPrices = await _context.SupplierProducts
                .Include(sp => sp.Supplier)
                .Include(sp => sp.Variant)
                    .ThenInclude(v => v.Product)
                .Where(sp => sp.VariantId == targetVariant.Id && sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0)
                .ToListAsync();

            foreach (var sp in suppliedPrices)
            {
                // Tránh trùng nếu đã có trong lịch sử gần nhất
                if (!results.Any(r => r.SupplierName.Equals(sp.Supplier?.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new PriceComparisonItemViewModel
                    {
                        SupplierId = sp.SupplierId,
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
                        SupplierId = item.StockReceipt?.SupplierId,
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

            // 4. Tính toán giá thấp nhất và độ chênh lệch để tối ưu hóa quá trình nhập hàng
            if (results.Any())
            {
                decimal minPrice = results.Min(r => r.ImportPrice);
                foreach (var r in results)
                {
                    r.IsLowestPrice = (r.ImportPrice == minPrice);
                    r.DifferenceFromLowest = r.ImportPrice - minPrice;
                }
            }

            return results;
        }

        public async Task<(decimal? LastPrice, string? SupplierName, DateTime? ImportDate, string? ReceiptCode)> GetLastImportPriceAsync(int variantId, int? supplierId)
        {
            var query = _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.VariantId == variantId && i.StockReceipt != null);

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var itemWithSup = await query
                    .Where(i => i.StockReceipt!.SupplierId == supplierId.Value)
                    .OrderByDescending(i => i.StockReceipt!.ImportDate)
                    .ThenByDescending(i => i.Id)
                    .FirstOrDefaultAsync();

                if (itemWithSup != null)
                {
                    return (itemWithSup.ImportPrice, itemWithSup.StockReceipt?.Supplier, itemWithSup.StockReceipt?.ImportDate, itemWithSup.StockReceipt?.ReceiptCode);
                }
            }

            // Theo yêu cầu điều chỉnh Giai đoạn 1 (Mục 5):
            // "Đơn giá nhập lấy từ lần nhập trước của chính nhà cung cấp đó; chưa từng nhập thì hiện 'Chưa nhập' và bắt buộc nhập tay."
            // Không tự động fallback sang NCC khác hoặc giá niêm yết
            return (null, null, null, null);
        }

        /// <summary>
        /// So sánh giá giữa các nhà cung cấp cho một biến thể:
        /// 1. Tách rõ: "Giá chào hiện tại" (từ bảng cung ứng kèm ngày cập nhật) và "Giá nhập lần gần nhất" (từ phiếu nhập kèm ngày và mã phiếu).
        /// 2. "Giá tốt nhất" và cột "So sánh với giá tốt nhất" CHỈ tính trên Giá chào hiện tại, không trộn với giá nhập cũ.
        /// 3. Hiển thị thêm chênh lệch giữa giá chào hiện tại và giá nhập lần trước của CÙNG nhà cung cấp.
        /// </summary>
        public async Task<List<VariantSupplierPriceComparisonViewModel>> GetSupplierPriceComparisonsAsync(int variantId)
        {
            var spList = await _context.SupplierProducts
                .Include(sp => sp.Supplier)
                .Where(sp => sp.VariantId == variantId && sp.Supplier != null && sp.Supplier.IsActive)
                .ToListAsync();

            if (!spList.Any()) return new List<VariantSupplierPriceComparisonViewModel>();

            var pastReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.VariantId == variantId && i.StockReceipt != null && i.StockReceipt.SupplierId.HasValue)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            var pastBySupplier = pastReceiptItems
                .GroupBy(i => i.StockReceipt!.SupplierId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // 1. Thu thập dữ liệu thô
            var rawList = spList.Select(sp =>
            {
                bool hasPast = pastBySupplier.TryGetValue(sp.SupplierId, out var pastItem);
                decimal? pastPrice = hasPast ? pastItem!.ImportPrice : null;
                DateTime? pastDate = hasPast ? pastItem!.StockReceipt?.ImportDate : null;
                string? pastCode = hasPast ? pastItem!.StockReceipt?.ReceiptCode : null;

                decimal suppliedPrice = sp.SuppliedPrice ?? 0;
                DateTime? updatedDate = sp.UpdatedAt ?? sp.CreatedAt;

                // Chênh lệch giữa Giá chào hiện tại và Giá nhập lần trước của CÙNG nhà cung cấp
                decimal? diffFromPast = null;
                double? diffFromPastPercent = null;
                if (suppliedPrice > 0 && hasPast && pastPrice.HasValue && pastPrice.Value > 0)
                {
                    diffFromPast = suppliedPrice - pastPrice.Value;
                    diffFromPastPercent = Math.Round((double)((diffFromPast.Value / pastPrice.Value) * 100), 1);
                }

                return new
                {
                    sp,
                    suppliedPrice,
                    updatedDate,
                    hasPast,
                    pastPrice,
                    pastDate,
                    pastCode,
                    diffFromPast,
                    diffFromPastPercent
                };
            }).ToList();

            // 2. Tính giá tốt nhất: CHỈ TÍNH TRÊN GIÁ CHÀO HIỆN TẠI (suppliedPrice > 0)
            decimal minSuppliedPrice = rawList.Where(x => x.suppliedPrice > 0)
                .Select(x => x.suppliedPrice)
                .DefaultIfEmpty(0)
                .Min();

            // 3. Xây dựng kết quả
            var result = rawList.Select(x =>
            {
                bool isLowest = (minSuppliedPrice > 0 && x.suppliedPrice == minSuppliedPrice);
                decimal diffFromLowest = (minSuppliedPrice > 0 && x.suppliedPrice > 0) ? (x.suppliedPrice - minSuppliedPrice) : 0;
                double diffPercent = (minSuppliedPrice > 0 && x.suppliedPrice > minSuppliedPrice)
                    ? Math.Round((double)(((x.suppliedPrice - minSuppliedPrice) / minSuppliedPrice) * 100), 1)
                    : 0;

                return new VariantSupplierPriceComparisonViewModel
                {
                    SupplierId = x.sp.SupplierId,
                    SupplierName = x.sp.Supplier?.Name ?? "N/A",
                    Country = x.sp.Supplier?.Country ?? "Việt Nam",
                    Address = x.sp.Supplier?.Address ?? "—",
                    Phone = x.sp.Supplier?.Phone ?? "—",
                    SuppliedPrice = x.suppliedPrice,
                    SuppliedPriceUpdatedAt = x.updatedDate,
                    SuppliedPriceDateFormatted = x.updatedDate?.ToString("dd/MM/yyyy"),
                    HasPastImport = x.hasPast,
                    LastImportPrice = x.pastPrice,
                    LastImportDate = x.pastDate,
                    LastImportDateFormatted = x.pastDate?.ToString("dd/MM/yyyy"),
                    LastReceiptCode = x.pastCode,
                    IsLowestPrice = isLowest,
                    DifferenceFromLowest = diffFromLowest,
                    DifferencePercent = diffPercent,
                    DiffFromPastImport = x.diffFromPast,
                    DiffFromPastImportPercent = x.diffFromPastPercent,
                    Notes = x.sp.Notes
                };
            }).OrderBy(x => x.SuppliedPrice > 0 ? x.SuppliedPrice : decimal.MaxValue).ToList();

            return result;
        }
    }
}
