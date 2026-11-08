using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Xunit;

namespace BeautyShop.Tests
{
    public class SupplierAndMultiItemReceiptTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly InventoryService _inventoryService;

        public SupplierAndMultiItemReceiptTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo Role, User, Brand, Category, Store
            var role = new Role { Id = 1, Name = "Admin" };
            var catSon = new Category { Id = 1, Name = "Son Môi", Slug = "son-moi" };
            var catPhan = new Category { Id = 2, Name = "Phấn Nền", Slug = "phan-nen" };
            var catKem = new Category { Id = 3, Name = "Kem Chống Nắng", Slug = "kem-chong-nang" };
            
            var brandLoreal = new Brand { Id = 1, Name = "L'Oréal" };
            var brandCocoon = new Brand { Id = 2, Name = "Cocoon" };

            // Kho tổng TP.HCM và Kho Tây Nam Bộ (Cần Thơ)
            var storeCentral = new Store
            {
                Id = 1,
                Name = "Kho tổng TP.HCM",
                Address = "Quận 1, TP.HCM",
                ProvinceName = "Thành phố Hồ Chí Minh",
                ProvinceCode = "79",
                Region = "Miền Nam",
                StoreType = "Kho tổng (Central)",
                IsActive = true
            };
            var storeCanTho = new Store
            {
                Id = 5,
                Name = "Kho Tây Nam Bộ (Cần Thơ)",
                Address = "KCN Trà Nóc, Bình Thủy, TP. Cần Thơ",
                ProvinceName = "Thành phố Cần Thơ",
                ProvinceCode = "92",
                Region = "Miền Nam",
                StoreType = "Kho phân phối (Retail)",
                IsActive = true
            };

            var user = new User { Id = 1, RoleId = 1, FullName = "Thủ kho trung tâm", Email = "stock@beautyshop.com", PasswordHash = "hash" };

            // Sản phẩm 1: Son môi (CategoryId = 1)
            var prod1 = new Product { Id = 1, Name = "Son L'Oréal Rouge Signature", CategoryId = 1, BrandId = 1, IsActive = true };
            var var1 = new ProductVariant { Id = 1, ProductId = 1, SKU = "LOR-LIP-01", ColorName = "Đỏ Hồng", Volume = "7ml", RetailPrice = 250000m, OriginalPrice = 160000m };

            // Sản phẩm 2: Phấn nền (CategoryId = 2)
            var prod2 = new Product { Id = 2, Name = "Phấn Nền L'Oréal Infallible", CategoryId = 2, BrandId = 1, IsActive = true };
            var var2 = new ProductVariant { Id = 2, ProductId = 2, SKU = "LOR-POW-01", ColorName = "Tự Nhiên", Volume = "9g", RetailPrice = 300000m, OriginalPrice = 200000m };

            // Sản phẩm 3: Chì kẻ mắt (CategoryId = 1)
            var prod3 = new Product { Id = 3, Name = "Chì Kẻ Mắt L'Oréal SuperLiner", CategoryId = 1, BrandId = 1, IsActive = true };
            var var3 = new ProductVariant { Id = 3, ProductId = 3, SKU = "LOR-EYE-01", ColorName = "Đen Tuyền", Volume = "1.2g", RetailPrice = 180000m, OriginalPrice = 110000m };

            _context.Roles.Add(role);
            _context.Categories.AddRange(catSon, catPhan, catKem);
            _context.Brands.AddRange(brandLoreal, brandCocoon);
            _context.Users.Add(user);
            _context.Products.AddRange(prod1, prod2, prod3);
            _context.ProductVariants.AddRange(var1, var2, var3);

            // Thêm Nhà cung cấp trong nước (Yêu cầu 2 & Nghiệp vụ nội địa)
            var supplier = new Supplier
            {
                Id = 1,
                Name = "Công ty Cổ phần Phân phối Dược Mỹ phẩm Nam Việt",
                ContactPerson = "Nguyễn Thu Hà",
                Phone = "028 3936 9142",
                Address = "Tầng 12, Tòa nhà Vincom Center, 72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                TaxCode = "0304910242",
                Country = "Việt Nam",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            // Thêm Nhà cung cấp quốc tế (Nghiệp vụ nhập khẩu)
            var supplierForeign = new Supplier
            {
                Id = 2,
                Name = "Tập đoàn Phân phối Mỹ phẩm Quốc tế Á Châu",
                ContactPerson = "Park Min Young",
                Phone = "+82 2 1234 5678",
                Address = "Gangnam-gu, Seoul, Hàn Quốc",
                TaxCode = "9988776655",
                Country = "Hàn Quốc",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Suppliers.AddRange(supplier, supplierForeign);

// ImportPorts are already seeded by HasData in ApplicationDbContext (Port 2: Cát Lái TP.HCM, Port 4: Lạch Huyện Hải Phòng)

            // Bảng Cung ứng (Yêu cầu 3): Gán sản phẩm 1, 2 cho NCC 1
            _context.SupplierProducts.AddRange(
                new SupplierProduct { Id = 1, SupplierId = 1, VariantId = 1, SuppliedPrice = 150000m, CreatedAt = DateTime.Now },
                new SupplierProduct { Id = 2, SupplierId = 1, VariantId = 2, SuppliedPrice = 190000m, CreatedAt = DateTime.Now }
            );

            _context.SaveChanges();

            _inventoryService = new InventoryService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        /// <summary>
        /// YÊU CẦU 1: Không cho phiếu nhập chỉ có một sản phẩm.
        /// Thử tạo phiếu chỉ có 1 sản phẩm -> Hệ thống phải từ chối kèm thông báo rõ ràng.
        /// </summary>
        [Fact]
        public async Task Req1_CreateMultiItemReceipt_WithOnlyOneItem_IsRejected()
        {
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 50, ImportPrice = 150000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");

            Assert.False(success);
            Assert.Null(receipt);
            Assert.Contains("2 sản phẩm", message);
        }

        /// <summary>
        /// YÊU CẦU 1: Thử tạo phiếu với 2 dòng nhưng cùng 1 biến thể -> Hệ thống cũng từ chối vì không phải nhiều mặt hàng khác nhau.
        /// </summary>
        [Fact]
        public async Task Req1_CreateMultiItemReceipt_WithDuplicateVariants_IsRejected()
        {
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 20, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 30, ImportPrice = 150000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");

            Assert.False(success);
            Assert.Contains("2 sản phẩm", message);
        }

        /// <summary>
        /// YÊU CẦU 1 & 7: Nhập cùng lúc nhiều sản phẩm (Son, Phấn, Chì kẻ...) vào kho thành công,
        /// tăng tồn kho cho từng sản phẩm và lưu chi tiết StockReceiptItems.
        /// </summary>
        [Fact]
        public async Task Req1_And_Req7_CreateMultiItemReceipt_WithMultipleItems_SucceedsAndUpdatesInventory()
        {
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1, // Nhập vào Kho tổng TP.HCM
                ReceiptType = "Domestic",
                SupplierId = 1,
                InvoiceNumber = "HD-VAT-88492",
                OriginCountry = "Pháp",
                PaymentStatus = "Paid",
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 30, ImportPrice = 155000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 25, ImportPrice = 195000m },
                    new ReceiptItemInputModel { VariantId = 3, Quantity = 40, ImportPrice = 105000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");

            Assert.True(success, message);
            Assert.NotNull(receipt);

            // Kiểm tra tổng tiền
            decimal expectedTotal = (30 * 155000m) + (25 * 195000m) + (40 * 105000m);
            Assert.Equal(expectedTotal, receipt.TotalAmount);
            Assert.Equal(expectedTotal, receipt.PaidAmount);
            Assert.Equal(0, receipt.DebtAmount);

            // Kiểm tra tồn kho của cả 3 sản phẩm đều tăng đúng số lượng
            var inv1 = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            var inv2 = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 2);
            var inv3 = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 3);

            Assert.Equal(30, inv1?.StockQuantity);
            Assert.Equal(25, inv2?.StockQuantity);
            Assert.Equal(40, inv3?.StockQuantity);

            // Kiểm tra bảng chi tiết StockReceiptItems
            var items = await _context.StockReceiptItems.Where(i => i.StockReceiptId == receipt.Id).ToListAsync();
            Assert.Equal(3, items.Count);
        }

        /// <summary>
        /// YÊU CẦU 2: Quản lý nhà cung cấp lưu thông tin, địa chỉ để phản hồi khi hàng lỗi.
        /// </summary>
        [Fact]
        public async Task Req2_SupplierManagement_StoresAddressForDefectiveGoodsFeedback()
        {
            var supplier = await _context.Suppliers.FindAsync(1);
            Assert.NotNull(supplier);
            Assert.False(string.IsNullOrWhiteSpace(supplier.Address));
            Assert.Contains("Vincom", supplier.Address);
            Assert.Equal("Việt Nam", supplier.Country);
        }

        /// <summary>
        /// YÊU CẦU 3: Bảng Cung ứng trung gian giữa Nhà cung cấp và Sản phẩm.
        /// Khi chọn nhà cung cấp 1, hệ thống tra cứu được đúng 2 sản phẩm họ cung cấp.
        /// </summary>
        [Fact]
        public async Task Req3_SupplyTable_FiltersProductsBelongingToSupplier()
        {
            var suppliedVariantIds = await _context.SupplierProducts
                .Where(sp => sp.SupplierId == 1)
                .Select(sp => sp.VariantId)
                .ToListAsync();

            Assert.Contains(1, suppliedVariantIds); // Son
            Assert.Contains(2, suppliedVariantIds); // Phấn
            Assert.DoesNotContain(3, suppliedVariantIds); // Chì kẻ mắt chưa gán
        }

        /// <summary>
        /// YÊU CẦU 5: Quản lý thanh toán và công nợ (Đơn chưa trả -> ghi nhận công nợ và cập nhật thanh toán)
        /// </summary>
        [Fact]
        public async Task Req5_PaymentAndDebtManagement_RecordsDebtAndUpdatesPayment()
        {
            // Tạo đơn chưa thanh toán (Unpaid)
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                PaymentStatus = "Unpaid",
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 100000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 100000m }
                }
            };

            var (success, _, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");
            Assert.True(success);
            Assert.NotNull(receipt);
            Assert.Equal("Unpaid", receipt.PaymentStatus);
            Assert.Equal(2000000m, receipt.DebtAmount);
            Assert.Equal(0m, receipt.PaidAmount);

            // Cập nhật thanh toán một phần: trả 1.500.000đ
            var (paySuccess, _) = await _inventoryService.UpdatePaymentAsync(receipt.Id, 1500000m, "Chuyển khoản đợt 1");
            Assert.True(paySuccess);

            var reloaded = await _context.StockReceipts.FindAsync(receipt.Id);
            Assert.Equal("Partial", reloaded!.PaymentStatus);
            Assert.Equal(1500000m, reloaded.PaidAmount);
            Assert.Equal(500000m, reloaded.DebtAmount);

            // Thanh toán nốt 500.000đ
            await _inventoryService.UpdatePaymentAsync(receipt.Id, 500000m, "Thanh toán đợt cuối");
            var finalReceipt = await _context.StockReceipts.FindAsync(receipt.Id);
            Assert.Equal("Paid", finalReceipt!.PaymentStatus);
            Assert.Equal(0m, finalReceipt.DebtAmount);
        }

        /// <summary>
        /// YÊU CẦU 9: Gợi ý giá nhập từ nơi khác (So sánh giá từ bảng cung ứng hoặc đơn nhập cũ).
        /// </summary>
        [Fact]
        public async Task Req9_PriceComparison_ReturnsSuppliedPriceFromSuppliers()
        {
            var comparisons = await _inventoryService.GetPriceComparisonAsync(1);
            Assert.NotNull(comparisons);
            Assert.NotEmpty(comparisons);
            Assert.Equal("Công ty Cổ phần Phân phối Dược Mỹ phẩm Nam Việt", comparisons.First().SupplierName);
            Assert.Equal(150000m, comparisons.First().ImportPrice);
        }

        /// <summary>
        /// YÊU CẦU 10: Liên hệ giá nhập với giá bán.
        /// Nhập giá mới kèm định lại giá bán -> Hệ thống tự động cập nhật ProductVariant.RetailPrice.
        /// </summary>
        [Fact]
        public async Task Req10_Repricing_UpdatesRetailPriceWhenNewRetailPriceSpecified()
        {
            // Ban đầu Variant 1 có RetailPrice = 250.000đ
            var initialVar = await _context.ProductVariants.FindAsync(1);
            Assert.Equal(250000m, initialVar!.RetailPrice);

            // Nhập hàng với giá nhập tăng lên và định lại giá bán mới = 320.000đ
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel
                    {
                        VariantId = 1,
                        Quantity = 10,
                        ImportPrice = 210000m,
                        NewRetailPrice = 320000m // Định lại giá bán mới
                    },
                    new ReceiptItemInputModel
                    {
                        VariantId = 2,
                        Quantity = 10,
                        ImportPrice = 220000m,
                        NewRetailPrice = 360000m
                    }
                }
            };

            var (success, _, _) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");
            Assert.True(success);

            // Kiểm tra giá bán lẻ mới đã được cập nhật thành công trong CSDL
            var updatedVar1 = await _context.ProductVariants.FindAsync(1);
            var updatedVar2 = await _context.ProductVariants.FindAsync(2);

            Assert.Equal(320000m, updatedVar1!.RetailPrice);
            Assert.Equal(210000m, updatedVar1.OriginalPrice);

            Assert.Equal(360000m, updatedVar2!.RetailPrice);
            Assert.Equal(220000m, updatedVar2.OriginalPrice);
        }

        // =========================================================================
        // YÊU CẦU 8: CÁC TEST CASES KIỂM THỬ TOÀN DIỆN VỀ QUY TẮC NHẬP KHO & RÀNG BUỘC
        // =========================================================================

        /// <summary>
        /// TEST 1: Lưu phiếu nhập kho hợp lệ (Tăng tồn kho, ghi InventoryLog, lưu đúng StockReceiptItems)
        /// </summary>
        [Fact]
        public async Task Req8_SaveValidReceipt_Success()
        {
            var model = new CreateStockReceiptViewModel
            {
                StoreId = 1, // Kho tổng TP.HCM
                ReceiptType = "Domestic",
                SupplierId = 1, // NCC Nam Việt (Việt Nam)
                OriginCountry = "Việt Nam",
                PaymentStatus = "Paid",
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 40, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 20, ImportPrice = 180000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(model, 1, "Thủ kho");

            Assert.True(success, message);
            Assert.NotNull(receipt);
            Assert.StartsWith("PN-TN-", receipt.ReceiptCode);

            // Kiểm tra tồn kho được cộng dồn chính xác
            var inv1 = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            var inv2 = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 2);
            Assert.NotNull(inv1);
            Assert.NotNull(inv2);
            Assert.True(inv1.StockQuantity >= 40);
            Assert.True(inv2.StockQuantity >= 20);

            // Kiểm tra nhật ký biến động kho (InventoryLog) được ghi đúng
            var logs = await _context.InventoryLogs
                .Where(l => l.InventoryId == inv1.Id && l.ChangeType == "Import")
                .ToListAsync();
            Assert.NotEmpty(logs);

            // Kiểm tra giá vốn niêm yết (OriginalPrice) được cập nhật theo phiếu nhập mới
            var v1 = await _context.ProductVariants.FindAsync(1);
            Assert.Equal(150000m, v1!.OriginalPrice);
        }

        /// <summary>
        /// TEST 2: Nhập khẩu thiếu cảng hoặc sai tỉnh bị chặn
        /// </summary>
        [Fact]
        public async Task Req8_ImportReceipt_MissingPortOrWrongProvince_Blocked()
        {
            // Trường hợp A: Nhập khẩu nhưng thiếu cảng (ImportPortId = null) -> Bị từ chối
            var modelMissingPort = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Import",
                ImportPortId = null,
                SupplierId = 2, // NCC Hàn Quốc
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (successA, messageA, _) = await _inventoryService.CreateMultiItemReceiptAsync(modelMissingPort, 1, "Thủ kho");
            Assert.False(successA);
            Assert.Contains("cảng", messageA.ToLower());

            // Trường hợp B: Chọn Cảng Hải Phòng (Mã 31) nhưng chọn Kho Cần Thơ (StoreId = 5, Mã 92) -> Sai tỉnh -> Bị chặn
            var modelWrongProvince = new CreateStockReceiptViewModel
            {
                StoreId = 5, // Kho Cần Thơ (ProvinceCode = 92)
                ReceiptType = "Import",
                ImportPortId = 4, // Cảng Lạch Huyện Hải Phòng (ProvinceCode = 31)
                SupplierId = 2, // NCC Hàn Quốc
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (successB, messageB, _) = await _inventoryService.CreateMultiItemReceiptAsync(modelWrongProvince, 1, "Thủ kho");
            Assert.False(successB);
            Assert.Contains("không cùng tỉnh", messageB.ToLower());
        }

        /// <summary>
        /// TEST 3: Nhập khẩu với xuất xứ hoặc nhà cung cấp Việt Nam bị chặn
        /// </summary>
        [Fact]
        public async Task Req8_ImportReceipt_OriginVietnam_Blocked()
        {
            // Trường hợp A: Chọn hình thức Nhập khẩu nhưng chọn NCC Việt Nam (SupplierId = 1, Country = "Việt Nam") -> Bị chặn
            var modelWithDomesticSup = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Import",
                ImportPortId = 2, // Cảng Tân Cảng - Cát Lái (TP.HCM, Id = 2)
                SupplierId = 1, // NCC Nam Việt (Việt Nam)
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (successA, messageA, _) = await _inventoryService.CreateMultiItemReceiptAsync(modelWithDomesticSup, 1, "Thủ kho");
            Assert.False(successA);
            Assert.Contains("nước ngoài", messageA.ToLower());

            // Trường hợp B: Chọn NCC nước ngoài (SupplierId = 2) nhưng cố tình truyền OriginCountry = "Việt Nam" -> Bị chặn
            var modelWithOriginVN = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Import",
                ImportPortId = 2,
                SupplierId = 2, // NCC Hàn Quốc
                OriginCountry = "Việt Nam", // Sai quy tắc
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (successB, messageB, _) = await _inventoryService.CreateMultiItemReceiptAsync(modelWithOriginVN, 1, "Thủ kho");
            Assert.False(successB);
            Assert.Contains("không thể có xuất xứ là 'việt nam'", messageB.ToLower());
        }

        /// <summary>
        /// TEST 4: Một dòng sai thì không lưu phần nào và tồn kho không đổi (Transaction rollback toàn bộ)
        /// </summary>
        [Fact]
        public async Task Req8_CreateReceipt_OneItemInvalid_TransactionRollback()
        {
            // Lấy số lượng tồn kho ban đầu
            var inv1Before = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            int initialStock1 = inv1Before?.StockQuantity ?? 0;

            var inv2Before = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 2);
            int initialStock2 = inv2Before?.StockQuantity ?? 0;

            int initialReceiptCount = await _context.StockReceipts.CountAsync();

            // Tạo phiếu với Dòng 1 hợp lệ, Dòng 2 có số lượng âm (-10)
            var modelWithInvalidLine = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 50, ImportPrice = 150000m }, // Hợp lệ
                    new ReceiptItemInputModel { VariantId = 2, Quantity = -10, ImportPrice = 180000m }  // LỖI: SL âm!
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(modelWithInvalidLine, 1, "Thủ kho");

            // Kiểm tra: Bị từ chối và báo lỗi rõ theo số dòng
            Assert.False(success);
            Assert.Null(receipt);
            Assert.Contains("Dòng 2", message);

            // Kiểm tra: Không có phiếu nhập nào được tạo trong DB
            int finalReceiptCount = await _context.StockReceipts.CountAsync();
            Assert.Equal(initialReceiptCount, finalReceiptCount);

            // Kiểm tra: Tồn kho của CẢ HAI mặt hàng hoàn toàn không đổi (Rollback 100%)
            var inv1After = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 1);
            var inv2After = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 1 && i.VariantId == 2);
            Assert.Equal(initialStock1, inv1After?.StockQuantity ?? 0);
            Assert.Equal(initialStock2, inv2After?.StockQuantity ?? 0);
        }

        /// <summary>
        /// TEST 5: Giá nhập được tự động điền đúng theo lần nhập gần nhất với nhà cung cấp đó
        /// </summary>
        [Fact]
        public async Task Req8_AutoFillImportPrice_FromLastReceiptWithSupplier()
        {
            // Bước 1: Tạo phiếu nhập lịch sử với NCC 1 (Nam Việt) có đơn giá 168.000đ cho Variant 1
            var receiptModel = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 20, ImportPrice = 168000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 20, ImportPrice = 192000m }
                }
            };
            var (createOk, _, _) = await _inventoryService.CreateMultiItemReceiptAsync(receiptModel, 1, "Thủ kho");
            Assert.True(createOk);

            // Bước 2: Gọi hàm lấy giá nhập gần nhất theo NCC 1
            var (lastPrice, supplierName, _, receiptCode) = await _inventoryService.GetLastImportPriceAsync(1, 1);

            Assert.NotNull(lastPrice);
            Assert.Equal(168000m, lastPrice.Value);
            Assert.Equal("Công ty Cổ phần Phân phối Dược Mỹ phẩm Nam Việt", supplierName);
            Assert.NotNull(receiptCode);
        }

        // =========================================================================
        // BỘ TEST GIAI ĐOẠN 1: MIGRATION VARIANTID, NGÀY NHẬP & BẮT BUỘC NHẬP TAY
        // =========================================================================

        /// <summary>
        /// Test Giai đoạn 1 - Mục 2: Kiểm tra dữ liệu chuyển đổi SupplierProduct sang VariantId
        /// (số dòng, giá bảo toàn, không trùng cặp SupplierId-VariantId, có ghi chú rà soát)
        /// </summary>
        [Fact]
        public async Task Phase1_SupplierProduct_NoDuplicatePairs_AndHasReviewNotes()
        {
            // Kiểm tra các dòng bảng cung ứng trong DbContext test
            var spList = await _context.SupplierProducts.ToListAsync();
            Assert.NotEmpty(spList);

            // Không trùng cặp (SupplierId, VariantId)
            var duplicateCount = spList
                .GroupBy(sp => new { sp.SupplierId, sp.VariantId })
                .Count(g => g.Count() > 1);
            Assert.Equal(0, duplicateCount);

            // Kiểm tra giá tham chiếu hợp lệ
            Assert.All(spList, sp => Assert.True(sp.SuppliedPrice > 0));
        }

        /// <summary>
        /// Test Giai đoạn 1 - Mục 4: Ngày nhập kho trong tương lai bị chặn ở server
        /// </summary>
        [Fact]
        public async Task Phase1_ReceiptDate_FutureDate_IsRejectedByServer()
        {
            var receiptModel = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                ImportDate = DateTime.Now.AddDays(2), // Ngày tương lai
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(receiptModel, 1, "Thủ kho");

            Assert.False(success);
            Assert.Null(receipt);
            Assert.Contains("tương lai", message.ToLower());
        }

        /// <summary>
        /// Test Giai đoạn 1 - Mục 4: Ngày nhập kho lùi quá giới hạn cho phép (30 ngày) bị chặn ở server
        /// </summary>
        [Fact]
        public async Task Phase1_ReceiptDate_ExceedsMaxPastDaysAllowed_IsRejectedByServer()
        {
            var receiptModel = new CreateStockReceiptViewModel
            {
                StoreId = 1,
                ReceiptType = "Domestic",
                SupplierId = 1,
                ImportDate = DateTime.Now.AddDays(-45), // Lùi quá 30 ngày
                Items = new List<ReceiptItemInputModel>
                {
                    new ReceiptItemInputModel { VariantId = 1, Quantity = 10, ImportPrice = 150000m },
                    new ReceiptItemInputModel { VariantId = 2, Quantity = 10, ImportPrice = 180000m }
                }
            };

            var (success, message, receipt) = await _inventoryService.CreateMultiItemReceiptAsync(receiptModel, 1, "Thủ kho");

            Assert.False(success);
            Assert.Null(receipt);
            Assert.Contains("lùi quá", message.ToLower());
        }

        /// <summary>
        /// Test Giai đoạn 1 - Mục 5: Đơn giá nhập lấy từ lần nhập trước của chính NCC đó;
        /// Chưa từng nhập thì trả về null để giao diện bắt buộc nhập tay
        /// </summary>
        [Fact]
        public async Task Phase1_AutoFillPrice_WhenNotPreviouslyImported_ReturnsNull_ForcingManualInput()
        {
            // Variant 3 chưa từng được nhập từ Supplier 1
            var (lastPrice, supplierName, importDate, receiptCode) = await _inventoryService.GetLastImportPriceAsync(3, 1);

            // Phải trả về null (không fallback lung tung) để frontend hiện 'Chưa nhập' và bắt buộc gõ tay
            Assert.Null(lastPrice);
            Assert.Null(receiptCode);
        }

        // =========================================================================
        // BỘ TEST: BẢNG SO SÁNH GIÁ ĐA NHÀ CUNG CẤP THEO BIẾN THỂ
        // 1. NCC có cả hai loại giá: Giá chào và Giá nhập lần gần nhất
        // 2. Chỉ có giá chào: Chưa từng nhập
        // 3. Giá chào cũ hơn giá nhập mới: Tách bạch rõ, Giá tốt nhất chỉ tính trên Giá chào
        // =========================================================================

        /// <summary>
        /// TEST 1: Nhà cung cấp có cả hai loại giá (Giá chào hiện tại và Giá nhập lần gần nhất)
        /// - Tách bạch rõ 2 loại giá
        /// - Tính chính xác chênh lệch giữa giá chào và giá nhập lần trước của chính NCC đó (DiffFromPastImport)
        /// - Giá tốt nhất và so sánh với giá tốt nhất chỉ dựa trên Giá chào hiện tại
        /// </summary>
        [Fact]
        public async Task PriceComparison_SupplierHasBothSuppliedPriceAndPastImport_CalculatesDifferencesCorrectly()
        {
            int testVariantId = 1;
            int supplierId = 1;

            // 1. Cập nhật bảng cung ứng: Giá chào = 150.000đ, ngày cập nhật 5 ngày trước
            var sp = await _context.SupplierProducts.FirstOrDefaultAsync(x => x.VariantId == testVariantId && x.SupplierId == supplierId);
            if (sp == null)
            {
                sp = new SupplierProduct { VariantId = testVariantId, SupplierId = supplierId, SuppliedPrice = 150000m, UpdatedAt = DateTime.Now.AddDays(-5) };
                _context.SupplierProducts.Add(sp);
            }
            else
            {
                sp.SuppliedPrice = 150000m;
                sp.UpdatedAt = DateTime.Now.AddDays(-5);
            }

            // 2. Tạo phiếu nhập gần nhất với NCC 1: Giá nhập = 140.000đ, ngày nhập 2 ngày trước
            var receipt = new StockReceipt
            {
                ReceiptCode = "PN-TEST-BOTH-01",
                ReceiptType = "Domestic",
                StoreId = 1,
                SupplierId = supplierId,
                VariantId = testVariantId,
                Supplier = "Công ty Cổ phần Phân phối Dược Mỹ phẩm Nam Việt",
                ImportDate = DateTime.Now.AddDays(-2),
                CreatedAt = DateTime.Now.AddDays(-2),
                TotalAmount = 1400000m,
                PaymentStatus = "Paid",
                Items = new List<StockReceiptItem>
                {
                    new StockReceiptItem
                    {
                        VariantId = testVariantId,
                        Quantity = 10,
                        ImportPrice = 140000m,
                        TotalPrice = 1400000m
                    }
                }
            };
            _context.StockReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            // 3. Gọi service lấy danh sách so sánh giá
            var comparisons = await _inventoryService.GetSupplierPriceComparisonsAsync(testVariantId);
            var item = comparisons.FirstOrDefault(c => c.SupplierId == supplierId);

            // Kiểm tra
            Assert.NotNull(item);
            Assert.Equal(150000m, item.SuppliedPrice);
            Assert.NotNull(item.SuppliedPriceUpdatedAt);
            Assert.True(item.HasPastImport);
            Assert.Equal(140000m, item.LastImportPrice);
            Assert.Equal("PN-TEST-BOTH-01", item.LastReceiptCode);

            // Chênh lệch nội bộ giữa Giá chào và Giá nhập cũ: 150.000 - 140.000 = +10.000đ (+7.1%)
            Assert.NotNull(item.DiffFromPastImport);
            Assert.Equal(10000m, item.DiffFromPastImport.Value);
            Assert.NotNull(item.DiffFromPastImportPercent);
            Assert.Equal(7.1, item.DiffFromPastImportPercent.Value);
        }

        /// <summary>
        /// TEST 2: Nhà cung cấp chỉ có giá chào (chưa từng nhập từ NCC này)
        /// - Hiển thị giá chào hiện tại kèm ngày cập nhật
        /// - LastImportPrice == null, HasPastImport == false
        /// - DiffFromPastImport == null (không có lịch sử nhập để đối chiếu)
        /// - Vẫn tham gia xếp hạng "Giá tốt nhất" bình thường theo Giá chào
        /// </summary>
        [Fact]
        public async Task PriceComparison_SupplierHasOnlySuppliedPrice_NoPastImportHistory()
        {
            int testVariantId = 2;
            int supplierId = 2; // supplierForeign: chưa từng nhập Variant 2

            // Xóa mọi phiếu nhập trước đó của NCC 2 với Variant 2 (nếu có)
            var oldReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.VariantId == testVariantId && i.StockReceipt != null && i.StockReceipt.SupplierId == supplierId)
                .ToListAsync();
            _context.StockReceiptItems.RemoveRange(oldReceiptItems);

            var sp = await _context.SupplierProducts.FirstOrDefaultAsync(x => x.VariantId == testVariantId && x.SupplierId == supplierId);
            if (sp == null)
            {
                sp = new SupplierProduct { VariantId = testVariantId, SupplierId = supplierId, SuppliedPrice = 165000m, UpdatedAt = DateTime.Now.AddDays(-3) };
                _context.SupplierProducts.Add(sp);
            }
            else
            {
                sp.SuppliedPrice = 165000m;
                sp.UpdatedAt = DateTime.Now.AddDays(-3);
            }
            await _context.SaveChangesAsync();

            // Gọi service
            var comparisons = await _inventoryService.GetSupplierPriceComparisonsAsync(testVariantId);
            var item = comparisons.FirstOrDefault(c => c.SupplierId == supplierId);

            // Kiểm tra
            Assert.NotNull(item);
            Assert.Equal(165000m, item.SuppliedPrice);
            Assert.False(item.HasPastImport);
            Assert.Null(item.LastImportPrice);
            Assert.Null(item.LastReceiptCode);
            Assert.Null(item.DiffFromPastImport);
            Assert.Null(item.DiffFromPastImportPercent);
        }

        /// <summary>
        /// TEST 3: Giá chào cũ hơn giá nhập mới
        /// - NCC A có Giá chào = 200.000đ (cập nhật ngày 01/10/2026), nhưng phiếu nhập mới ngày 15/10/2026 có Giá nhập = 180.000đ
        /// - NCC B có Giá chào = 190.000đ (cập nhật ngày 20/10/2026), chưa từng nhập
        /// - Kết quả: Giá tốt nhất CHỈ tính trên Giá chào hiện tại -> NCC B (190.000đ) là tốt nhất, KHÔNG BỊ TRỘN với giá nhập 180.000đ cũ của NCC A
        /// - Tách bạch rõ ràng ngày cập nhật giá chào và ngày của phiếu nhập
        /// </summary>
        [Fact]
        public async Task PriceComparison_SuppliedPriceOlderThanNewReceipt_BestPriceOnlyBasedOnSuppliedPrice()
        {
            // Tạo một biến thể mới riêng biệt để test so sánh đa NCC
            var testVariant = new ProductVariant
            {
                ProductId = 1,
                SKU = "TEST-COMPARE-VAR-99",
                RetailPrice = 300000m,
                OriginalPrice = 180000m
            };
            _context.ProductVariants.Add(testVariant);
            await _context.SaveChangesAsync();

            int varId = testVariant.Id;
            int supplierA = 1;
            int supplierB = 2;

            DateTime suppliedDateA = new DateTime(2026, 10, 1, 8, 0, 0);
            DateTime receiptDateA = new DateTime(2026, 10, 15, 10, 0, 0); // Phiếu nhập mới hơn giá chào của A
            DateTime suppliedDateB = new DateTime(2026, 10, 20, 9, 0, 0);

            // NCC A: Giá chào 200.000đ (01/10/2026)
            _context.SupplierProducts.Add(new SupplierProduct
            {
                VariantId = varId,
                SupplierId = supplierA,
                SuppliedPrice = 200000m,
                CreatedAt = suppliedDateA,
                UpdatedAt = suppliedDateA
            });

            // Phiếu nhập của NCC A: Giá nhập 180.000đ (15/10/2026)
            _context.StockReceipts.Add(new StockReceipt
            {
                ReceiptCode = "PN-NEWER-RECEIPT-A",
                ReceiptType = "Domestic",
                StoreId = 1,
                SupplierId = supplierA,
                VariantId = varId,
                ImportDate = receiptDateA,
                CreatedAt = receiptDateA,
                TotalAmount = 1800000m,
                PaymentStatus = "Paid",
                Items = new List<StockReceiptItem>
                {
                    new StockReceiptItem { VariantId = varId, Quantity = 10, ImportPrice = 180000m, TotalPrice = 1800000m }
                }
            });

            // NCC B: Giá chào 190.000đ (20/10/2026), chưa từng nhập
            _context.SupplierProducts.Add(new SupplierProduct
            {
                VariantId = varId,
                SupplierId = supplierB,
                SuppliedPrice = 190000m,
                CreatedAt = suppliedDateB,
                UpdatedAt = suppliedDateB
            });

            await _context.SaveChangesAsync();

            // Thực thi
            var comparisons = await _inventoryService.GetSupplierPriceComparisonsAsync(varId);

            var itemA = comparisons.FirstOrDefault(c => c.SupplierId == supplierA);
            var itemB = comparisons.FirstOrDefault(c => c.SupplierId == supplierB);

            Assert.NotNull(itemA);
            Assert.NotNull(itemB);

            // Kiểm tra NCC A: Giá chào và giá nhập tách bạch
            Assert.Equal(200000m, itemA.SuppliedPrice);
            Assert.Equal(suppliedDateA, itemA.SuppliedPriceUpdatedAt);
            Assert.True(itemA.HasPastImport);
            Assert.Equal(180000m, itemA.LastImportPrice);
            Assert.Equal(receiptDateA, itemA.LastImportDate);
            Assert.Equal("PN-NEWER-RECEIPT-A", itemA.LastReceiptCode);
            // Giá chào hiện tại cao hơn giá nhập ngày 15/10: 200k - 180k = +20k
            Assert.Equal(20000m, itemA.DiffFromPastImport);

            // Kiểm tra tính "Giá tốt nhất":
            // Phải là NCC B (190.000đ) vì chỉ so sánh trên Giá chào hiện tại (200.000đ vs 190.000đ)
            // Không được chọn giá 180.000đ của NCC A làm giá tốt nhất!
            Assert.True(itemB.IsLowestPrice);
            Assert.False(itemA.IsLowestPrice);
            Assert.Equal(0, itemB.DifferenceFromLowest);
            Assert.Equal(10000m, itemA.DifferenceFromLowest); // 200.000 - 190.000 = 10.000đ
        }

    }
}
