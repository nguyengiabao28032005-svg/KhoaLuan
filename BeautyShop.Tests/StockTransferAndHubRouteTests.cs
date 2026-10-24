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
    /// Bộ kiểm thử tự động cho BƯỚC 4: Luân chuyển kho qua điểm trung chuyển (Hub)
    /// Kiểm tra 100% các quy tắc nghiệp vụ:
    /// 1. Chuyển hợp lệ trực tiếp giữa hai kho có tuyến (TP.HCM -> Cần Thơ) -> Thành công.
    /// 2. Tuyến qua Hub: đi thẳng bị chặn, bắt buộc chặng 1 đến Hub Đà Nẵng -> Bị từ chối.
    /// 3. Tuyến qua Hub: đi qua Hub thành công trọn vẹn 2 chặng (Hà Nội -> Đà Nẵng -> Lâm Đồng).
    /// 4. Chuyển về chính kho gửi -> Bị chặn.
    /// 5. Chuyển vòng ngược lại kho đã đi qua trong cùng mã lô -> Bị chặn.
    /// 6. Tuyến không tồn tại hoặc đã bị tắt (IsActive = false) -> Bị chặn.
    /// 7. Vượt quá tồn kho khả dụng tại kho gửi -> Bị từ chối.
    /// 8. Trừ tồn khi tạo phiếu, cộng tồn khi nhận, bảo toàn tổng tồn toàn hệ thống (InTransit).
    /// 9. Hủy phiếu hoàn lại tồn kho đúng tại kho gửi.
    /// </summary>
    public class StockTransferAndHubRouteTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly TransferService _transferService;

        public StockTransferAndHubRouteTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Khởi tạo Role & User phục vụ khóa ngoại CreatedByUserId
            var role = new Role { Id = 1, Name = "Admin" };
            var user = new User { Id = 1, FullName = "Admin", Email = "admin@beautyshop.com", PasswordHash = "hash", RoleId = 1, IsActive = true };
            _context.Roles.Add(role);
            _context.Users.Add(user);

            // Khởi tạo sản phẩm & biến thể
            var brand = new Brand { Id = 1, Name = "Shiseido" };
            var category = new Category { Id = 1, Name = "Kem chống nắng", Slug = "kem-chong-nang" };
            var product = new Product { Id = 1, Name = "Shiseido Expert Sun", BrandId = 1, CategoryId = 1, Description = "Mô tả", IsActive = true };
            var variant = new ProductVariant { Id = 1, ProductId = 1, SKU = "SHI-SUN-01", Volume = "50ml", OriginalPrice = 500000, RetailPrice = 750000 };

            _context.Brands.Add(brand);
            _context.Categories.Add(category);
            _context.Products.Add(product);
            _context.ProductVariants.Add(variant);

            _context.SaveChanges();

            _transferService = new TransferService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task Test_DirectTransfer_ValidRoute_Success()
        {
            // Kho TP.HCM (Id 1) có 50 hộp, chuyển thẳng trực tiếp sang Cần Thơ (Id 6)
            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 50, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            var items = new List<(int VariantId, int Quantity)> { (1, 15) };
            var result = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: items, userId: 1, userName: "Admin", note: "Chuyển thẳng nội miền");

            Assert.True(result.Success);
            Assert.NotNull(result.Transfer);
            Assert.Equal("InTransit", result.Transfer.Status);
            Assert.Equal(1, result.Transfer.FromStoreId);
            Assert.Equal(6, result.Transfer.ToStoreId);

            // Kiểm tra tồn kho kho TP.HCM: StockQuantity giảm 15, InTransitQuantity tăng 15
            var updatedInvHcm = await _context.Inventories.FirstAsync(i => i.StoreId == 1 && i.VariantId == 1);
            Assert.Equal(35, updatedInvHcm.StockQuantity);
            Assert.Equal(15, updatedInvHcm.InTransitQuantity);
        }

        [Fact]
        public async Task Test_RouteViaHub_DirectTransfer_Blocked()
        {
            // Tuyến Hà Nội (3) -> Lâm Đồng (2) là tuyến liên miền, bắt buộc qua Hub Đà Nẵng (5)
            // Nếu cố tình chuyển thẳng Hà Nội -> Lâm Đồng thì phải bị từ chối
            var invHn = new Inventory { StoreId = 3, VariantId = 1, StockQuantity = 100, ReservedQuantity = 0 };
            _context.Inventories.Add(invHn);
            await _context.SaveChangesAsync();

            var items = new List<(int VariantId, int Quantity)> { (1, 20) };
            var result = await _transferService.CreateTransferAsync(
                fromStoreId: 3, toStoreId: 2, finalDestinationStoreId: 2,
                batchCode: null, items: items, userId: 1, userName: "Admin", note: "Cố tình đi tắt ngang");

            Assert.False(result.Success);
            Assert.Contains("bắt buộc chặng đầu phải gom về", result.Message);
            Assert.Null(result.Transfer);

            // Tồn kho Hà Nội không thay đổi
            var checkInv = await _context.Inventories.FirstAsync(i => i.StoreId == 3 && i.VariantId == 1);
            Assert.Equal(100, checkInv.StockQuantity);
            Assert.Equal(0, checkInv.InTransitQuantity);
        }

        [Fact]
        public async Task Test_RouteViaHub_TransferViaHub_Success()
        {
            // Kịch bản hoàn chỉnh: Hà Nội (3) -> Hub Đà Nẵng (5) -> Lâm Đồng (2)
            var invHn = new Inventory { StoreId = 3, VariantId = 1, StockQuantity = 80, ReservedQuantity = 0 };
            _context.Inventories.Add(invHn);
            await _context.SaveChangesAsync();

            // CHẶNG 1: Hà Nội -> Hub Đà Nẵng (Đích cuối: Lâm Đồng)
            var itemsLeg1 = new List<(int VariantId, int Quantity)> { (1, 30) };
            var leg1Result = await _transferService.CreateTransferAsync(
                fromStoreId: 3, toStoreId: 5, finalDestinationStoreId: 2,
                batchCode: null, items: itemsLeg1, userId: 1, userName: "Admin HN", note: "Chặng 1 vào Hub");

            Assert.True(leg1Result.Success);
            var batchCode = leg1Result.Transfer!.BatchCode;
            Assert.Equal(1, leg1Result.Transfer.LegIndex);

            // Hub Đà Nẵng tiếp nhận chặng 1
            var receiveLeg1 = await _transferService.ReceiveTransferAsync(leg1Result.Transfer.Id, 1, "Thủ kho Đà Nẵng", "Đã nhận đủ tại Hub");
            Assert.True(receiveLeg1.Success);
            Assert.False(receiveLeg1.IsAtFinalDestination); // Chưa phải đích cuối

            var invDn = await _context.Inventories.FirstAsync(i => i.StoreId == 5 && i.VariantId == 1);
            Assert.Equal(30, invDn.StockQuantity);

            // CHẶNG 2: Hub Đà Nẵng -> Lâm Đồng (Đích cuối: Lâm Đồng) với cùng BatchCode
            var itemsLeg2 = new List<(int VariantId, int Quantity)> { (1, 30) };
            var leg2Result = await _transferService.CreateTransferAsync(
                fromStoreId: 5, toStoreId: 2, finalDestinationStoreId: 2,
                batchCode: batchCode, items: itemsLeg2, userId: 1, userName: "Admin ĐN", note: "Chặng 2 từ Hub đi Lâm Đồng");

            Assert.True(leg2Result.Success);
            Assert.Equal(batchCode, leg2Result.Transfer!.BatchCode);
            Assert.Equal(2, leg2Result.Transfer.LegIndex);

            // Lâm Đồng tiếp nhận chặng 2
            var receiveLeg2 = await _transferService.ReceiveTransferAsync(leg2Result.Transfer.Id, 1, "Thủ kho Lâm Đồng", "Đã nhận đủ tại đích cuối");
            Assert.True(receiveLeg2.Success);
            Assert.True(receiveLeg2.IsAtFinalDestination); // Đã tới đích cuối cùng

            // Tồn kho Lâm Đồng tăng 30
            var invLd = await _context.Inventories.FirstAsync(i => i.StoreId == 2 && i.VariantId == 1);
            Assert.Equal(30, invLd.StockQuantity);

            // Toàn bộ hành trình lô hàng có 2 chặng
            var history = await _transferService.GetBatchHistoryAsync(batchCode);
            Assert.Equal(2, history.Count);
            Assert.All(history, t => Assert.Equal("Received", t.Status));
        }

        [Fact]
        public async Task Test_SameStoreTransfer_Blocked()
        {
            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 20, ReservedQuantity = 0 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            var items = new List<(int VariantId, int Quantity)> { (1, 5) };
            var result = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 1, finalDestinationStoreId: 1,
                batchCode: null, items: items, userId: 1, userName: "Admin", note: "Chuyển về chính mình");

            Assert.False(result.Success);
            Assert.Contains("không được trùng nhau", result.Message);
        }

        [Fact]
        public async Task Test_LoopTransfer_Blocked()
        {
            // Chặn chuyển vòng ngược: Lô hàng từ Hà Nội (3) -> Hub Đà Nẵng (5)
            // Tại Hub Đà Nẵng, cố tình chọn chuyển ngược lại Hà Nội (3) -> Phải bị chặn!
            var invHn = new Inventory { StoreId = 3, VariantId = 1, StockQuantity = 40, ReservedQuantity = 0 };
            _context.Inventories.Add(invHn);
            await _context.SaveChangesAsync();

            // Chặng 1: Hà Nội -> Đà Nẵng
            var leg1 = await _transferService.CreateTransferAsync(
                fromStoreId: 3, toStoreId: 5, finalDestinationStoreId: 2,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 10) }, userId: 1, userName: "Admin", note: null);

            await _transferService.ReceiveTransferAsync(leg1.Transfer!.Id, 1, "Admin", null);

            // Cố tình chuyển ngược từ Đà Nẵng (5) về lại Hà Nội (3) cùng batchCode
            var loopResult = await _transferService.CreateTransferAsync(
                fromStoreId: 5, toStoreId: 3, finalDestinationStoreId: 3,
                batchCode: leg1.Transfer.BatchCode, items: new List<(int VariantId, int Quantity)> { (1, 10) }, userId: 1, userName: "Admin", note: "Quay ngược lại");

            Assert.False(loopResult.Success);
            Assert.Contains("Chặn chuyển vòng ngược", loopResult.Message);
        }

        [Fact]
        public async Task Test_RouteNotExistOrDisabled_Blocked()
        {
            // Tắt tuyến TP.HCM -> Cần Thơ (Route Id 14)
            var route = await _context.TransferRoutes.FirstAsync(r => r.Id == 14);
            route.IsActive = false;
            await _context.SaveChangesAsync();

            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 20, ReservedQuantity = 0 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            var result = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 5) }, userId: 1, userName: "Admin", note: null);

            Assert.False(result.Success);
            Assert.Contains("tạm đóng", result.Message);
        }

        [Fact]
        public async Task Test_ExceedAvailableStock_Rejected()
        {
            // Tồn kho: 20, Đang giữ (Reserved): 15 -> Khả dụng chỉ còn 5
            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 20, ReservedQuantity = 15 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            // Cố tình chuyển 10 sản phẩm
            var items = new List<(int VariantId, int Quantity)> { (1, 10) };
            var result = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: items, userId: 1, userName: "Admin", note: null);

            Assert.False(result.Success);
            Assert.Contains("chỉ còn khả dụng 5", result.Message);
        }

        [Fact]
        public async Task Test_InventoryPreserved_ThroughCreationAndReceipt()
        {
            // Bảo toàn tồn kho: Tổng tồn kho toàn hệ thống không đổi
            var invFrom = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 100, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.Add(invFrom);
            await _context.SaveChangesAsync();

            int initialTotalStock = invFrom.StockQuantity;

            // 1. Tạo phiếu chuyển 25 sản phẩm
            var createResult = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 25) }, userId: 1, userName: "Admin", note: null);

            Assert.True(createResult.Success);

            // Kiểm tra tổng tồn kho (Kho 1 tồn + Kho 1 đang chuyển) = 75 + 25 = 100
            var invFromAfter = await _context.Inventories.FirstAsync(i => i.StoreId == 1 && i.VariantId == 1);
            Assert.Equal(75, invFromAfter.StockQuantity);
            Assert.Equal(25, invFromAfter.InTransitQuantity);
            Assert.Equal(initialTotalStock, invFromAfter.StockQuantity + invFromAfter.InTransitQuantity);

            // 2. Nhận hàng tại kho 6
            var receiveResult = await _transferService.ReceiveTransferAsync(createResult.Transfer!.Id, 1, "Thủ kho 6", null);
            Assert.True(receiveResult.Success);

            var invFromFinal = await _context.Inventories.FirstAsync(i => i.StoreId == 1 && i.VariantId == 1);
            var invToFinal = await _context.Inventories.FirstAsync(i => i.StoreId == 6 && i.VariantId == 1);

            Assert.Equal(75, invFromFinal.StockQuantity);
            Assert.Equal(0, invFromFinal.InTransitQuantity);
            Assert.Equal(25, invToFinal.StockQuantity);

            // Tổng tồn toàn hệ thống: 75 + 25 = 100
            int finalTotalStock = invFromFinal.StockQuantity + invToFinal.StockQuantity;
            Assert.Equal(initialTotalStock, finalTotalStock);
        }

        [Fact]
        public async Task Test_CancelTransfer_RestoresStock()
        {
            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 50, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            // Tạo phiếu chuyển 20 sản phẩm
            var createResult = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 20) }, userId: 1, userName: "Admin", note: null);

            Assert.True(createResult.Success);

            // Hủy phiếu khi đang chuyển
            var cancelResult = await _transferService.CancelTransferAsync(createResult.Transfer!.Id, 1, "Admin", "Xe chuyển kho bị hoãn");
            Assert.True(cancelResult.Success);

            // Tồn kho kho gửi được hoàn nguyên
            var restoredInv = await _context.Inventories.FirstAsync(i => i.StoreId == 1 && i.VariantId == 1);
            Assert.Equal(50, restoredInv.StockQuantity);
            Assert.Equal(0, restoredInv.InTransitQuantity);

            var transfer = await _context.StockTransfers.FindAsync(createResult.Transfer.Id);
            Assert.Equal("Cancelled", transfer!.Status);
        }

        [Fact]
        public async Task Test_Transfer_StockBeforeAndAfter_Accurate()
        {
            // Kho gửi có 50, kho nhận có 10
            var invFrom = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 50, ReservedQuantity = 0, InTransitQuantity = 0 };
            var invTo = new Inventory { StoreId = 6, VariantId = 1, StockQuantity = 10, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.AddRange(invFrom, invTo);
            await _context.SaveChangesAsync();

            // Chuyển 15 sản phẩm từ 1 sang 6
            var createResult = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 15) },
                userId: 1, userName: "Admin", note: "Kiểm tra đối chiếu tồn kho");

            Assert.True(createResult.Success);
            int transferId = createResult.Transfer!.Id;

            // Kiểm tra đối chiếu tồn kho khi đang chuyển (InTransit)
            var comparisonInTransit = await _transferService.GetTransferStockComparisonAsync(transferId);
            Assert.Single(comparisonInTransit);
            var row1 = comparisonInTransit.First();
            Assert.Equal(1, row1.VariantId);
            Assert.Equal(15, row1.Quantity);
            Assert.Equal(50, row1.FromStoreInitialStock);
            Assert.Equal(35, row1.FromStoreAfterStock);
            Assert.Equal(10, row1.ToStoreInitialStock);
            Assert.Equal(10, row1.ToStoreCurrentActualStock); // Chưa nhận nên tồn thực tế tại đích vẫn là 10
            Assert.Equal(25, row1.ToStoreExpectedAfterStock); // Dự kiến khi nhận sẽ là 25

            // Nhận hàng tại kho đích
            var receiveResult = await _transferService.ReceiveTransferAsync(transferId, 1, "Admin", "Nhận đủ 15 hộp");
            Assert.True(receiveResult.Success);

            // Kiểm tra đối chiếu tồn kho sau khi đã nhận (Received)
            var comparisonReceived = await _transferService.GetTransferStockComparisonAsync(transferId);
            var row2 = comparisonReceived.First();
            Assert.Equal(50, row2.FromStoreInitialStock);
            Assert.Equal(35, row2.FromStoreAfterStock);
            Assert.Equal(10, row2.ToStoreInitialStock);
            Assert.Equal(25, row2.ToStoreCurrentActualStock); // Đã nhận nên tồn thực tế tại đích tăng lên 25
        }

        [Fact]
        public async Task Test_Transfer_ReceiverInventoryInitializedOnCreate()
        {
            // Kho 1 có 40 sản phẩm, kho 6 CHƯA TỪNG CÓ bản ghi Inventory của sản phẩm này
            var invFrom = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 40, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.Add(invFrom);
            await _context.SaveChangesAsync();

            // Kiểm tra ban đầu kho 6 chưa có bản ghi
            var initialInvTo = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 6 && i.VariantId == 1);
            Assert.Null(initialInvTo);

            // Tạo phiếu chuyển 10 sản phẩm từ 1 sang 6
            var createResult = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 10) },
                userId: 1, userName: "Admin", note: "Chuyển cho kho chưa từng có sản phẩm");

            Assert.True(createResult.Success);

            // Xác nhận bản ghi kho tại kho 6 đã được tự động khởi tạo với StockQuantity = 0
            var autoInitInvTo = await _context.Inventories.FirstOrDefaultAsync(i => i.StoreId == 6 && i.VariantId == 1);
            Assert.NotNull(autoInitInvTo);
            Assert.Equal(0, autoInitInvTo.StockQuantity);

            // Khi nhận hàng, tồn kho kho 6 tăng đúng 10
            var receiveResult = await _transferService.ReceiveTransferAsync(createResult.Transfer!.Id, 1, "Admin", "Nhận hàng vào kho mới");
            Assert.True(receiveResult.Success);

            var finalInvTo = await _context.Inventories.FirstAsync(i => i.StoreId == 6 && i.VariantId == 1);
            Assert.Equal(10, finalInvTo.StockQuantity);
        }

        [Fact]
        public async Task Test_HoldingLocation_InTransitAndReceived()
        {
            var invHcm = new Inventory { StoreId = 1, VariantId = 1, StockQuantity = 30, ReservedQuantity = 0, InTransitQuantity = 0 };
            _context.Inventories.Add(invHcm);
            await _context.SaveChangesAsync();

            var createResult = await _transferService.CreateTransferAsync(
                fromStoreId: 1, toStoreId: 6, finalDestinationStoreId: 6,
                batchCode: null, items: new List<(int VariantId, int Quantity)> { (1, 5) },
                userId: 1, userName: "Admin", note: "Kiểm tra vị trí hàng");

            Assert.True(createResult.Success);
            var transfer = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .FirstAsync(t => t.Id == createResult.Transfer!.Id);

            // Khi đang vận chuyển
            string locInTransit = _transferService.GetCurrentHoldingLocation(transfer);
            Assert.Contains("Đang trên xe vận chuyển", locInTransit);

            // Khi đã nhận hàng
            await _transferService.ReceiveTransferAsync(transfer.Id, 1, "Admin", "Đã nhận");
            var receivedTransfer = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .FirstAsync(t => t.Id == transfer.Id);

            string locReceived = _transferService.GetCurrentHoldingLocation(receivedTransfer);
            Assert.Contains("Đã nhập kho an toàn tại", locReceived);
        }
    }
}
