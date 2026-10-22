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
    /// <summary>
    /// Bộ kiểm thử tự động cho BƯỚC 5: Chức năng giao hàng tối ưu và Quyết toán tiền COD / Đối soát cuối ngày
    /// Kiểm tra 100% các quy tắc nghiệp vụ:
    /// 1. Quy tắc ca giao mặc định theo giờ chốt cấu hình (trước 10:00 -> ca Chiều hôm nay; sau 10:00 -> ca Sáng ngày mai).
    /// 2. Gom chuyến tự động theo Ngày + Ca + Cụm, tách chuyến khi vượt ngưỡng N (mặc định 8 đơn).
    /// 3. Sắp xếp thứ tự điểm dừng theo SortOrder của khu vực, không đi lòng vòng quay lại khu vực cũ.
    /// 4. Đơn hàng không COD (đã thanh toán trực tuyến) có CodExpected = 0.
    /// 5. Không cho phép gán một đơn hàng vào hai chuyến hoặc hai điểm dừng cùng lúc (ràng buộc unique filtered index).
    /// 6. Hủy / gỡ đơn khỏi chuyến cập nhật lại tổng số đơn và tổng COD của chuyến.
    /// 7. Giao hàng thất bại bắt buộc có lý do; tăng số lần giao lại; vượt quá giới hạn (>=2) chuyển sang Chờ xử lý.
    /// 8. Quyết toán tiền COD giữa Shipper và Thu ngân; Đối soát cuối ngày giữa Thu ngân và Admin, khóa sau khi duyệt.
    /// </summary>
    public class DeliveryOptimizationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly DeliveryOptimizationService _deliveryService;

        public DeliveryOptimizationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo Roles & Users theo vai trò chức năng (Admin, Warehouse, Cashier, Shipper)
            var adminRole = new Role { Id = 1, Name = "Admin", DisplayName = "Quản trị viên" };
            var staffRole = new Role { Id = 2, Name = "Employee", DisplayName = "Nhân viên (Cũ)" };
            var warehouseRole = new Role { Id = 3, Name = "Warehouse", DisplayName = "Nhân viên kho" };
            var cashierRole = new Role { Id = 4, Name = "Cashier", DisplayName = "Thu ngân" };
            var shipperRole = new Role { Id = 5, Name = "Shipper", DisplayName = "Nhân viên giao hàng" };
            _context.Roles.AddRange(adminRole, staffRole, warehouseRole, cashierRole, shipperRole);

            var admin = new User { Id = 1, FullName = "Admin Quản Lý", Email = "admin@beautyshop.com", PasswordHash = "hash", RoleId = 1, Position = "Admin", IsActive = true };
            var shipper = new User { Id = 3, FullName = "Nguyễn Văn Shipper", Email = "shipper@beautyshop.com", PasswordHash = "hash", RoleId = 5, Position = "Shipper", IsActive = true };
            var cashier = new User { Id = 5, FullName = "Trần Thị Thu Ngân", Email = "cashier@beautyshop.com", PasswordHash = "hash", RoleId = 4, Position = "Cashier", IsActive = true };
            _context.Users.AddRange(admin, shipper, cashier);

            // Lưu ý: ApplicationDbContext.EnsureCreated() đã tự động seed 14 DeliveryZones mẫu cho TP.HCM và Lâm Đồng.

            // Khởi tạo sản phẩm để phục vụ tính báo cáo đối soát
            var brand = new Brand { Id = 1, Name = "La Roche-Posay" };
            var category = new Category { Id = 1, Name = "Chăm sóc da", Slug = "cham-soc-da" };
            var product = new Product { Id = 1, Name = "Kem chống nắng Anthelios", BrandId = 1, CategoryId = 1, Description = "Mô tả", IsActive = true };
            var variant = new ProductVariant { Id = 1, ProductId = 1, SKU = "LRP-01", Volume = "50ml", OriginalPrice = 300000, RetailPrice = 450000 };

            _context.Brands.Add(brand);
            _context.Categories.Add(category);
            _context.Products.Add(product);
            _context.ProductVariants.Add(variant);

            _context.SaveChanges();

            _deliveryService = new DeliveryOptimizationService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public void Test1_DefaultDeliverySchedule_RespectsCutoffTime()
        {
            // Case A: Đặt trước 10:00 (ví dụ 08:30 sáng) -> Giao ca Chiều cùng ngày
            var morningTime = new DateTime(2026, 10, 16, 8, 30, 0);
            var scheduleMorning = DeliveryConfig.GetDefaultDeliverySchedule(morningTime);

            Assert.Equal(morningTime.Date, scheduleMorning.ExpectedDate);
            Assert.Equal("Afternoon", scheduleMorning.Shift);
            Assert.Contains("trước giờ chốt", scheduleMorning.Reason);

            // Case B: Đặt từ 10:00 trở đi (ví dụ 14:15 chiều) -> Giao ca Sáng ngày hôm sau
            var afternoonTime = new DateTime(2026, 10, 16, 14, 15, 0);
            var scheduleAfternoon = DeliveryConfig.GetDefaultDeliverySchedule(afternoonTime);

            Assert.Equal(afternoonTime.Date.AddDays(1), scheduleAfternoon.ExpectedDate);
            Assert.Equal("Morning", scheduleAfternoon.Shift);
            Assert.Contains("sau giờ chốt", scheduleAfternoon.Reason);
        }

        [Fact]
        public async Task Test2_AutoBatch_GroupsByDateShiftCluster_AndSplitsWhenExceedingN()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Afternoon";

            // Tạo 10 đơn hàng cùng ngày, cùng ca Afternoon, cùng cụm HCM_NW
            for (int i = 1; i <= 10; i++)
            {
                var order = new Order
                {
                    Id = 100 + i,
                    OrderCode = $"ORD-NW-{i:D2}",
                    UserId = 1,
                    ShippingAddress = $"Địa chỉ {i}, Hóc Môn",
                    DeliveryZoneId = 1, // Hóc Môn (Cluster HCM_NW)
                    ExpectedDeliveryDate = targetDate,
                    DeliveryShift = targetShift,
                    DeliveryStatus = "ReadyForDelivery",
                    OrderStatus = "Processing",
                    TotalAmount = 200000,
                    PaymentMethod = "COD",
                    FailedDeliveryAttempts = 0,
                    CreatedAt = DateTime.Now
                };
                _context.Orders.Add(order);
            }

            // Tạo 1 đơn khác ca (Morning) -> Không được vào batch của Afternoon
            var morningOrder = new Order
            {
                Id = 199,
                OrderCode = "ORD-MORNING",
                UserId = 1,
                ShippingAddress = "Địa chỉ khác, Hóc Môn",
                DeliveryZoneId = 1,
                ExpectedDeliveryDate = targetDate,
                DeliveryShift = "Morning",
                DeliveryStatus = "ReadyForDelivery",
                OrderStatus = "Processing",
                TotalAmount = 200000,
                PaymentMethod = "COD",
                CreatedAt = DateTime.Now
            };
            _context.Orders.Add(morningOrder);
            await _context.SaveChangesAsync();

            // Thực hiện gom tự động với N = 8
            var result = await _deliveryService.AutoBatchTripsAsync(targetDate, targetShift, maxOrdersPerTrip: 8, createdByUserId: 1);

            Assert.True(result.Success);
            // 10 đơn vượt N=8 nên phải tách làm 2 chuyến
            Assert.Equal(2, result.CreatedTrips.Count);

            var trip1 = result.CreatedTrips[0];
            var trip2 = result.CreatedTrips[1];

            Assert.Equal(8, trip1.TotalOrders);
            Assert.Equal(2, trip2.TotalOrders);
            Assert.Equal("HCM_NW", trip1.ClusterCode);
            Assert.Equal("HCM_NW", trip2.ClusterCode);
            Assert.Equal(targetShift, trip1.Shift);

            // Đơn morning không bị gom
            var unassignedMorning = await _context.Orders.FindAsync(199);
            Assert.Equal("ReadyForDelivery", unassignedMorning!.DeliveryStatus);
        }

        [Fact]
        public async Task Test3_StopOrdering_InCluster_FollowsSortOrder_NoLoopback()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Morning";

            // Tạo các đơn xen kẽ giữa Hóc Môn (SortOrder = 10) và Củ Chi (SortOrder = 20)
            var ord1 = new Order { Id = 201, OrderCode = "ORD-CC-1", UserId = 1, ShippingAddress = "Củ Chi", DeliveryZoneId = 2, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 150000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var ord2 = new Order { Id = 202, OrderCode = "ORD-HM-1", UserId = 1, ShippingAddress = "Hóc Môn", DeliveryZoneId = 1, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 250000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var ord3 = new Order { Id = 203, OrderCode = "ORD-CC-2", UserId = 1, ShippingAddress = "Củ Chi", DeliveryZoneId = 2, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 300000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var ord4 = new Order { Id = 204, OrderCode = "ORD-HM-2", UserId = 1, ShippingAddress = "Hóc Môn", DeliveryZoneId = 1, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 350000, PaymentMethod = "COD", CreatedAt = DateTime.Now };

            _context.Orders.AddRange(ord1, ord2, ord3, ord4);
            await _context.SaveChangesAsync();

            var result = await _deliveryService.AutoBatchTripsAsync(targetDate, targetShift, maxOrdersPerTrip: 8, createdByUserId: 1);
            Assert.True(result.Success);
            Assert.Single(result.CreatedTrips);

            var trip = await _context.DeliveryTrips
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order)
                        .ThenInclude(o => o.DeliveryZone)
                .FirstAsync(t => t.Id == result.CreatedTrips[0].Id);

            var stops = trip.Stops.OrderBy(s => s.StopOrder).ToList();
            Assert.Equal(4, stops.Count);

            // Kiểm tra tính tuần tự không quay xe (No Loopback):
            // Các điểm dừng đầu tiên (Stop 1, Stop 2) phải thuộc Hóc Môn (SortOrder 10)
            // Các điểm dừng tiếp theo (Stop 3, Stop 4) phải thuộc Củ Chi (SortOrder 20)
            Assert.Equal(1, stops[0].Order.DeliveryZone!.SortOrder);
            Assert.Equal(1, stops[1].Order.DeliveryZone!.SortOrder);
            Assert.Equal(2, stops[2].Order.DeliveryZone!.SortOrder);
            Assert.Equal(2, stops[3].Order.DeliveryZone!.SortOrder);
        }

        [Fact]
        public async Task Test4_NonCodOrder_HasZeroExpectedCodAmount()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Evening";

            // Đơn COD
            var codOrder = new Order { Id = 301, OrderCode = "ORD-COD", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 500000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            // Đơn đã thanh toán online qua VNPAY
            var paidOrder = new Order { Id = 302, OrderCode = "ORD-VNPAY", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 800000, PaymentMethod = "VNPAY", CreatedAt = DateTime.Now };

            _context.Orders.AddRange(codOrder, paidOrder);
            await _context.SaveChangesAsync();

            var result = await _deliveryService.AutoBatchTripsAsync(targetDate, targetShift, createdByUserId: 1);
            Assert.True(result.Success);

            var trip = await _context.DeliveryTrips
                .Include(t => t.Stops)
                .FirstAsync(t => t.Id == result.CreatedTrips[0].Id);

            var stopCod = trip.Stops.First(s => s.OrderId == 301);
            var stopPaid = trip.Stops.First(s => s.OrderId == 302);

            Assert.Equal(500000, stopCod.CodExpectedAmount);
            Assert.Equal(0, stopPaid.CodExpectedAmount); // Không thu COD với đơn đã thanh toán trước
            Assert.Equal(500000, trip.TotalCodExpected); // Tổng COD kỳ vọng của chuyến chỉ là 500k
        }

        [Fact]
        public async Task Test5_PreventAssigningOrderToMultipleActiveTrips()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Morning";

            var order = new Order { Id = 401, OrderCode = "ORD-DUP", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 300000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Tạo chuyến 1 chứa đơn 401
            var trip1Res = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 401 }, 1);
            Assert.True(trip1Res.Success);

            // Tạo chuyến 2 cố gắng chứa lại đơn 401 -> Dịch vụ phải từ chối
            var trip2Res = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 401 }, 1);
            Assert.False(trip2Res.Success);
            Assert.Contains("chuyến giao khác", trip2Res.Message);

            // Kiểm tra thêm tầng database: Unique Filtered Index trên DeliveryTripStop WHERE [IsActive] = 1
            // Cố tình insert trực tiếp vào DbContext để kiểm tra DB constraint
            var duplicateStop = new DeliveryTripStop
            {
                DeliveryTripId = trip1Res.Trip!.Id,
                OrderId = 401,
                StopOrder = 99,
                StopStatus = "Assigned",
                IsActive = true
            };
            _context.DeliveryTripStops.Add(duplicateStop);
            await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            {
                await _context.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task Test6_RemoveOrderFromTrip_UpdatesTripTotalsAndResetsOrderStatus()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Afternoon";

            var ord1 = new Order { Id = 501, OrderCode = "ORD-REM-1", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 200000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var ord2 = new Order { Id = 502, OrderCode = "ORD-REM-2", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 350000, PaymentMethod = "COD", CreatedAt = DateTime.Now };

            _context.Orders.AddRange(ord1, ord2);
            await _context.SaveChangesAsync();

            var tripRes = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 501, 502 }, 1);
            Assert.True(tripRes.Success);
            int tripId = tripRes.Trip!.Id;

            // Xóa ord1 khỏi chuyến
            var removeRes = await _deliveryService.RemoveOrderFromTripAsync(tripId, 501, "Khách yêu cầu dời ngày");
            Assert.True(removeRes.Success);

            // Kiểm tra chuyến đã cập nhật số lượng và COD
            var tripAfter = await _context.DeliveryTrips.FindAsync(tripId);
            Assert.Equal(1, tripAfter!.TotalOrders);
            Assert.Equal(350000, tripAfter.TotalCodExpected);

            // Kiểm tra điểm dừng của ord1 đã bị hủy kích hoạt (IsActive = false)
            var stop1 = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 501 && s.DeliveryTripId == tripId);
            Assert.False(stop1.IsActive);
            Assert.Equal("Removed", stop1.StopStatus);

            // Đơn ord1 được hoàn lại trạng thái sẵn sàng giao
            var ord1After = await _context.Orders.FindAsync(501);
            Assert.Equal("Ready", ord1After!.DeliveryStatus);
        }

        [Fact]
        public async Task Test7_DeliveryFailure_RequiresReason_AndExceedingLimitSetsPendingResolution()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Morning";

            var ord = new Order { Id = 601, OrderCode = "ORD-FAIL", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 500000, PaymentMethod = "COD", FailedDeliveryAttempts = 0, CreatedAt = DateTime.Now };
            _context.Orders.Add(ord);
            await _context.SaveChangesAsync();

            var tripRes = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 601 }, 1);
            var stop = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 601);

            // Lần 1: Báo thất bại nhưng KHÔNG nhập lý do -> Bị từ chối
            var failNoReason = await _deliveryService.UpdateStopStatusAsync(stop.Id, "Failed", null, "", null, currentUserId: 3);
            Assert.False(failNoReason.Success);
            Assert.Contains("lý do giao hàng thất bại", failNoReason.Message);

            // Lần 1: Báo thất bại có lý do hợp lệ -> Thành công, số lần thất bại = 1
            var fail1 = await _deliveryService.UpdateStopStatusAsync(stop.Id, "Failed", null, "Khách không nghe máy lần 1", null, currentUserId: 3);
            Assert.True(fail1.Success);

            var ordAfter1 = await _context.Orders.FindAsync(601);
            Assert.Equal(1, ordAfter1!.FailedDeliveryAttempts);
            Assert.Equal("Ready", ordAfter1.DeliveryStatus);

            // Giả lập lần giao thứ 2 (đã có 1 lần thất bại trước đó, và giờ thất bại tiếp)
            // Điểm dừng 1 đã xong, ta kích hoạt điểm dừng thứ 2
            stop.IsActive = false;
            await _context.SaveChangesAsync();

            var trip2Res = await _deliveryService.CreateManualTripAsync(targetDate.AddDays(1), targetShift, "HCM_CEN", 3, new List<int> { 601 }, 1);
            var stop2 = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 601 && s.DeliveryTripId == trip2Res.Trip!.Id);

            // Lần 2: Báo thất bại tiếp (đạt ngưỡng tối đa 2 lần)
            var fail2 = await _deliveryService.UpdateStopStatusAsync(stop2.Id, "Failed", null, "Khách đổi ý không muốn nhận hàng", null, currentUserId: 3);
            Assert.True(fail2.Success);

            var ordAfter2 = await _context.Orders.FindAsync(601);
            Assert.Equal(2, ordAfter2!.FailedDeliveryAttempts);
            // Vượt / đạt ngưỡng tối đa -> chuyển sang Chờ xử lý (PendingResolution)
            Assert.Equal("PendingResolution", ordAfter2.DeliveryStatus);
        }

        [Fact]
        public async Task Test8_TripSettlement_AndDailyReconciliation_LocksAfterApproval()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Morning";

            var ord = new Order { Id = 701, OrderCode = "ORD-SETTLE", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "ReadyForDelivery", OrderStatus = "Processing", TotalAmount = 450000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var orderDetail = new OrderDetail { Id = 701, OrderId = 701, VariantId = 1, Quantity = 1, UnitPrice = 450000 };
            _context.Orders.Add(ord);
            _context.OrderDetails.Add(orderDetail);
            await _context.SaveChangesAsync();

            var tripRes = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 701 }, 1);
            int tripId = tripRes.Trip!.Id;

            // Bắt đầu chuyến
            await _deliveryService.StartTripAsync(tripId, 3);

            // Shipper giao thành công và thu đủ 450,000 VND
            var stop = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 701);
            await _deliveryService.UpdateStopStatusAsync(stop.Id, "Delivered", 450000, null, "Giao đủ", currentUserId: 3);

            // Tầng 1: Quyết toán chuyến giữa Shipper và Thu ngân
            // Thu ngân nhận 450,000 VND -> Không lệch
            var settleRes = await _deliveryService.SettleTripCodAsync(tripId, cashierUserId: 5, receivedAmount: 450000, notes: "Khớp tiền");
            Assert.True(settleRes.Success);

            var tripAfterSettle = await _context.DeliveryTrips.FindAsync(tripId);
            Assert.Equal("Settled", tripAfterSettle!.SettlementStatus);
            Assert.Equal(0, tripAfterSettle.SettlementDifference);

            // Tầng 2: Thu ngân lập bảng đối soát cuối ngày gửi Admin
            var reconRes = await _deliveryService.CreateDailyReconciliationAsync(targetDate, cashierUserId: 5, totalCashSubmitted: 450000, differenceReason: null);
            Assert.True(reconRes.Success);
            Assert.NotNull(reconRes.Reconciliation);
            Assert.Equal(450000, reconRes.Reconciliation!.TotalCodReceived);
            Assert.Equal("Draft", reconRes.Reconciliation.Status);

            // Admin duyệt bảng đối soát cuối ngày
            var approveRes = await _deliveryService.ApproveDailyReconciliationAsync(reconRes.Reconciliation.Id, adminUserId: 1);
            Assert.True(approveRes.Success);

            var reconApproved = await _context.DailyReconciliations.FindAsync(reconRes.Reconciliation.Id);
            Assert.Equal("Approved", reconApproved!.Status);

            // Cố gắng duyệt lại một bảng đã duyệt -> Phải bị từ chối
            var duplicateApprove = await _deliveryService.ApproveDailyReconciliationAsync(reconApproved.Id, adminUserId: 1);
            Assert.False(duplicateApprove.Success);
            Assert.Contains("đã được phê duyệt", duplicateApprove.Message);
        }

        [Fact]
        public async Task Test9_DetectZoneAsync_RecognizesNewWardCommuneAndTraditionalNames()
        {
            // 1. Địa chỉ viết theo cách mới sau 2025 (chỉ có Phường/Xã + TP, không ghi Quận/Huyện cũ)
            var zoneNewWard1 = await _deliveryService.DetectZoneAsync("123 Nguyễn Huệ, Phường Sài Gòn, TP.HCM");
            Assert.NotNull(zoneNewWard1);
            Assert.Equal("HCM_CEN", zoneNewWard1!.ClusterCode);

            var zoneNewCommune = await _deliveryService.DetectZoneAsync("45 Quốc lộ 22, Xã Bà Điểm, TP.HCM");
            Assert.NotNull(zoneNewCommune);
            Assert.Equal("HCM_NW", zoneNewCommune!.ClusterCode);

            var zoneNewWardEast = await _deliveryService.DetectZoneAsync("88 Võ Văn Ngân, Phường Hiệp Phú, TP.HCM");
            Assert.NotNull(zoneNewWardEast);
            Assert.Equal("HCM_EST", zoneNewWardEast!.ClusterCode);

            var zoneDalatWard = await _deliveryService.DetectZoneAsync("10 Trần Phú, Phường Xuân Hương, Tỉnh Lâm Đồng");
            Assert.NotNull(zoneDalatWard);
            Assert.Equal("LD_DALAT", zoneDalatWard!.ClusterCode);

            // 2. Địa chỉ viết theo tên khu vực quen dùng
            var zoneTraditional = await _deliveryService.DetectZoneAsync("99 Quang Trung, Gò Vấp, TP.HCM");
            Assert.NotNull(zoneTraditional);
            Assert.Equal("Gò Vấp", zoneTraditional!.ZoneName);
        }

        [Fact]
        public async Task Test10_CancelOrderWhenInActiveTrip_RemovesStopAndUpdatesTripTotals()
        {
            var targetDate = new DateTime(2026, 10, 16);
            string targetShift = "Morning";

            var ord1 = new Order { Id = 801, OrderCode = "ORD-CANCEL-1", UserId = 1, ShippingAddress = "Quận 1", DeliveryZoneId = 7, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "Ready", OrderStatus = "Confirmed", TotalAmount = 300000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            var ord2 = new Order { Id = 802, OrderCode = "ORD-CANCEL-2", UserId = 1, ShippingAddress = "Quận 3", DeliveryZoneId = 8, ExpectedDeliveryDate = targetDate, DeliveryShift = targetShift, DeliveryStatus = "Ready", OrderStatus = "Confirmed", TotalAmount = 500000, PaymentMethod = "COD", CreatedAt = DateTime.Now };
            _context.Orders.AddRange(ord1, ord2);
            await _context.SaveChangesAsync();

            var tripRes = await _deliveryService.CreateManualTripAsync(targetDate, targetShift, "HCM_CEN", 3, new List<int> { 801, 802 }, 1);
            Assert.True(tripRes.Success);
            int tripId = tripRes.Trip!.Id;

            // Khi đơn 801 bị hủy lúc đang nằm trong chuyến giao
            var cancelRes = await _deliveryService.CancelOrderAndRemoveFromTripAsync(801, "Khách hàng yêu cầu hủy đơn");
            Assert.True(cancelRes.Success);

            // Kiểm tra trạng thái đơn chuyển sang Cancelled
            var cancelledOrder = await _context.Orders.FindAsync(801);
            Assert.Equal("Cancelled", cancelledOrder!.OrderStatus);
            Assert.Equal("Cancelled", cancelledOrder.DeliveryStatus);

            // Kiểm tra điểm dừng của đơn 801 đã được gỡ khỏi chuyến (IsActive = false, StopStatus = Removed)
            var stop801 = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 801 && s.DeliveryTripId == tripId);
            Assert.False(stop801.IsActive);
            Assert.Equal("Removed", stop801.StopStatus);

            // Kiểm tra tổng đơn và tổng COD của chuyến đã tự động cập nhật giảm đi đơn 801
            var updatedTrip = await _context.DeliveryTrips.FindAsync(tripId);
            Assert.Equal(1, updatedTrip!.TotalOrders);
            Assert.Equal(500000, updatedTrip.TotalCodExpected);
        }

        [Fact]
        public async Task Test11_ConcurrentOrderAssignment_OnlyOneSucceeds()
        {
            // Sử dụng file SQLite tạm thời để 2 DbContext song song cùng truy cập thực sự vào 1 database
            string tempDbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"beautyshop_concurrent_{Guid.NewGuid():N}.db");
            string connString = $"Data Source={tempDbPath};Pooling=False";

            try
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connString)
                    .Options;

                using (var setupCtx = new ApplicationDbContext(options))
                {
                    await setupCtx.Database.EnsureCreatedAsync();
                    setupCtx.Roles.AddRange(
                        new Role { Id = 1, Name = "Admin" },
                        new Role { Id = 5, Name = "Shipper" }
                    );
                    setupCtx.Users.AddRange(
                        new User { Id = 1, FullName = "Admin", Email = "a@b.com", PasswordHash = "h", RoleId = 1, IsActive = true },
                        new User { Id = 3, FullName = "Shipper 1", Email = "s1@b.com", PasswordHash = "h", RoleId = 5, IsActive = true }
                    );
                    setupCtx.Orders.Add(new Order
                    {
                        Id = 901,
                        OrderCode = "ORD-CONCURRENT-901",
                        UserId = 1,
                        ShippingAddress = "Quận 1, TP.HCM",
                        DeliveryZoneId = 7,
                        ExpectedDeliveryDate = new DateTime(2026, 10, 16),
                        DeliveryShift = "Morning",
                        DeliveryStatus = "Ready",
                        OrderStatus = "Processing",
                        TotalAmount = 250000,
                        PaymentMethod = "COD",
                        CreatedAt = DateTime.Now
                    });
                    await setupCtx.SaveChangesAsync();
                }

                // Chạy đồng thời 2 tác vụ trên 2 DbContext độc lập cùng gán đơn 901 vào chuyến
                var task1 = Task.Run(async () =>
                {
                    using var ctx1 = new ApplicationDbContext(options);
                    var svc1 = new DeliveryOptimizationService(ctx1);
                    try
                    {
                        return await svc1.CreateManualTripAsync(new DateTime(2026, 10, 16), "Morning", "HCM_CEN", 3, new List<int> { 901 }, 1);
                    }
                    catch (Exception ex)
                    {
                        return (Success: false, Message: ex.Message, Trip: (DeliveryTrip?)null);
                    }
                });

                var task2 = Task.Run(async () =>
                {
                    using var ctx2 = new ApplicationDbContext(options);
                    var svc2 = new DeliveryOptimizationService(ctx2);
                    try
                    {
                        return await svc2.CreateManualTripAsync(new DateTime(2026, 10, 16), "Morning", "HCM_CEN", 3, new List<int> { 901 }, 1);
                    }
                    catch (Exception ex)
                    {
                        return (Success: false, Message: ex.Message, Trip: (DeliveryTrip?)null);
                    }
                });

                var results = await Task.WhenAll(task1, task2);
                int successCount = results.Count(r => r.Success);

                // Chỉ duy nhất 1 thao tác được phép thành công; thao tác còn lại bị chặn bởi kiểm tra hoặc Unique Filtered Index
                Assert.Equal(1, successCount);

                using (var verifyCtx = new ApplicationDbContext(options))
                {
                    int activeStopsForOrder = await verifyCtx.DeliveryTripStops
                        .CountAsync(s => s.OrderId == 901 && s.IsActive);
                    Assert.Equal(1, activeStopsForOrder);
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempDbPath))
                {
                    try { System.IO.File.Delete(tempDbPath); } catch { }
                }
            }
        }
    }
}
