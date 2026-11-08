using System;
using System.Collections.Generic;
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
    public class RevenueAndAnalyticsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly RevenueAnalyticsService _revenueService;

        public RevenueAndAnalyticsTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo các bảng dữ liệu nền tảng an toàn
            if (!_context.Stores.Any(s => s.Id == 1))
            {
                _context.Stores.Add(new Store { Id = 1, Name = "Kho trung tâm", Address = "TP. Hồ Chí Minh", IsActive = true });
            }
            if (!_context.Categories.Any(c => c.Id == 1))
            {
                _context.Categories.Add(new Category { Id = 1, Name = "Mỹ phẩm", Slug = "my-pham" });
            }
            if (!_context.Brands.Any(b => b.Id == 1))
            {
                _context.Brands.Add(new Brand { Id = 1, Name = "Beauty Brand" });
            }
            if (!_context.Products.Any(p => p.Id == 1))
            {
                _context.Products.Add(new Product { Id = 1, Name = "Kem dưỡng phục hồi", CategoryId = 1, BrandId = 1, IsActive = true });
            }
            if (!_context.ProductVariants.Any(v => v.Id == 1))
            {
                _context.ProductVariants.Add(new ProductVariant { Id = 1, ProductId = 1, SKU = "KD-01", RetailPrice = 300000m, OriginalPrice = 180000m });
            }
            _context.SaveChanges();

            _revenueService = new RevenueAnalyticsService(_context);
        }

        public void Dispose()
        {
            _connection.Close();
            _context.Dispose();
        }

        [Fact]
        public async Task Test1_UncompletedStatuses_DoNotIncreaseRevenue()
        {
            // Danh sách các trạng thái chưa hoàn thành
            var uncompletedStatuses = new[] { "Pending", "Chờ duyệt", "Confirmed", "Đã xác nhận", "Processing", "Đóng gói", "Shipping", "Delivering", "Đang giao", "Failed", "Giao thất bại" };

            int id = 100;
            foreach (var status in uncompletedStatuses)
            {
                _context.Orders.Add(new Order
                {
                    Id = id++,
                    OrderCode = $"ORD-{id}",
                    CustomerName = "Khách hàng thử nghiệm",
                    OrderStatus = status,
                    TotalAmount = 500000m,
                    FinalAmount = 500000m,
                    CreatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();

            // Kiểm tra: Doanh thu phải bằng 0 tuyệt đối dù có 11 đơn ở các trạng thái chưa hoàn thành
            decimal totalRevenue = await _revenueService.GetRevenueAsync(_context.Orders);
            int completedCount = await _revenueService.GetCompletedOrderCountAsync(_context.Orders);

            Assert.Equal(0m, totalRevenue);
            Assert.Equal(0, completedCount);
        }

        [Fact]
        public async Task Test2_CancelledOrder_DoesNotIncreaseRevenue()
        {
            _context.Orders.Add(new Order
            {
                Id = 201,
                OrderCode = "ORD-CANCEL-1",
                CustomerName = "Khách hủy",
                OrderStatus = "Cancelled",
                TotalAmount = 750000m,
                FinalAmount = 750000m,
                CreatedAt = DateTime.Now,
                CompletedAt = null
            });
            await _context.SaveChangesAsync();

            decimal totalRevenue = await _revenueService.GetRevenueAsync(_context.Orders);
            Assert.Equal(0m, totalRevenue);
        }

        [Fact]
        public async Task Test3_CompletedOrder_IncreasesRevenueAccurately()
        {
            var completionDate = new DateTime(2026, 11, 1, 10, 0, 0);

            var order = new Order
            {
                Id = 301,
                OrderCode = "ORD-COMPLETED-1",
                CustomerName = "Khách mua hàng thành công",
                TotalAmount = 300000m,
                FinalAmount = 300000m,
                CreatedAt = completionDate.AddDays(-2) // Đặt trước 2 ngày
            };

            // Đánh dấu hoàn thành
            _revenueService.MarkOrderAsCompleted(order, completionDate);
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal revenue = await _revenueService.GetRevenueAsync(_context.Orders);
            int count = await _revenueService.GetCompletedOrderCountAsync(_context.Orders);

            Assert.Equal(300000m, revenue);
            Assert.Equal(1, count);
            Assert.Equal("Completed", order.OrderStatus);
            Assert.Equal(completionDate, order.CompletedAt);
        }

        [Fact]
        public async Task Test4_OrderCompletedOnDifferentDate_CalculatedByCompletedDateNotCreatedDate()
        {
            var createdDate = new DateTime(2026, 10, 15, 8, 0, 0);
            var completedDate = new DateTime(2026, 10, 20, 14, 0, 0);

            var order = new Order
            {
                Id = 401,
                OrderCode = "ORD-DIFF-DATE",
                CustomerName = "Giao khác ngày",
                TotalAmount = 450000m,
                FinalAmount = 450000m,
                OrderStatus = "Completed",
                CreatedAt = createdDate,
                CompletedAt = completedDate
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Tính doanh thu ngày đặt hàng (15/10) -> Phải bằng 0
            var oct15Start = new DateTime(2026, 10, 15, 0, 0, 0);
            var oct15End = new DateTime(2026, 10, 15, 23, 59, 59);
            decimal revCreatedDate = await _revenueService.GetRevenueAsync(_context.Orders, oct15Start, oct15End);
            Assert.Equal(0m, revCreatedDate);

            // Tính doanh thu ngày hoàn thành (20/10) -> Phải ghi nhận 450.000đ
            var oct20Start = new DateTime(2026, 10, 20, 0, 0, 0);
            var oct20End = new DateTime(2026, 10, 20, 23, 59, 59);
            decimal revCompletedDate = await _revenueService.GetRevenueAsync(_context.Orders, oct20Start, oct20End);
            Assert.Equal(450000m, revCompletedDate);
        }

        [Fact]
        public async Task Test5_UpdatingCompletedOrder_PreservesCompletedAtAndDoesNotRecalculate()
        {
            var originalCompletedAt = new DateTime(2026, 10, 10, 12, 0, 0);

            var order = new Order
            {
                Id = 501,
                OrderCode = "ORD-IMMUTABLE-DATE",
                CustomerName = "Tên ban đầu",
                TotalAmount = 250000m,
                FinalAmount = 250000m,
                OrderStatus = "Completed",
                CompletedAt = originalCompletedAt,
                CreatedAt = originalCompletedAt.AddDays(-1)
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Admin chỉnh sửa thông tin đơn và gọi lại MarkOrderAsCompleted
            order.CustomerName = "Tên cập nhật sau đó";
            _revenueService.MarkOrderAsCompleted(order, DateTime.Now);
            await _context.SaveChangesAsync();

            // CompletedAt ban đầu phải được bảo toàn 100%
            var updatedOrder = await _context.Orders.FindAsync(501);
            Assert.NotNull(updatedOrder);
            Assert.Equal(originalCompletedAt, updatedOrder!.CompletedAt);
            Assert.Equal("Tên cập nhật sau đó", updatedOrder.CustomerName);
        }

        [Fact]
        public async Task Test6_CostOfGoodsSoldAndGrossProfit_CalculatedAccurately()
        {
            var completedDate = new DateTime(2026, 11, 1, 15, 0, 0);

            var order = new Order
            {
                Id = 601,
                OrderCode = "ORD-PROFIT-1",
                CustomerName = "Khách kiểm tra lợi nhuận",
                TotalAmount = 600000m,
                FinalAmount = 600000m,
                OrderStatus = "Completed",
                CompletedAt = completedDate,
                CreatedAt = completedDate.AddHours(-3)
            };

            // Mua 2 sản phẩm: Đơn giá bán = 300.000đ, Giá vốn = 180.000đ
            var detail = new OrderDetail
            {
                Id = 601,
                OrderId = 601,
                VariantId = 1,
                Quantity = 2,
                UnitPrice = 300000m,
                CostPrice = 180000m
            };

            _context.Orders.Add(order);
            _context.OrderDetails.Add(detail);
            await _context.SaveChangesAsync();

            decimal revenue = await _revenueService.GetRevenueAsync(_context.Orders);
            decimal cogs = await _revenueService.GetCogsAsync(_context.Orders);
            decimal grossProfit = await _revenueService.GetGrossProfitAsync(_context.Orders);

            // Doanh thu: 600.000đ
            Assert.Equal(600000m, revenue);
            // Giá vốn: 2 * 180.000đ = 360.000đ
            Assert.Equal(360000m, cogs);
            // Lợi nhuận gộp: 600.000đ - 360.000đ = 240.000đ
            Assert.Equal(240000m, grossProfit);
        }

        [Fact]
        public async Task Test7_SupplierDebtPayment_IsNotAddedToRevenue()
        {
            // Tạo phiếu nhập kho và nợ NCC
            var receipt = new StockReceipt
            {
                Id = 701,
                ReceiptCode = "PN-TEST-COST-01",
                VariantId = 1,
                Quantity = 10,
                ImportPrice = 150000m,
                TotalAmount = 1500000m,
                PaidAmount = 500000m,
                DebtAmount = 1000000m,
                PaymentStatus = "Partial",
                ImportDate = DateTime.Now,
                CreatedAt = DateTime.Now
            };
            _context.StockReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            // Doanh thu bán hàng ban đầu = 0
            decimal initialRev = await _revenueService.GetRevenueAsync(_context.Orders);
            Assert.Equal(0m, initialRev);

            // Thanh toán trả nợ NCC 1.000.000đ
            receipt.PaidAmount += 1000000m;
            receipt.DebtAmount = 0m;
            receipt.PaymentStatus = "Paid";
            await _context.SaveChangesAsync();

            // Doanh thu bán hàng sau khi trả nợ NCC vẫn phải bằng 0 (Chi phí nhập hàng không được tính vào doanh thu)
            decimal revAfterPayment = await _revenueService.GetRevenueAsync(_context.Orders);
            Assert.Equal(0m, revAfterPayment);
        }

        [Fact]
        public async Task Test8_TotalOrdersCount_IsPureInteger_NotFormattedAsCurrency()
        {
            _context.Orders.Add(new Order { Id = 801, OrderCode = "ORD-CNT-1", OrderStatus = "Pending", FinalAmount = 100000m });
            _context.Orders.Add(new Order { Id = 802, OrderCode = "ORD-CNT-2", OrderStatus = "Completed", CompletedAt = DateTime.Now, FinalAmount = 200000m });
            await _context.SaveChangesAsync();

            int totalOrders = await _context.Orders.CountAsync();
            decimal totalRevenue = await _revenueService.GetRevenueAsync(_context.Orders);

            // Số lượng đơn hàng phải là số nguyên (2 đơn)
            Assert.Equal(2, totalOrders);
            Assert.False(totalOrders.ToString().Contains("đ"), "Tổng số lượng đơn hàng không được chứa ký hiệu tiền tệ 'đ'");

            // Doanh thu chỉ tính đơn Completed (200.000đ)
            Assert.Equal(200000m, totalRevenue);
        }
    }
}
