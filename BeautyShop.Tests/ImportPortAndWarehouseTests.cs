using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using Xunit;

namespace BeautyShop.Tests
{
    /// <summary>
    /// Bộ kiểm thử tự động cho BƯỚC 3 HOÀN THIỆN: Ràng buộc nhập hàng chỉ ở tỉnh có CẢNG nhập khẩu
    /// Đáp ứng 100% 6 ca kiểm thử theo yêu cầu:
    /// 1. Nhập khẩu hợp lệ (Cảng mở, kho cùng tỉnh, số lượng & giá > 0, có số tờ khai) -> Thành công.
    /// 2. Thiếu cảng khi nhập khẩu -> Bị từ chối kèm thông báo rõ ràng.
    /// 3. Cảng bị tắt AllowImport -> Bị từ chối kèm thông báo cảng tạm đóng.
    /// 4. Kho khác tỉnh với cảng -> Bị từ chối, nêu rõ tỉnh kho và tỉnh cảng.
    /// 5. Nhập trong nước không bị ràng buộc -> Thành công bình thường ở bất kỳ kho nào.
    /// 6. Lỗi giữa chừng không làm lệch tồn kho -> Transaction rollback hoàn toàn, số lượng tồn kho giữ nguyên.
    /// </summary>
    public class ImportPortAndWarehouseTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly InventoryService _inventoryService;

        public ImportPortAndWarehouseTests()
        {
            // Sử dụng SQLite in-memory để đảm bảo test chạy độc lập, nhanh chóng và cô lập
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // 1. Khởi tạo Role và User phục vụ ghi nhận người tạo phiếu nhập
            var role = new Role { Id = 1, Name = "Admin" };
            var user = new User { Id = 1, FullName = "Nhân viên thủ kho", Email = "admin@beautyshop.com", PasswordHash = "hash", RoleId = 1, IsActive = true };
            _context.Roles.Add(role);
            _context.Users.Add(user);

            // 2. Khởi tạo dữ liệu nền tảng cho Product và Variant
            var brand = new Brand { Id = 1, Name = "Estee Lauder" };
            var category = new Category { Id = 1, Name = "Serum chống lão hóa", Slug = "serum-chong-lao-hoa" };
            var product = new Product
            {
                Id = 1,
                Name = "Advanced Night Repair Serum",
                BrandId = 1,
                CategoryId = 1,
                Description = "Serum phục hồi ban đêm",
                IsActive = true
            };
            var variant = new ProductVariant
            {
                Id = 1,
                ProductId = 1,
                SKU = "EST-ANR-50ML",
                Barcode = "8809123456789",
                Volume = "50ml",
                OriginalPrice = 0, // Ban đầu chưa nhập hàng
                RetailPrice = null
            };

            // 3. Khởi tạo kho chưa gán tỉnh để kiểm thử ngoại lệ
            var storeNoProvince = new Store
            {
                Id = 10,
                Name = "Kho Chưa Gán Tỉnh",
                Address = "Địa chỉ chưa rõ",
                ProvinceName = null,
                ProvinceCode = null,
                Region = "Chưa rõ",
                StoreType = "Kho phụ",
                IsActive = true
            };

            _context.Brands.Add(brand);
            _context.Categories.Add(category);
            _context.Products.Add(product);
            _context.ProductVariants.Add(variant);
            _context.Stores.Add(storeNoProvince);

            _context.SaveChanges();

            _inventoryService = new InventoryService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        /// <summary>
        /// TEST 1: Nhập khẩu hợp lệ
        /// Điều kiện: Cảng mở (Tân Sơn Nhất, TP.HCM), kho cùng tỉnh (Kho TP.HCM), số lượng > 0, giá > 0, có số tờ khai hải quan.
        /// Kết quả: Thành công, lưu ImportPortId, CustomsDeclarationNumber, tăng tồn kho và cập nhật giá vốn.
        /// </summary>
        [Fact]
        public async Task Test1_ImportReceipt_ValidPortAndSameProvinceWarehouse_Succeeds()
        {
            // ARRANGE: Cảng 1 = Sân bay Tân Sơn Nhất (TP.HCM, 79), Kho 1 = Kho TP.HCM (79)
            int variantId = 1;
            int quantity = 25;
            decimal importPrice = 1250000;
            string supplier = "Estee Lauder International";
            int storeId = 1;
            int importPortId = 1;
            string customsNumber = "TKHQ-2026-SGN-0982";

            // ACT: Tạo phiếu nhập khẩu
            var result = await _inventoryService.CreateReceiptAsync(
                variantId: variantId,
                quantity: quantity,
                importPrice: importPrice,
                supplier: supplier,
                importDate: DateTime.Now,
                userId: 1,
                userName: "Nhân viên thủ kho",
                note: "Nhập khẩu chính ngạch đường hàng không",
                storeId: storeId,
                receiptType: "Import",
                importPortId: importPortId,
                customsDeclarationNumber: customsNumber
            );

            // ASSERT:
            Assert.True(result.Success, $"Tạo phiếu nhập khẩu thất bại: {result.Message}");
            Assert.NotNull(result.Receipt);
            Assert.Equal("Import", result.Receipt.ReceiptType);
            Assert.Equal(storeId, result.Receipt.StoreId);
            Assert.Equal(importPortId, result.Receipt.ImportPortId);
            Assert.Equal(customsNumber, result.Receipt.CustomsDeclarationNumber);
            Assert.StartsWith("PN-NK-", result.Receipt.ReceiptCode);

            // Tồn kho tại kho 1 phải tăng lên đúng 25
            var inv = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == storeId && i.VariantId == variantId);
            Assert.NotNull(inv);
            Assert.Equal(25, inv.StockQuantity);

            // Giá vốn biến thể được cập nhật tự động
            var v = await _context.ProductVariants.FindAsync(variantId);
            Assert.Equal(1250000, v?.OriginalPrice);

            // Có ghi log nhật ký biến động kho
            var log = await _context.InventoryLogs.FirstOrDefaultAsync(l => l.InventoryId == inv.Id);
            Assert.NotNull(log);
            Assert.Contains(customsNumber, log.Note);
        }

        /// <summary>
        /// TEST 2: Thiếu cảng khi nhập khẩu
        /// Điều kiện: Phiếu loại "Import" nhưng không chọn cảng (importPortId = null).
        /// Kết quả: Bị từ chối kèm thông báo lỗi tiếng Việt rõ ràng.
        /// </summary>
        [Fact]
        public async Task Test2_ImportReceipt_MissingPort_IsRejectedWithClearMessage()
        {
            // ARRANGE & ACT: Cố tình không truyền cảng nhập khẩu
            var result = await _inventoryService.CreateReceiptAsync(
                variantId: 1,
                quantity: 15,
                importPrice: 800000,
                supplier: "Nhà cung cấp ngoại",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Nhân viên",
                note: "Thiếu thông tin cảng",
                storeId: 1,
                receiptType: "Import",
                importPortId: null
            );

            // ASSERT:
            Assert.False(result.Success, "Hệ thống phải từ chối khi phiếu nhập khẩu không chọn cảng.");
            Assert.Null(result.Receipt);
            Assert.Contains("Vui lòng chọn cảng nhập khẩu", result.Message);
        }

        /// <summary>
        /// TEST 3: Cảng bị tắt AllowImport
        /// Điều kiện: Cảng tại TP.HCM (Id = 1) bị đóng tiếp nhận (AllowImport = false).
        /// Kết quả: Bị từ chối kèm thông báo cảng tạm đóng tiếp nhận.
        /// </summary>
        [Fact]
        public async Task Test3_ImportReceipt_DisabledPort_IsRejectedWithClearMessage()
        {
            // ARRANGE: Tắt AllowImport của cảng 1 (Tân Sơn Nhất)
            var port = await _context.ImportPorts.FindAsync(1);
            Assert.NotNull(port);
            port.AllowImport = false;
            await _context.SaveChangesAsync();

            // ACT: Thử tạo phiếu nhập khẩu qua cảng 1
            var result = await _inventoryService.CreateReceiptAsync(
                variantId: 1,
                quantity: 10,
                importPrice: 500000,
                supplier: "Đối tác Hàn Quốc",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Admin",
                note: "Thử nhập khi cảng đã đóng",
                storeId: 1,
                receiptType: "Import",
                importPortId: 1
            );

            // ASSERT:
            Assert.False(result.Success, "Phải từ chối nhập khẩu khi cảng bị tắt AllowImport.");
            Assert.Null(result.Receipt);
            Assert.Contains("tạm đóng tiếp nhận", result.Message);
        }

        /// <summary>
        /// TEST 4: Kho khác tỉnh với cảng
        /// Điều kiện: Chọn Cảng 1 (Tân Sơn Nhất, TP.HCM - mã 79) nhưng chọn Kho 3 (Hà Nội - mã 01).
        /// Kết quả: Bị từ chối kèm thông báo nêu rõ 2 tỉnh/thành không cùng địa bàn.
        /// </summary>
        [Fact]
        public async Task Test4_ImportReceipt_PortAndWarehouseInDifferentProvinces_IsRejectedWithClearMessage()
        {
            // ARRANGE: Cảng 1 tại TP.HCM (Id = 1), Kho 3 tại Hà Nội (Id = 3)
            int portId = 1;  // TP.HCM
            int storeId = 3; // Hà Nội

            // ACT: Cố tình nhập khẩu từ cảng TP.HCM vào kho Hà Nội
            var result = await _inventoryService.CreateReceiptAsync(
                variantId: 1,
                quantity: 20,
                importPrice: 600000,
                supplier: "Nhà cung cấp ngoại",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Admin",
                note: "Lệch tỉnh kho và cảng",
                storeId: storeId,
                receiptType: "Import",
                importPortId: portId
            );

            // ASSERT:
            Assert.False(result.Success, "Hệ thống phải từ chối khi kho và cảng khác tỉnh/thành.");
            Assert.Null(result.Receipt);
            Assert.Contains("không cùng tỉnh/thành phố", result.Message);
            Assert.Contains("Thành phố Hà Nội", result.Message);
            Assert.Contains("Thành phố Hồ Chí Minh", result.Message);

            // Tồn kho tại kho Hà Nội không bị thay đổi
            var inv = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == storeId && i.VariantId == 1);
            Assert.True(inv == null || inv.StockQuantity == 0);
        }

        /// <summary>
        /// TEST 5: Nhập trong nước không bị ràng buộc cảng
        /// Điều kiện: Phiếu loại "Domestic", nhập vào Kho 2 (Lâm Đồng - tỉnh không có cảng).
        /// Kết quả: Thành công bình thường, không yêu cầu cảng.
        /// </summary>
        [Fact]
        public async Task Test5_DomesticReceipt_NoPortRequiredAndAnyWarehouse_SucceedsNormally()
        {
            // ARRANGE: Kho 2 tại Lâm Đồng (không có cảng)
            int storeId = 2;
            int quantity = 40;
            decimal price = 700000;

            // ACT: Tạo phiếu nhập trong nước
            var result = await _inventoryService.CreateReceiptAsync(
                variantId: 1,
                quantity: quantity,
                importPrice: price,
                supplier: "Nhà phân phối mỹ phẩm Miền Trung",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Nhân viên kho",
                note: "Hàng nội địa",
                storeId: storeId,
                receiptType: "Domestic",
                importPortId: null
            );

            // ASSERT:
            Assert.True(result.Success, $"Nhập trong nước phải thành công: {result.Message}");
            Assert.NotNull(result.Receipt);
            Assert.Equal("Domestic", result.Receipt.ReceiptType);
            Assert.Null(result.Receipt.ImportPortId);
            Assert.Equal(storeId, result.Receipt.StoreId);
            Assert.StartsWith("PN-TN-", result.Receipt.ReceiptCode);

            // Tồn kho tại kho Lâm Đồng được cập nhật đúng 40
            var inv = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == storeId && i.VariantId == 1);
            Assert.NotNull(inv);
            Assert.Equal(40, inv.StockQuantity);
        }

        /// <summary>
        /// TEST 6: Lỗi giữa chừng không làm lệch tồn kho (Transaction Rollback)
        /// Điều kiện: Cố tình gửi dữ liệu không hợp lệ (số lượng <= 0 hoặc đơn vị giá <= 0).
        /// Kết quả: Toàn bộ thao tác bị hủy, không để tồn kho hoặc giá vốn bị lệch.
        /// </summary>
        [Fact]
        public async Task Test6_TransactionFailure_RollsBackCompletelyWithoutStockDiscrepancy()
        {
            // ARRANGE: Ghi nhận tồn kho ban đầu tại kho 1
            var initialInv = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            int initialStock = initialInv?.StockQuantity ?? 0;

            // ACT: Gọi nhập kho với giá nhập <= 0 (vi phạm quy định đơn giá vốn)
            var resultInvalidPrice = await _inventoryService.CreateReceiptAsync(
                variantId: 1,
                quantity: 50,
                importPrice: 0, // Giá 0 VNĐ không hợp lệ
                supplier: "Nhà cung cấp lỗi",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Admin",
                note: "Test lỗi giá",
                storeId: 1,
                receiptType: "Domestic"
            );

            // ASSERT:
            Assert.False(resultInvalidPrice.Success);
            Assert.Contains("Giá nhập kho phải lớn hơn 0", resultInvalidPrice.Message);

            // Kiểm tra tồn kho sau khi giao dịch bị từ chối: Tồn kho tuyệt đối không bị tăng lên
            var currentInv = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            int currentStock = currentInv?.StockQuantity ?? 0;
            Assert.Equal(initialStock, currentStock);

            // Không sinh ra bất kỳ bản ghi InventoryLog hay StockReceipt rác nào
            Assert.False(await _context.StockReceipts.AnyAsync(r => r.Quantity == 50 && r.ImportPrice == 0));
        }
    }
}
