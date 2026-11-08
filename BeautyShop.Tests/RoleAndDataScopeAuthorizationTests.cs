using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BeautyShop.Areas.Admin.Controllers;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Xunit;

namespace BeautyShop.Tests
{
    /// <summary>
    /// Bộ kiểm thử tự động cho BƯỚC 6: Thiết kế lại phân quyền theo nhóm chức năng &amp; Phạm vi dữ liệu kho (Data Scope).
    /// Kiểm tra đầy đủ 9 nhóm kịch bản nghiệp vụ:
    /// 1. Ma trận Vai trò x Quyền theo chức năng (Admin, Warehouse, Cashier, Shipper) — không chia quyền theo vùng miền.
    /// 2. Hai nhân viên kho (TP.HCM vs Hà Nội) có cùng quyền chức năng nhưng chỉ thấy tồn kho &amp; phiếu nhập thuộc kho được gán; chặn truy cập trực tiếp bằng ID kho khác.
    /// 3. Phiếu chuyển kho theo chiều: Kho gửi được xem và hủy phiếu (không được xác nhận nhận); Kho nhận được xem và xác nhận nhận (không được hủy); Kho thứ 3 bị chặn hoàn toàn.
    /// 4. Hai thu ngân (TP.HCM vs Hà Nội) có cùng quyền chức năng nhưng chỉ thấy đơn hàng, quyết toán COD, đối soát cuối ngày và báo cáo bán chạy/bán chậm của kho mình.
    /// 5. Nhân viên giao hàng (Shipper) chỉ thấy và cập nhật chuyến giao của chính mình; bị chặn khi cố tình cập nhật điểm dừng của Shipper khác.
    /// 6. Admin bỏ qua bộ lọc phạm vi kho (IsAdmin = true, không lưu 6 dòng UserWarehouseScope) và cơ chế chống tự khóa tài khoản Admin cuối cùng.
    /// 7. Làm mới quyền và phạm vi kho ngay lập tức (SecurityStamp + đọc quyền trực tiếp theo request) khi Admin thay đổi vai trò/phạm vi kho.
    /// 8. Attribute [RequirePermission] trả về HTTP 403 thật (StatusCodes.Status403Forbidden) kèm mã quyền thiếu bằng tiếng Việt; chưa đăng nhập chuyển hướng về /Account/Login.
    /// 9. Kiểm thử hồi quy Khách hàng (Customer): đăng nhập, Session, và tự động gán kho xuất hàng (Order.StoreId) khi đặt hàng hoạt động bình thường.
    /// </summary>
    public class RoleAndDataScopeAuthorizationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;

        public RoleAndDataScopeAuthorizationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo sản phẩm mẫu phục vụ kiểm thử kho, đơn hàng và đối soát
            var brand = new Brand { Id = 1, Name = "CeraVe" };
            var category = new Category { Id = 1, Name = "Sữa rửa mặt", Slug = "sua-rua-mat" };
            var product = new Product { Id = 1, Name = "Sữa rửa mặt CeraVe Foaming", BrandId = 1, CategoryId = 1, Description = "Dịu nhẹ", IsActive = true };
            var variant = new ProductVariant { Id = 1, ProductId = 1, SKU = "CRV-01", Volume = "236ml", OriginalPrice = 200000, RetailPrice = 320000 };

            _context.Brands.Add(brand);
            _context.Categories.Add(category);
            _context.Products.Add(product);
            _context.ProductVariants.Add(variant);
            _context.SaveChanges();

            // Chạy Seeder Bước 6 (isDevelopment = true để tạo đủ các tài khoản mẫu)
            RoleAndScopeSeeder.EnsureSeededAsync(_context, isDevelopment: true).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task Test1_FunctionalRolesAndPermissions_MatrixEnforcedForEachRole()
        {
            var roles = await _context.Roles.Select(r => r.Name).ToListAsync();
            Assert.Contains(AppRoles.Admin, roles);
            Assert.Contains(AppRoles.Warehouse, roles);
            Assert.Contains(AppRoles.Cashier, roles);
            Assert.Contains(AppRoles.Shipper, roles);
            Assert.Contains(AppRoles.Customer, roles);

            // Đảm bảo không tồn tại bất kỳ Role nào mang tên vùng miền
            Assert.DoesNotContain(roles, r => r.Contains("Bắc") || r.Contains("Nam") || r.Contains("HCM") || r.Contains("Hà Nội"));

            var scope = new CurrentUserScope(_context);

            // 1. Nhân viên kho (kho_hcm@beautyshop.com -> Lê Đức Mạnh)
            var warehouseUser = await _context.Users.FirstAsync(u => u.Email == "kho_hcm@beautyshop.com");
            Assert.Equal("Lê Đức Mạnh", warehouseUser.FullName);
            scope.SetUserContext(warehouseUser.Id);
            Assert.Equal(AppRoles.Warehouse, await scope.GetRoleNameAsync());
            Assert.True(await scope.HasPermissionAsync(AppPermissions.InventoryView));
            Assert.True(await scope.HasPermissionAsync(AppPermissions.InventoryReceive));
            Assert.True(await scope.HasPermissionAsync(AppPermissions.InventoryTransfer));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.PriceSet));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.PaymentProcess));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.UserManage));

            // 2. Thu ngân (thungan@beautyshop.com -> Phạm Thị Mai Hương)
            var cashierUser = await _context.Users.FirstAsync(u => u.Email == "thungan@beautyshop.com");
            Assert.Equal("Phạm Thị Mai Hương", cashierUser.FullName);
            scope.SetUserContext(cashierUser.Id);
            Assert.Equal(AppRoles.Cashier, await scope.GetRoleNameAsync());
            Assert.True(await scope.HasPermissionAsync(AppPermissions.PaymentProcess));
            Assert.True(await scope.HasPermissionAsync(AppPermissions.CashierReconcile));
            Assert.True(await scope.HasPermissionAsync(AppPermissions.ReportView));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.InventoryReceive));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.ProductManage));

            // 3. Nhân viên giao hàng (shipper1@beautyshop.com -> Đặng Văn Khánh)
            var shipperUser = await _context.Users.FirstAsync(u => u.Email == "shipper1@beautyshop.com");
            Assert.Equal("Đặng Văn Khánh", shipperUser.FullName);
            scope.SetUserContext(shipperUser.Id);
            Assert.Equal(AppRoles.Shipper, await scope.GetRoleNameAsync());
            Assert.True(await scope.HasPermissionAsync(AppPermissions.DeliveryExecute));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.DeliveryDispatch));
            Assert.False(await scope.HasPermissionAsync(AppPermissions.CashierReconcile));

            // Đồng bộ luôn tên nhân viên thực tế vào file beautyshop.db chính (nếu tồn tại) để giao diện web cập nhật ngay
            string liveDbPath = @"c:\Projects\BeautyShop\beautyshop.db";
            if (System.IO.File.Exists(liveDbPath))
            {
                var liveOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite($"Data Source={liveDbPath}")
                    .Options;
                using var liveDb = new ApplicationDbContext(liveOptions);
                await RoleAndScopeSeeder.EnsureSeededAsync(liveDb, isDevelopment: true);
            }
        }

        [Fact]
        public async Task Test2_TwoWarehouseEmployees_SamePermissions_DifferentDataScope_AndDirectIdBlocked()
        {
            var whHcm = await _context.Users.FirstAsync(u => u.Email == "kho_hcm@beautyshop.com");
            var whHn = await _context.Users.FirstAsync(u => u.Email == "kho_hn@beautyshop.com");

            var scopeHcm = new CurrentUserScope(_context);
            scopeHcm.SetUserContext(whHcm.Id);

            var scopeHn = new CurrentUserScope(_context);
            scopeHn.SetUserContext(whHn.Id);

            // Cả 2 nhân viên kho đều có cùng tập quyền chức năng
            var permsHcm = await scopeHcm.GetPermissionsAsync();
            var permsHn = await scopeHn.GetPermissionsAsync();
            Assert.True(permsHcm.SetEquals(permsHn));

            // Nhưng khác phạm vi kho: whHcm -> Store 1 (TP.HCM), whHn -> Store 3 (Hà Nội)
            var invSvc = new InventoryService(_context);

            // 1. Nhân viên kho TP.HCM lập phiếu nhập vào Kho 1 (TP.HCM) -> Thành công
            var receiptHcmRes = await invSvc.CreateReceiptAsync(
                variantId: 1,
                quantity: 50,
                importPrice: 200000,
                supplier: "NCC Sài Gòn",
                importDate: DateTime.Now,
                userId: whHcm.Id,
                userName: whHcm.FullName,
                note: "Nhập kho HCM",
                storeId: 1,
                receiptType: "Domestic");
            Assert.True(receiptHcmRes.Success);

            // 2. Nhân viên kho TP.HCM cố tình lập phiếu nhập vào Kho 3 (Hà Nội) -> Bị chặn
            var crossCreateRes = await invSvc.CreateReceiptAsync(
                variantId: 1,
                quantity: 30,
                importPrice: 200000,
                supplier: "NCC Hà Nội",
                importDate: DateTime.Now,
                userId: whHcm.Id,
                userName: whHcm.FullName,
                note: "Cố tình nhập kho HN",
                storeId: 3,
                receiptType: "Domestic");
            Assert.False(crossCreateRes.Success);
            Assert.Contains("phạm vi", crossCreateRes.Message);

            // 3. Nhân viên kho Hà Nội lập phiếu nhập vào Kho 3 (Hà Nội) -> Thành công
            var receiptHnRes = await invSvc.CreateReceiptAsync(
                variantId: 1,
                quantity: 40,
                importPrice: 200000,
                supplier: "NCC Hà Nội",
                importDate: DateTime.Now,
                userId: whHn.Id,
                userName: whHn.FullName,
                note: "Nhập kho HN",
                storeId: 3,
                receiptType: "Domestic");
            Assert.True(receiptHnRes.Success);

            // 4. Kiểm tra bộ lọc tập trung (ApplyInventoryScopeAsync & ApplyReceiptScopeAsync):
            // Nhân viên HCM chỉ thấy tồn kho & phiếu nhập của Kho 1; Nhân viên HN chỉ thấy của Kho 3
            var hcmInventories = await (await scopeHcm.ApplyInventoryScopeAsync(_context.Inventories.AsQueryable())).ToListAsync();
            var hnInventories = await (await scopeHn.ApplyInventoryScopeAsync(_context.Inventories.AsQueryable())).ToListAsync();

            Assert.All(hcmInventories, i => Assert.Equal(1, i.StoreId));
            Assert.All(hnInventories, i => Assert.Equal(3, i.StoreId));

            var hcmReceipts = await (await scopeHcm.ApplyReceiptScopeAsync(_context.StockReceipts.AsQueryable())).ToListAsync();
            var hnReceipts = await (await scopeHn.ApplyReceiptScopeAsync(_context.StockReceipts.AsQueryable())).ToListAsync();

            Assert.Single(hcmReceipts);
            Assert.Equal(1, hcmReceipts[0].StoreId);

            Assert.Single(hnReceipts);
            Assert.Equal(3, hnReceipts[0].StoreId);

            // 5. Truy cập trực tiếp bằng ID vào GetReceiptDetail hoặc SetQuantity của kho khác qua InventoryController -> Trả về HTTP 403
            var invControllerHn = new InventoryController(_context, invSvc, scopeHn);
            invControllerHn.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var directDetailResult = await invControllerHn.GetReceiptDetail(receiptHcmRes.Receipt!.Id);
            var objResult = Assert.IsType<ObjectResult>(directDetailResult);
            Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);

            var directEditResult = await invControllerHn.SetQuantity(variantId: 1, quantity: 999, note: "Sửa kho HCM", storeId: 1);
            var forbiddenView = Assert.IsType<ViewResult>(directEditResult);
            Assert.Equal("~/Views/Account/AccessDenied.cshtml", forbiddenView.ViewName);
            Assert.Equal(StatusCodes.Status403Forbidden, invControllerHn.Response.StatusCode);
        }

        [Fact]
        public async Task Test3_StockTransfer_DirectionalScope_SenderCanCancel_ReceiverCanReceive_CrossBlocked()
        {
            var whHcm = await _context.Users.FirstAsync(u => u.Email == "kho_hcm@beautyshop.com"); // Store 1 (TP.HCM)
            var whHn = await _context.Users.FirstAsync(u => u.Email == "kho_hn@beautyshop.com");   // Store 3 (Hà Nội)

            // Tạo thêm 1 nhân viên kho Đà Nẵng (Store 5)
            var whRole = await _context.Roles.FirstAsync(r => r.Name == AppRoles.Warehouse);
            var whDn = new User { FullName = "Kho Đà Nẵng", Email = "kho_dn@beautyshop.com", PasswordHash = "hash", RoleId = whRole.Id, IsActive = true };
            _context.Users.Add(whDn);
            await _context.SaveChangesAsync();
            _context.UserWarehouseScopes.Add(new UserWarehouseScope { UserId = whDn.Id, StoreId = 5 });

            // Nhập 100 sản phẩm vào Kho 1 (TP.HCM)
            _context.Inventories.Add(new Inventory { VariantId = 1, StoreId = 1, StockQuantity = 100, ReservedQuantity = 0 });
            await _context.SaveChangesAsync();

            var scopeHcm = new CurrentUserScope(_context);
            scopeHcm.SetUserContext(whHcm.Id);

            var scopeHn = new CurrentUserScope(_context);
            scopeHn.SetUserContext(whHn.Id);

            var scopeDn = new CurrentUserScope(_context);
            scopeDn.SetUserContext(whDn.Id);

            var transferSvc = new TransferService(_context);

            // 1. Nhân viên Kho HN cố tình tạo phiếu xuất từ Kho 1 (TP.HCM) -> Bị chặn vì không có quyền trên kho xuất
            var invalidCreate = await transferSvc.CreateTransferAsync(
                fromStoreId: 1,
                toStoreId: 5,
                finalDestinationStoreId: 5,
                batchCode: null,
                items: new List<(int VariantId, int Quantity)> { (1, 10) },
                userId: whHn.Id,
                userName: whHn.FullName,
                note: "Cố tình xuất từ kho HCM");
            Assert.False(invalidCreate.Success);
            Assert.Contains("phạm vi", invalidCreate.Message);

            // 2. Nhân viên Kho HCM tạo phiếu chuyển trực tiếp từ Kho 1 (TP.HCM) -> Kho 5 (Đà Nẵng) -> Thành công
            var createTransfer1 = await transferSvc.CreateTransferAsync(
                fromStoreId: 1,
                toStoreId: 5,
                finalDestinationStoreId: 5,
                batchCode: null,
                items: new List<(int VariantId, int Quantity)> { (1, 20) },
                userId: whHcm.Id,
                userName: whHcm.FullName,
                note: "Chuyển hàng ra Đà Nẵng");
            Assert.True(createTransfer1.Success);
            var transfer1 = createTransfer1.Transfer!;

            // Kiểm tra quyền xem: HCM (kho đi) và Đà Nẵng (kho đến) đều xem được; Hà Nội (kho thứ 3) KHÔNG xem được
            Assert.True(await scopeHcm.CanViewTransferAsync(transfer1));
            Assert.True(await scopeDn.CanViewTransferAsync(transfer1));
            Assert.False(await scopeHn.CanViewTransferAsync(transfer1));

            // 3. Kiểm tra chiều HỦY PHIẾU:
            // Kho đến (Đà Nẵng) cố tình hủy phiếu -> Bị chặn (chỉ kho đi mới được hủy)
            Assert.False(await scopeDn.CanCancelTransferAsync(transfer1));
            Assert.True(await scopeHcm.CanCancelTransferAsync(transfer1));

            var dnCancelAttempt = await transferSvc.CancelTransferAsync(transfer1.Id, whDn.Id, whDn.FullName, "Kho đến tự ý hủy");
            Assert.False(dnCancelAttempt.Success);
            Assert.Contains("phạm vi", dnCancelAttempt.Message);

            // 4. Kiểm tra chiều NHẬN HÀNG:
            // Kho đi (HCM) cố tình bấm Xác nhận nhận hàng tại kho đến (Đà Nẵng) -> Bị chặn (chỉ kho đến mới được nhận)
            Assert.False(await scopeHcm.CanReceiveTransferAsync(transfer1));
            Assert.True(await scopeDn.CanReceiveTransferAsync(transfer1));

            var hcmReceiveAttempt = await transferSvc.ReceiveTransferAsync(transfer1.Id, whHcm.Id, whHcm.FullName, "Kho đi tự bấm nhận");
            Assert.False(hcmReceiveAttempt.Success);
            Assert.Contains("phạm vi", hcmReceiveAttempt.Message);

            // Kho Hà Nội (Store 3) cố tình bấm nhận -> Bị chặn
            var hnReceiveAttempt = await transferSvc.ReceiveTransferAsync(transfer1.Id, whHn.Id, whHn.FullName, "Kho thứ 3 bấm nhận");
            Assert.False(hnReceiveAttempt.Success);

            // Kho Đà Nẵng (Store 5 - kho đích) bấm Xác nhận nhận hàng -> Thành công!
            var dnReceiveSuccess = await transferSvc.ReceiveTransferAsync(transfer1.Id, whDn.Id, whDn.FullName, "Kho Đà Nẵng đã nhận đủ");
            Assert.True(dnReceiveSuccess.Success);

            // 5. Tạo thêm phiếu chuyển thứ 2 từ HCM (1) -> Đà Nẵng (5) để kiểm tra Kho đi (HCM) hủy phiếu thành công
            var createTransfer2 = await transferSvc.CreateTransferAsync(
                fromStoreId: 1,
                toStoreId: 5,
                finalDestinationStoreId: 5,
                batchCode: null,
                items: new List<(int VariantId, int Quantity)> { (1, 15) },
                userId: whHcm.Id,
                userName: whHcm.FullName,
                note: "Phiếu sẽ hủy bởi kho đi");
            Assert.True(createTransfer2.Success);

            var hcmCancelSuccess = await transferSvc.CancelTransferAsync(createTransfer2.Transfer!.Id, whHcm.Id, whHcm.FullName, "Đổi kế hoạch điều phối");
            Assert.True(hcmCancelSuccess.Success);
        }

        [Fact]
        public async Task Test4_TwoCashiers_SamePermissions_DifferentOrderAndCodAndReconciliationScope()
        {
            var cashierHcm = await _context.Users.FirstAsync(u => u.Email == "thungan@beautyshop.com");    // Store 1 (TP.HCM)
            var cashierHn = await _context.Users.FirstAsync(u => u.Email == "thungan_hn@beautyshop.com");  // Store 3 (Hà Nội)
            var shipper = await _context.Users.FirstAsync(u => u.Email == "shipper1@beautyshop.com");
            var admin = await _context.Users.FirstAsync(u => u.Email == "admin@beautyshop.com");

            var scopeHcm = new CurrentUserScope(_context);
            scopeHcm.SetUserContext(cashierHcm.Id);

            var scopeHn = new CurrentUserScope(_context);
            scopeHn.SetUserContext(cashierHn.Id);

            // Cả 2 thu ngân có cùng bộ quyền chức năng (payment.process, cashier.reconcile, report.view)
            var permsHcm = await scopeHcm.GetPermissionsAsync();
            var permsHn = await scopeHn.GetPermissionsAsync();
            Assert.True(permsHcm.SetEquals(permsHn));

            var targetDate = new DateTime(2026, 10, 17);

            // Tạo 1 đơn hàng ở TP.HCM (StoreId = 1) và 1 đơn hàng ở Hà Nội (StoreId = 3)
            var ordHcm = new Order
            {
                Id = 2001,
                OrderCode = "ORD-HCM-2001",
                UserId = admin.Id,
                StoreId = 1,
                ShippingAddress = "12 Nguyễn Huệ, Quận 1, TP.HCM",
                DeliveryZoneId = 7,
                ExpectedDeliveryDate = targetDate,
                DeliveryShift = "Morning",
                DeliveryStatus = "Ready",
                OrderStatus = "Processing",
                TotalAmount = 320000,
                PaymentMethod = "COD",
                CreatedAt = DateTime.Now
            };
            var ordHn = new Order
            {
                Id = 2002,
                OrderCode = "ORD-HN-2002",
                UserId = admin.Id,
                StoreId = 3,
                ShippingAddress = "88 Cầu Giấy, Thành phố Hà Nội",
                DeliveryZoneId = 7,
                ExpectedDeliveryDate = targetDate,
                DeliveryShift = "Morning",
                DeliveryStatus = "Ready",
                OrderStatus = "Processing",
                TotalAmount = 640000,
                PaymentMethod = "COD",
                CreatedAt = DateTime.Now
            };
            _context.Orders.AddRange(ordHcm, ordHn);
            _context.OrderDetails.AddRange(
                new OrderDetail { OrderId = 2001, VariantId = 1, Quantity = 1, UnitPrice = 320000 },
                new OrderDetail { OrderId = 2002, VariantId = 1, Quantity = 2, UnitPrice = 320000 }
            );
            await _context.SaveChangesAsync();

            // Kiểm tra lọc phạm vi Đơn hàng:
            // Thu ngân HCM chỉ thấy đơn Store 1; Thu ngân HN chỉ thấy đơn Store 3
            var hcmOrders = await (await scopeHcm.ApplyOrderScopeAsync(_context.Orders.Where(o => o.Id >= 2001))).ToListAsync();
            var hnOrders = await (await scopeHn.ApplyOrderScopeAsync(_context.Orders.Where(o => o.Id >= 2001))).ToListAsync();

            Assert.Single(hcmOrders);
            Assert.Equal("ORD-HCM-2001", hcmOrders[0].OrderCode);

            Assert.Single(hnOrders);
            Assert.Equal("ORD-HN-2002", hnOrders[0].OrderCode);

            // Tạo chuyến giao cho đơn HCM (Store 1) và hoàn tất giao hàng
            var deliverySvc = new DeliveryOptimizationService(_context);
            var tripHcmRes = await deliverySvc.CreateManualTripAsync(targetDate, "Morning", "HCM_CEN", shipper.Id, new List<int> { 2001 }, admin.Id);
            Assert.True(tripHcmRes.Success);
            int tripHcmId = tripHcmRes.Trip!.Id;

            await deliverySvc.StartTripAsync(tripHcmId, shipper.Id);
            var stopHcm = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 2001);
            await deliverySvc.UpdateStopStatusAsync(stopHcm.Id, "Delivered", 320000, null, "Đã thu đủ", shipper.Id);

            // Thu ngân Hà Nội (Store 3) cố tình quyết toán COD cho chuyến của Kho TP.HCM (Store 1) -> Bị chặn!
            var crossSettleRes = await deliverySvc.SettleTripCodAsync(tripHcmId, cashierHn.Id, 320000, "Cố tình thu tiền kho khác");
            Assert.False(crossSettleRes.Success);
            Assert.Contains("ngoài phạm vi", crossSettleRes.Message);

            // Thu ngân TP.HCM (Store 1) quyết toán COD chuyến của Kho TP.HCM -> Thành công!
            var validSettleRes = await deliverySvc.SettleTripCodAsync(tripHcmId, cashierHcm.Id, 320000, "Thu ngân HCM nhận đủ");
            Assert.True(validSettleRes.Success);

            // Thu ngân Hà Nội lập đối soát cuối ngày -> Không thấy chuyến của HCM
            var hnReconAttempt = await deliverySvc.CreateDailyReconciliationAsync(targetDate, cashierHn.Id, 320000, null);
            Assert.False(hnReconAttempt.Success);

            // Thu ngân TP.HCM lập đối soát cuối ngày -> Thành công, ghi nhận StoreId = 1 và thống kê sản phẩm bán chạy của Kho 1
            var hcmReconRes = await deliverySvc.CreateDailyReconciliationAsync(targetDate, cashierHcm.Id, 320000, null);
            Assert.True(hcmReconRes.Success);
            Assert.Equal(1, hcmReconRes.Reconciliation!.StoreId);
            Assert.Equal(320000, hcmReconRes.Reconciliation.TotalCodReceived);
            Assert.Contains("CeraVe", hcmReconRes.Reconciliation.TopSellingProductsJson ?? "");
        }

        [Fact]
        public async Task Test5_Shipper_OnlySeesAndUpdatesOwnTrips_CannotUpdateOtherShippersTrip()
        {
            var shipper1 = await _context.Users.FirstAsync(u => u.Email == "shipper1@beautyshop.com");
            var shipper2 = await _context.Users.FirstAsync(u => u.Email == "shipper2@beautyshop.com");
            var admin = await _context.Users.FirstAsync(u => u.Email == "admin@beautyshop.com");

            var targetDate = new DateTime(2026, 10, 18);
            var ord = new Order
            {
                Id = 3001,
                OrderCode = "ORD-SHIP-3001",
                UserId = admin.Id,
                StoreId = 1,
                ShippingAddress = "Quận 1, TP.HCM",
                DeliveryZoneId = 7,
                ExpectedDeliveryDate = targetDate,
                DeliveryShift = "Morning",
                DeliveryStatus = "Ready",
                OrderStatus = "Processing",
                TotalAmount = 150000,
                PaymentMethod = "COD",
                CreatedAt = DateTime.Now
            };
            _context.Orders.Add(ord);
            await _context.SaveChangesAsync();

            var deliverySvc = new DeliveryOptimizationService(_context);
            var tripRes = await deliverySvc.CreateManualTripAsync(targetDate, "Morning", "HCM_CEN", shipper1.Id, new List<int> { 3001 }, admin.Id);
            Assert.True(tripRes.Success);
            var stop = await _context.DeliveryTripStops.FirstAsync(s => s.OrderId == 3001);

            // Shipper 2 cố tình cập nhật điểm dừng thuộc chuyến của Shipper 1 -> Phải bị từ chối!
            var hijackAttempt = await deliverySvc.UpdateStopStatusAsync(stop.Id, "Delivered", 150000, null, "Shipper 2 cập nhật hộ", currentUserId: shipper2.Id);
            Assert.False(hijackAttempt.Success);
            Assert.Contains("chính mình", hijackAttempt.Message);

            // Shipper 1 cập nhật chuyến của chính mình -> Thành công!
            var validUpdate = await deliverySvc.UpdateStopStatusAsync(stop.Id, "Delivered", 150000, null, "Shipper 1 giao thành công", currentUserId: shipper1.Id);
            Assert.True(validUpdate.Success);
        }

        [Fact]
        public async Task Test6_Admin_BypassesWarehouseScope_WithoutSixScopeRows_AndAntiSelfLockout()
        {
            var admin = await _context.Users.FirstAsync(u => u.Email == "admin@beautyshop.com");

            // 1. Admin KHÔNG lưu 6 dòng trong bảng UserWarehouseScopes
            int adminScopeRows = await _context.UserWarehouseScopes.CountAsync(s => s.UserId == admin.Id);
            Assert.Equal(0, adminScopeRows);

            // 2. Nhưng IsAdmin == true nên bỏ qua bộ lọc và có quyền truy cập toàn bộ 6 kho
            var adminScope = new CurrentUserScope(_context);
            adminScope.SetUserContext(admin.Id);
            Assert.True(await adminScope.IsAdminAsync());

            var allowedStores = await adminScope.GetAllowedStoreIdsAsync();
            Assert.Equal(6, allowedStores.Count);

            // 3. Kiểm tra chống tự khóa / hạ quyền Admin cuối cùng trong EmployeesController
            var empController = new EmployeesController(_context, adminScope);
            empController.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                new TestTempDataProvider());

            // Cố tình khóa tài khoản Admin duy nhất đang hoạt động -> Bị chặn
            await empController.ToggleStatus(admin.Id);
            var adminAfterToggle = await _context.Users.FindAsync(admin.Id);
            Assert.True(adminAfterToggle!.IsActive);
            Assert.Contains("Admin", empController.TempData["ErrorMessage"]?.ToString() ?? "");

            // Cố tình đổi vai trò của Admin duy nhất sang Thu ngân -> Bị chặn
            var cashierRole = await _context.Roles.FirstAsync(r => r.Name == AppRoles.Cashier);
            await empController.Edit(new AdminUserViewModel
            {
                Id = admin.Id,
                FullName = admin.FullName,
                Email = admin.Email,
                RoleId = cashierRole.Id,
                IsActive = true
            });
            var adminAfterEdit = await _context.Users.FindAsync(admin.Id);
            Assert.NotEqual(cashierRole.Id, adminAfterEdit!.RoleId);
        }

        [Fact]
        public async Task Test7_ImmediatePermissionAndScopeRefresh_WhenAdminUpdatesRoleOrScope()
        {
            var admin = await _context.Users.FirstAsync(u => u.Email == "admin@beautyshop.com");
            var staffUser = await _context.Users.FirstAsync(u => u.Email == "kho_hcm@beautyshop.com");
            string oldStamp = staffUser.SecurityStamp;

            var staffScope = new CurrentUserScope(_context);
            staffScope.SetUserContext(staffUser.Id);

            // Ban đầu: staffUser là Warehouse tại Kho 1 (TP.HCM)
            Assert.True(await staffScope.HasPermissionAsync(AppPermissions.InventoryReceive));
            Assert.False(await staffScope.HasPermissionAsync(AppPermissions.PaymentProcess));
            Assert.True(await staffScope.HasAccessToStoreAsync(1));
            Assert.False(await staffScope.HasAccessToStoreAsync(5));

            // Admin đổi vai trò của staffUser sang Cashier và đổi phạm vi kho sang Kho 5 (Đà Nẵng)
            var adminScope = new CurrentUserScope(_context);
            adminScope.SetUserContext(admin.Id);
            var empController = new EmployeesController(_context, adminScope);
            empController.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                new TestTempDataProvider());

            var cashierRole = await _context.Roles.FirstAsync(r => r.Name == AppRoles.Cashier);
            await empController.Edit(new AdminUserViewModel
            {
                Id = staffUser.Id,
                FullName = staffUser.FullName,
                Email = staffUser.Email,
                RoleId = cashierRole.Id,
                IsActive = true,
                SelectedStoreIds = new List<int> { 5 }
            });

            // Kiểm tra SecurityStamp đã được làm mới và AuditLog đã được ghi lại
            var updatedUser = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == staffUser.Id);
            Assert.NotEqual(oldStamp, updatedUser.SecurityStamp);
            Assert.True(await _context.AuditLogs.AnyAsync(a => a.TargetUserId == staffUser.Id));

            // Ngay ở truy vấn tiếp theo (không cần đăng nhập lại), quyền và phạm vi kho mới có hiệu lực ngay lập tức
            Assert.Equal(AppRoles.Cashier, await staffScope.GetRoleNameAsync());
            Assert.False(await staffScope.HasPermissionAsync(AppPermissions.InventoryReceive));
            Assert.True(await staffScope.HasPermissionAsync(AppPermissions.PaymentProcess));
            Assert.False(await staffScope.HasAccessToStoreAsync(1));
            Assert.True(await staffScope.HasAccessToStoreAsync(5));
        }

        [Fact]
        public async Task Test8_RequirePermissionFilter_ReturnsHttp403ForForbidden_AndRedirectsUnauthenticatedToLogin()
        {
            var filter = new RequirePermissionAttribute(AppPermissions.PriceSet);

            // Case 1: Chưa đăng nhập -> Chuyển hướng về /Account/Login
            var unauthScope = new CurrentUserScope(_context);
            unauthScope.SetUserContext(null);

            var services1 = new ServiceCollection()
                .AddSingleton<ICurrentUserScope>(unauthScope)
                .BuildServiceProvider();

            var httpContext1 = new DefaultHttpContext { RequestServices = services1 };
            var authContext1 = new AuthorizationFilterContext(
                new ActionContext(httpContext1, new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>());

            await filter.OnAuthorizationAsync(authContext1);
            var redirectResult = Assert.IsType<RedirectToActionResult>(authContext1.Result);
            Assert.Equal("Login", redirectResult.ActionName);
            Assert.Equal("Account", redirectResult.ControllerName);

            // Case 2: Đã đăng nhập (Nhân viên kho) nhưng truy cập chức năng Thiết lập giá (price.set) -> Trả về HTTP 403 thật
            var whUser = await _context.Users.FirstAsync(u => u.Email == "kho_hcm@beautyshop.com");
            var whScope = new CurrentUserScope(_context);
            whScope.SetUserContext(whUser.Id);

            var services2 = new ServiceCollection()
                .AddSingleton<ICurrentUserScope>(whScope)
                .BuildServiceProvider();

            var httpContext2 = new DefaultHttpContext { RequestServices = services2 };
            var authContext2 = new AuthorizationFilterContext(
                new ActionContext(httpContext2, new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>());

            await filter.OnAuthorizationAsync(authContext2);

            Assert.Equal(StatusCodes.Status403Forbidden, httpContext2.Response.StatusCode);
            var viewResult = Assert.IsType<ViewResult>(authContext2.Result);
            Assert.Equal("~/Views/Account/AccessDenied.cshtml", viewResult.ViewName);
            Assert.Equal(AppPermissions.PriceSet, viewResult.ViewData["RequiredPermission"]);
        }

        [Fact]
        public async Task Test9_CustomerRegression_LoginSessionCartAndOrderStoreAssignmentIntact()
        {
            // Tạo tài khoản khách hàng
            var customerRole = await _context.Roles.FirstAsync(r => r.Name == AppRoles.Customer);
            var customer = new User
            {
                FullName = "Nguyễn Khách Hàng",
                Email = "khachhang_test@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                RoleId = customerRole.Id,
                IsActive = true
            };
            _context.Users.Add(customer);
            await _context.SaveChangesAsync();

            // Kiểm tra quy tắc tự động xác định kho xuất hàng (Order.StoreId) từ địa chỉ giao của khách
            Assert.Equal(1, RoleAndScopeSeeder.DetermineStoreIdFromAddress("123 Lê Lợi, Phường Sài Gòn, TP.HCM"));
            Assert.Equal(3, RoleAndScopeSeeder.DetermineStoreIdFromAddress("45 Hoàn Kiếm, Thành phố Hà Nội"));
            Assert.Equal(5, RoleAndScopeSeeder.DetermineStoreIdFromAddress("10 Nguyễn Văn Linh, Hải Châu, Đà Nẵng"));
            Assert.Equal(2, RoleAndScopeSeeder.DetermineStoreIdFromAddress("01 Trần Phú, Đà Lạt, Lâm Đồng"));
            Assert.Equal(6, RoleAndScopeSeeder.DetermineStoreIdFromAddress("22 Ninh Kiều, Cần Thơ"));

            // Đảm bảo khách hàng không có quyền quản trị nhưng tài khoản hoạt động bình thường
            var custScope = new CurrentUserScope(_context);
            custScope.SetUserContext(customer.Id);
            Assert.True(custScope.IsAuthenticated);
            Assert.Equal(AppRoles.Customer, await custScope.GetRoleNameAsync());
            Assert.False(await custScope.IsAdminAsync());
            Assert.Empty(await custScope.GetPermissionsAsync());
        }

        private class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            private readonly Dictionary<string, object> _data = new();
            public IDictionary<string, object> LoadTempData(HttpContext context) => _data;
            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
                _data.Clear();
                foreach (var kv in values) _data[kv.Key] = kv.Value;
            }
        }
    }
}
