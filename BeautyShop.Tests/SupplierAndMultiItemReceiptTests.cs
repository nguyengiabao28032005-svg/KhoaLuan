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

            // Thêm Nhà cung cấp có địa chỉ phản hồi khi hàng lỗi (Yêu cầu 2)
            var supplier = new Supplier
            {
                Id = 1,
                Name = "Công ty TNHH L'Oréal Việt Nam",
                ContactPerson = "Nguyễn Thu Hà",
                Phone = "028 3936 9142",
                Address = "Tầng 12, Tòa nhà Vincom Center, 72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                TaxCode = "0304910242",
                Country = "Pháp",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Suppliers.Add(supplier);

            // Bảng Cung ứng (Yêu cầu 3): Gán sản phẩm 1, 2 cho NCC 1
            _context.SupplierProducts.AddRange(
                new SupplierProduct { Id = 1, SupplierId = 1, ProductId = 1, SuppliedPrice = 150000m, CreatedAt = DateTime.Now },
                new SupplierProduct { Id = 2, SupplierId = 1, ProductId = 2, SuppliedPrice = 190000m, CreatedAt = DateTime.Now }
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
            Assert.Equal("Pháp", supplier.Country);
        }

        /// <summary>
        /// YÊU CẦU 3: Bảng Cung ứng trung gian giữa Nhà cung cấp và Sản phẩm.
        /// Khi chọn nhà cung cấp 1, hệ thống tra cứu được đúng 2 sản phẩm họ cung cấp.
        /// </summary>
        [Fact]
        public async Task Req3_SupplyTable_FiltersProductsBelongingToSupplier()
        {
            var suppliedProductIds = await _context.SupplierProducts
                .Where(sp => sp.SupplierId == 1)
                .Select(sp => sp.ProductId)
                .ToListAsync();

            Assert.Contains(1, suppliedProductIds); // Son
            Assert.Contains(2, suppliedProductIds); // Phấn
            Assert.DoesNotContain(3, suppliedProductIds); // Chì kẻ mắt chưa gán
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
            Assert.Equal("Công ty TNHH L'Oréal Việt Nam", comparisons.First().SupplierName);
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
    }
}
