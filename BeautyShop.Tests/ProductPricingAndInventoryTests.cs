using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Controllers;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Xunit;

namespace BeautyShop.Tests
{
    public class ProductPricingAndInventoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;

        public ProductPricingAndInventoryTests()
        {
            // Thiết lập SQLite in-memory database để test cô lập và nhanh chóng
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo các bảng dữ liệu nền tảng
            var role = new Role { Id = 1, Name = "Admin" };
            var category = new Category { Name = "Chăm sóc da", Slug = "cham-soc-da" };
            var brand = new Brand { Name = "Cocoon Vietnam" };
            var store = new Store { Name = "Kho trung tâm", Address = "TP. Hồ Chí Minh", IsActive = true };
            var user = new User { Id = 1, RoleId = 1, FullName = "Thủ kho", Email = "staff@beautyshop.com", PasswordHash = "hash123" };
            _context.Roles.Add(role);
            _context.Categories.Add(category);
            _context.Brands.Add(brand);
            _context.Stores.Add(store);
            _context.Users.Add(user);
            _context.SaveChanges();

            _productService = new ProductService(_context);
            _inventoryService = new InventoryService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        /// <summary>
        /// TEST 1: Tạo sản phẩm mới theo quy trình không cần giá
        /// Kết quả mong đợi: Sản phẩm và biến thể mặc định được tạo thành công, RetailPrice và OriginalPrice đều là NULL, tồn kho = 0.
        /// </summary>
        [Fact]
        public async Task Test1_CreateProduct_WithoutPrice_SucceedsAndHasNoPriceOrInventory()
        {
            // Arrange: Chuẩn bị form tạo sản phẩm chỉ có thông tin mô tả, danh mục, dung tích, màu (không có ô giá và tồn kho)
            var model = new CreateProductViewModel
            {
                Name = "Nước hoa hồng Cocoon 140ml",
                CategoryId = 1,
                BrandId = 1,
                Volume = "140ml",
                ColorName = "Tự nhiên",
                Description = "Nước hoa hồng chiết xuất từ hoa hồng hữu cơ",
                IsActive = true
            };

            // Act: Gọi service tạo sản phẩm
            var (success, message, product) = await _productService.CreateProductAsync(model);

            // Assert: Kiểm tra tạo thành công
            Assert.True(success, message);
            Assert.NotNull(product);

            // Kiểm tra biến thể sinh ra trong database
            var variant = await _context.ProductVariants
                .Include(v => v.Inventories)
                .FirstOrDefaultAsync(v => v.ProductId == product.Id);

            Assert.NotNull(variant);
            Assert.Null(variant.RetailPrice);       // Giá bán lẻ chưa thiết lập
            Assert.Null(variant.OriginalPrice);     // Giá gốc chưa nhập
            Assert.Null(variant.WholesalePrice);    // Giá sỉ chưa thiết lập
            Assert.False(variant.HasPrice);         // Thuộc tính HasPrice = false

            // Kiểm tra tồn kho ban đầu = 0
            var inventory = variant.Inventories.FirstOrDefault();
            Assert.NotNull(inventory);
            Assert.Equal(0, inventory.StockQuantity);
        }

        /// <summary>
        /// TEST 2: Nhập hàng qua phiếu nhập làm tăng tồn kho và ghi nhận giá gốc (OriginalPrice)
        /// Kết quả mong đợi: StockQuantity tăng lên đúng số lượng nhập, OriginalPrice được cập nhật bằng ImportPrice.
        /// </summary>
        [Fact]
        public async Task Test2_StockReceipt_IncreasesInventoryAndRecordsOriginalPrice()
        {
            // Arrange: Tạo sản phẩm ban đầu chưa có giá
            var createModel = new CreateProductViewModel
            {
                Name = "Tinh chất hoa hồng Cocoon",
                CategoryId = 1,
                BrandId = 1,
                Volume = "30ml",
                IsActive = true
            };
            var (_, _, product) = await _productService.CreateProductAsync(createModel);
            var variant = await _context.ProductVariants.FirstAsync(v => v.ProductId == product!.Id);

            int importQuantity = 100;
            decimal importPrice = 125000m; // Giá nhập: 125.000đ

            // Act: Tạo phiếu nhập kho qua InventoryService
            var (success, message, receipt) = await _inventoryService.CreateReceiptAsync(
                variantId: variant.Id,
                quantity: importQuantity,
                importPrice: importPrice,
                supplier: "Nhà cung cấp Cocoon",
                importDate: DateTime.Now,
                userId: 1,
                userName: "Thủ kho",
                note: "Nhập hàng đợt 1 tháng 10"
            );

            // Assert: Phiếu nhập tạo thành công
            Assert.True(success, message);
            Assert.NotNull(receipt);
            Assert.Equal(importQuantity, receipt.Quantity);
            Assert.Equal(importPrice, receipt.ImportPrice);

            // Kiểm tra tồn kho biến thể đã được cộng đúng
            var updatedInventory = await _context.Inventories.FirstOrDefaultAsync(i => i.VariantId == variant.Id);
            Assert.NotNull(updatedInventory);
            Assert.Equal(importQuantity, updatedInventory.StockQuantity);

            // Kiểm tra giá gốc (OriginalPrice / Giá vốn) đã được ghi nhận từ phiếu nhập
            var updatedVariant = await _context.ProductVariants.FindAsync(variant.Id);
            Assert.NotNull(updatedVariant);
            Assert.Equal(importPrice, updatedVariant.OriginalPrice);
        }

        /// <summary>
        /// TEST 3: Biến thể chưa có giá bán thì không thể mua được (bị chặn khi thêm vào giỏ hàng)
        /// Kết quả mong đợi: CartController trả về thông báo lỗi, từ chối cho thêm vào giỏ hàng.
        /// </summary>
        [Fact]
        public async Task Test3_VariantWithoutRetailPrice_CannotBeAddedToCart()
        {
            // Arrange: Tạo sản phẩm và biến thể chưa thiết lập giá bán lẻ (RetailPrice = null)
            var createModel = new CreateProductViewModel
            {
                Name = "Mặt nạ nghệ Hưng Yên",
                CategoryId = 1,
                BrandId = 1,
                Volume = "100ml",
                IsActive = true
            };
            var (_, _, product) = await _productService.CreateProductAsync(createModel);
            var variant = await _context.ProductVariants.FirstAsync(v => v.ProductId == product!.Id);

            // Nhập hàng để có tồn kho > 0 nhưng CỐ TÌNH CHƯA THIẾT LẬP GIÁ BÁN
            await _inventoryService.CreateReceiptAsync(variant.Id, 20, 80000m, "NCC", DateTime.Now, 1, "Thủ kho", null);

            var unpricedVariant = await _context.ProductVariants.FindAsync(variant.Id);
            Assert.NotNull(unpricedVariant);
            Assert.False(unpricedVariant.HasPrice); // Xác nhận chưa có giá bán

            // Thiết lập Controller & HttpContext giả lập
            var cartController = new CartController(_context);
            var httpContext = new DefaultHttpContext();
            var session = new MockHttpSession();
            httpContext.Session = session;
            httpContext.Session.SetInt32("UserId", 1);
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

            cartController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act: Thử thêm biến thể chưa có giá vào giỏ hàng
            var result = cartController.AddToCart(productId: product!.Id, quantity: 1, variantId: variant.Id);

            // Assert: Hệ thống phải từ chối thêm vào giỏ hàng
            Assert.IsType<JsonResult>(result);
            var jsonResult = (JsonResult)result;
            dynamic data = jsonResult.Value!;
            
            // data.success phải là false và có thông báo lỗi chưa thiết lập giá
            bool isSuccess = (bool)data.GetType().GetProperty("success")!.GetValue(data, null)!;
            string returnMessage = (string)data.GetType().GetProperty("message")!.GetValue(data, null)!;

            Assert.False(isSuccess);
            Assert.Contains("giá", returnMessage.ToLower());
        }

        /// <summary>
        /// TEST 4: Đặt giá bán cho biến thể khi CHƯA có hàng tồn kho (Tồn kho = 0) phải bị từ chối
        /// Kết quả mong đợi: ProductService.SetVariantPricingAsync trả về false và thông báo lỗi yêu cầu nhập kho trước.
        /// </summary>
        [Fact]
        public async Task Test4_SetVariantPricing_WhenStockIsZero_IsRejectedWithClearMessage()
        {
            // Arrange: Tạo sản phẩm mới, tồn kho ban đầu = 0
            var createModel = new CreateProductViewModel
            {
                Name = "Kem chống nắng bí đao Cocoon",
                CategoryId = 1,
                BrandId = 1,
                Volume = "50ml",
                IsActive = true
            };
            var (_, _, product) = await _productService.CreateProductAsync(createModel);
            var variant = await _context.ProductVariants.FirstAsync(v => v.ProductId == product!.Id);

            // Act: Cố gắng đặt giá bán lẻ khi kho chưa có sản phẩm (Stock = 0)
            var (success, message) = await _productService.SetVariantPricingAsync(new SetPricingViewModel
            {
                VariantId = variant.Id,
                RetailPrice = 395000m,
                WholesalePrice = 320000m
            });

            // Assert: Phải bị từ chối kèm thông báo rõ ràng
            Assert.False(success);
            Assert.Contains("chưa có hàng trong kho", message.ToLower());

            // Giá bán lẻ trong database vẫn phải là null
            var reloadVariant = await _context.ProductVariants.FindAsync(variant.Id);
            Assert.Null(reloadVariant!.RetailPrice);
            Assert.False(reloadVariant.HasPrice);
        }

        /// <summary>
        /// TEST 5: Biến thể chưa có giá bán không thể đi qua bước thanh toán/tạo đơn (kiểm tra CheckoutController)
        /// Kết quả mong đợi: CheckoutController phát hiện và từ chối tạo đơn hàng, chuyển hướng về Cart và thông báo lỗi.
        /// </summary>
        [Fact]
        public void Test5_UnpricedVariant_CannotPassCheckoutOrOrderCreation()
        {
            // Arrange: Tạo sản phẩm và biến thể chưa có giá bán lẻ
            var product = new Product
            {
                Name = "Sản phẩm thử nghiệm",
                CategoryId = 1,
                BrandId = 1,
                IsActive = true
            };
            _context.Products.Add(product);
            _context.SaveChanges();

            var variant = new ProductVariant
            {
                ProductId = product.Id,
                SKU = "TEST-NO-PRICE-01",
                Volume = "Standard",
                OriginalPrice = 50000m,
                RetailPrice = null, // Cố tình không có giá bán
                WholesalePrice = null
            };
            _context.ProductVariants.Add(variant);
            _context.SaveChanges();

            // Giả lập giỏ hàng có chứa biến thể chưa có giá
            var cart = new List<CartItemViewModel>
            {
                new CartItemViewModel
                {
                    ProductId = 1,
                    VariantId = variant.Id,
                    ProductName = "Sản phẩm thử nghiệm",
                    Price = 0,
                    Quantity = 2
                }
            };

            var httpContext = new DefaultHttpContext();
            var session = new MockHttpSession();
            httpContext.Session = session;
            httpContext.Session.SetInt32("UserId", 1);
            BeautyShop.Helpers.SessionExtensions.SetObject(session, "CART", cart);

            var checkoutController = new CheckoutController(_context, null!, new DeliveryOptimizationService(_context));
            checkoutController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            checkoutController.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                httpContext,
                new MockTempDataProvider());

            // Act 1: Truy cập trang thanh toán (GET Checkout/Index)
            var getResult = checkoutController.Index();

            // Assert 1: Bị chặn ngay từ bước xem trang thanh toán
            var redirectGet = Assert.IsType<RedirectToActionResult>(getResult);
            Assert.Equal("Cart", redirectGet.ControllerName);
            Assert.Equal("Index", redirectGet.ActionName);
            Assert.True(checkoutController.TempData.ContainsKey("ErrorMessage"));
            Assert.Contains("giá", checkoutController.TempData["ErrorMessage"]!.ToString()!.ToLower());

            // Act 2: Cố tình gửi POST đặt hàng trực tiếp (POST Checkout/Index)
            var postModel = new CheckoutViewModel
            {
                CustomerName = "Nguyễn Văn A",
                PhoneNumber = "0901234567",
                ShippingAddress = "123 Đường ABC, Quận 1, TP.HCM",
                PaymentMethod = "COD"
            };
            var postResult = checkoutController.Index(postModel, null, null);

            // Assert 2: Bị chặn ở bước tạo đơn, không có đơn hàng nào được lưu vào CSDL
            var redirectPost = Assert.IsType<RedirectToActionResult>(postResult);
            Assert.Equal("Cart", redirectPost.ControllerName);
            Assert.Equal("Index", redirectPost.ActionName);
            Assert.Equal(0, _context.Orders.Count()); // Không có đơn hàng nào được tạo
        }
    }

    /// <summary>
    /// Mock ISession để kiểm thử CartController
    /// </summary>
    public class MockHttpSession : ISession
    {
        private readonly Dictionary<string, byte[]> _storage = new();

        public bool IsAvailable => true;
        public string Id => "test-session-id";
        public IEnumerable<string> Keys => _storage.Keys;

        public void Clear() => _storage.Clear();
        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _storage.Remove(key);
        public void Set(string key, byte[] value) => _storage[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _storage.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Mock ITempDataProvider để kiểm thử TempData trong Controller
    /// </summary>
    public class MockTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
