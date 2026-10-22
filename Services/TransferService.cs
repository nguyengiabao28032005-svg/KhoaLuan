using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public class TransferService : ITransferService
    {
        private readonly ApplicationDbContext _context;

        public TransferService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách các kho đích hợp lệ có tuyến xuất phát từ kho gửi
        /// </summary>
        public async Task<List<DestinationStoreOption>> GetValidDestinationsAsync(int fromStoreId)
        {
            var routes = await _context.TransferRoutes
                .Include(r => r.ToStore)
                .Include(r => r.TransitStore)
                .Where(r => r.FromStoreId == fromStoreId && r.IsActive)
                .ToListAsync();

            var options = new List<DestinationStoreOption>();
            foreach (var r in routes)
            {
                if (r.ToStore == null || !r.ToStore.IsActive) continue;

                options.Add(new DestinationStoreOption
                {
                    StoreId = r.ToStoreId,
                    StoreName = r.ToStore.Name,
                    ProvinceName = r.ToStore.ProvinceName,
                    Region = r.ToStore.Region,
                    StoreType = r.ToStore.StoreType,
                    IsDirect = !r.TransitStoreId.HasValue,
                    TransitStoreId = r.TransitStoreId,
                    TransitStoreName = r.TransitStore?.Name,
                    DistanceKm = r.DistanceKm,
                    EstimatedHours = r.EstimatedHours,
                    RouteDescription = r.Description
                });
            }

            return options;
        }

        /// <summary>
        /// Tạo phiếu chuyển kho với đầy đủ các quy tắc kiểm tra nghiêm ngặt:
        /// 1. Kho gửi khác kho nhận.
        /// 2. Tuyến hợp lệ và đang hoạt động.
        /// 3. Nếu tuyến qua Hub thì chặng đầu bắt buộc đến Hub, không cho đi thẳng.
        /// 4. Chặn chuyển vòng ngược (kho đã đi qua trong cùng mã lô).
        /// 5. Số lượng > 0 và không vượt quá tồn khả dụng tại kho gửi.
        /// 6. Cập nhật tồn kho (trừ tồn kho đi, tăng tồn đang chuyển InTransit) và ghi InventoryLog trong Transaction.
        /// </summary>
        public async Task<(bool Success, string Message, StockTransfer? Transfer)> CreateTransferAsync(
            int fromStoreId,
            int toStoreId,
            int finalDestinationStoreId,
            string? batchCode,
            List<(int VariantId, int Quantity)> items,
            int? userId,
            string? userName,
            string? note)
        {
            if (fromStoreId <= 0 || toStoreId <= 0 || finalDestinationStoreId <= 0)
            {
                return (false, "Vui lòng chọn đầy đủ thông tin kho gửi, kho nhận và kho đích cuối cùng.", null);
            }

            if (fromStoreId == toStoreId)
            {
                return (false, "Kho gửi và kho nhận của chặng không được trùng nhau.", null);
            }

            if (items == null || items.Count == 0 || items.All(x => x.Quantity <= 0))
            {
                return (false, "Vui lòng chọn ít nhất một sản phẩm và số lượng chuyển phải lớn hơn 0.", null);
            }

            var fromStore = await _context.Stores.FirstOrDefaultAsync(s => s.Id == fromStoreId);
            var toStore = await _context.Stores.FirstOrDefaultAsync(s => s.Id == toStoreId);
            var finalStore = await _context.Stores.FirstOrDefaultAsync(s => s.Id == finalDestinationStoreId);

            if (fromStore == null || !fromStore.IsActive)
            {
                return (false, "Kho gửi không tồn tại hoặc đang tạm ngừng hoạt động.", null);
            }

            if (toStore == null || !toStore.IsActive)
            {
                return (false, "Kho nhận không tồn tại hoặc đang tạm ngừng hoạt động.", null);
            }

            if (finalStore == null || !finalStore.IsActive)
            {
                return (false, "Kho đích cuối cùng không tồn tại hoặc đang tạm ngừng hoạt động.", null);
            }

            // Kiểm tra phạm vi kho gửi (FromStoreId) nếu người thực hiện không phải Admin
            if (userId.HasValue)
            {
                var actingUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.WarehouseScopes)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (actingUser != null && actingUser.Role?.Name != AppRoles.Admin)
                {
                    if (!actingUser.WarehouseScopes.Any(ws => ws.StoreId == fromStoreId))
                    {
                        return (false, $"Truy cập bị từ chối: Bạn không có phạm vi quản lý tại kho gửi '{fromStore.Name}', không được phép lập phiếu xuất chuyển từ kho này.", null);
                    }
                }
            }

            // 1. KIỂM TRA TUYẾN TỪ KHO GỬI ĐẾN KHO ĐÍCH
            // Đối với chặng đầu (chưa có batchCode hoặc kho gửi là kho xuất phát ban đầu):
            // Kiểm tra tuyến tổng thể từ fromStore đến finalDestinationStore
            var mainRoute = await _context.TransferRoutes
                .Include(r => r.TransitStore)
                .FirstOrDefaultAsync(r => r.FromStoreId == fromStoreId && r.ToStoreId == finalDestinationStoreId);

            bool isLegFromHub = false;
            List<StockTransfer> previousTransfers = new();

            if (!string.IsNullOrWhiteSpace(batchCode))
            {
                previousTransfers = await _context.StockTransfers
                    .Where(t => t.BatchCode == batchCode)
                    .OrderBy(t => t.LegIndex)
                    .ToListAsync();

                if (previousTransfers.Any())
                {
                    isLegFromHub = true;
                }
            }

            if (!isLegFromHub)
            {
                // Đây là chặng đầu tiên
                if (mainRoute == null || !mainRoute.IsActive)
                {
                    return (false, $"Không tồn tại tuyến vận chuyển hợp lệ (hoặc tuyến đang tạm đóng) từ '{fromStore.Name}' đến '{finalStore.Name}'.", null);
                }

                // Nếu tuyến yêu cầu qua Hub thì chặng đầu BẮT BUỘC nhận tại Hub, không được đi thẳng
                if (mainRoute.TransitStoreId.HasValue)
                {
                    if (toStoreId != mainRoute.TransitStoreId.Value)
                    {
                        var hubName = mainRoute.TransitStore?.Name ?? "Hub trung chuyển";
                        return (false, $"Tuyến từ '{fromStore.Name}' đi '{finalStore.Name}' là tuyến liên miền, bắt buộc chặng đầu phải gom về '{hubName}'. Không được phép chuyển trực tiếp đi tắt ngang.", null);
                    }
                }
                else
                {
                    // Tuyến đi thẳng: kho nhận của chặng phải chính là kho đích cuối cùng
                    if (toStoreId != finalDestinationStoreId)
                    {
                        return (false, $"Tuyến từ '{fromStore.Name}' đến '{finalStore.Name}' là tuyến vận chuyển trực tiếp, kho nhận chặng này phải là '{finalStore.Name}'.", null);
                    }
                }
            }
            else
            {
                // Đây là chặng tiếp theo (ví dụ xuất phát từ Hub đi đích tiếp theo)
                // Kiểm tra tuyến chặng này từ fromStoreId đến toStoreId
                var legRoute = await _context.TransferRoutes
                    .FirstOrDefaultAsync(r => r.FromStoreId == fromStoreId && r.ToStoreId == toStoreId);

                if (legRoute == null || !legRoute.IsActive)
                {
                    return (false, $"Không tồn tại tuyến vận chuyển trực tiếp hợp lệ từ '{fromStore.Name}' đến '{toStore.Name}'.", null);
                }
            }

            // 2. CHẶN CHUYỂN VÒNG NGƯỢC (LOOP DETECTION)
            if (previousTransfers.Any())
            {
                // Lấy tất cả các kho đã từng xuất phát hoặc đã từng tiếp nhận trong lô hàng này
                var visitedStoreIds = new HashSet<int>();
                foreach (var pt in previousTransfers)
                {
                    visitedStoreIds.Add(pt.FromStoreId);
                    visitedStoreIds.Add(pt.ToStoreId);
                }

                if (visitedStoreIds.Contains(toStoreId))
                {
                    return (false, $"Chặn chuyển vòng ngược: Lô hàng '{batchCode}' đã từng đi qua kho '{toStore.Name}', không được phép chuyển vòng ngược lại kho cũ.", null);
                }
            }

            // 3. KIỂM TRA TỒN KHO KHẢ DỤNG TẠI KHO GỬI
            foreach (var item in items)
            {
                if (item.Quantity <= 0)
                {
                    return (false, "Số lượng chuyển mỗi sản phẩm phải lớn hơn 0.", null);
                }

                var inv = await _context.Inventories
                    .Include(i => i.Variant)
                    .ThenInclude(v => v!.Product)
                    .FirstOrDefaultAsync(i => i.StoreId == fromStoreId && i.VariantId == item.VariantId);

                if (inv == null)
                {
                    var variant = await _context.ProductVariants.Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == item.VariantId);
                    var vName = variant?.Product?.Name ?? $"Biến thể #{item.VariantId}";
                    return (false, $"Sản phẩm '{vName}' chưa từng có dữ liệu tồn kho tại '{fromStore.Name}'.", null);
                }

                int availableStock = inv.StockQuantity - inv.ReservedQuantity;
                if (availableStock < item.Quantity)
                {
                    var vName = inv.Variant?.Product?.Name ?? $"Biến thể #{item.VariantId}";
                    return (false, $"Sản phẩm '{vName}' tại kho '{fromStore.Name}' chỉ còn khả dụng {availableStock} sản phẩm (Tồn: {inv.StockQuantity}, Đang giữ: {inv.ReservedQuantity}), không đủ để chuyển {item.Quantity} sản phẩm.", null);
                }
            }

            // 4. THỰC HIỆN TẠO PHIẾU VÀ TRỪ TỒN KHO TRONG TRANSACTION
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string dateStr = DateTime.Now.ToString("yyyyMMdd");
                string finalBatchCode = batchCode;

                if (string.IsNullOrWhiteSpace(finalBatchCode))
                {
                    int batchCountToday = await _context.StockTransfers
                        .Where(t => t.CreatedAt.Date == DateTime.Today)
                        .Select(t => t.BatchCode)
                        .Distinct()
                        .CountAsync() + 1;
                    finalBatchCode = $"LOT-{dateStr}-{batchCountToday:D4}";
                }

                int transferCountToday = await _context.StockTransfers
                    .CountAsync(t => t.CreatedAt.Date == DateTime.Today) + 1;
                string transferCode = $"CK-{dateStr}-{transferCountToday:D4}";

                int legIndex = previousTransfers.Any() ? previousTransfers.Max(t => t.LegIndex) + 1 : 1;

                var stockTransfer = new StockTransfer
                {
                    TransferCode = transferCode,
                    BatchCode = finalBatchCode,
                    LegIndex = legIndex,
                    FromStoreId = fromStoreId,
                    ToStoreId = toStoreId,
                    FinalDestinationStoreId = finalDestinationStoreId,
                    Status = "InTransit",
                    CreatedAt = DateTime.Now,
                    CreatedByUserId = userId,
                    CreatedByName = userName ?? "Quản trị viên",
                    Note = note
                };

                _context.StockTransfers.Add(stockTransfer);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    var line = new StockTransferLine
                    {
                        StockTransferId = stockTransfer.Id,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity
                    };
                    _context.StockTransferLines.Add(line);

                    // Trừ tồn kho vật lý tại kho gửi và tăng InTransitQuantity
                    var inv = await _context.Inventories.FirstAsync(i => i.StoreId == fromStoreId && i.VariantId == item.VariantId);
                    inv.StockQuantity -= item.Quantity;
                    inv.InTransitQuantity += item.Quantity;

                    // Ghi thẻ kho InventoryLog
                    var log = new InventoryLog
                    {
                        InventoryId = inv.Id,
                        ChangeType = "XuatChuyenKho",
                        QuantityChanged = -item.Quantity,
                        Note = $"Xuất chuyển kho chặng {legIndex} (Phiếu {transferCode}, Lô {finalBatchCode}) đến '{toStore.Name}'",
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryLogs.Add(log);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Tạo phiếu chuyển kho '{transferCode}' (Lô {finalBatchCode} - Chặng {legIndex}) thành công. Hàng đang trên đường luân chuyển đến '{toStore.Name}'.", stockTransfer);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi tạo phiếu chuyển kho: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Xác nhận nhận hàng tại kho đích của chặng:
        /// - Đổi trạng thái sang "Received"
        /// - Giảm InTransitQuantity tại kho gửi
        /// - Tăng StockQuantity tại kho nhận
        /// - Ghi thẻ kho InventoryLog
        /// </summary>
        public async Task<(bool Success, string Message, bool IsAtFinalDestination)> ReceiveTransferAsync(
            int transferId,
            int? userId,
            string? userName,
            string? note)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.Lines)
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.FinalDestinationStore)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
            {
                return (false, "Không tìm thấy thông tin phiếu chuyển kho.", false);
            }

            // Kiểm tra phạm vi kho nhận (ToStoreId) nếu người thực hiện không phải Admin
            if (userId.HasValue)
            {
                var actingUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.WarehouseScopes)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (actingUser != null && actingUser.Role?.Name != AppRoles.Admin)
                {
                    if (!actingUser.WarehouseScopes.Any(ws => ws.StoreId == transfer.ToStoreId))
                    {
                        return (false, $"Truy cập bị từ chối: Chỉ nhân viên được phân quyền phạm vi tại kho nhận '{transfer.ToStore?.Name}' mới có quyền xác nhận tiếp nhận phiếu chuyển kho này.", false);
                    }
                }
            }

            if (transfer.Status != "InTransit")
            {
                string statusText = transfer.Status == "Received" ? "đã được tiếp nhận trước đó" : "đã bị hủy";
                return (false, $"Phiếu chuyển kho '{transfer.TransferCode}' {statusText}, không thể nhận lại.", false);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                transfer.Status = "Received";
                transfer.ReceivedAt = DateTime.Now;
                transfer.ReceivedByUserId = userId;
                transfer.ReceivedByName = userName ?? "Quản trị viên";
                if (!string.IsNullOrWhiteSpace(note))
                {
                    transfer.Note = string.IsNullOrEmpty(transfer.Note) ? note : $"{transfer.Note} | Ghi chú nhận: {note}";
                }

                foreach (var line in transfer.Lines)
                {
                    // 1. Giảm InTransitQuantity tại kho gửi
                    var invFrom = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.StoreId == transfer.FromStoreId && i.VariantId == line.VariantId);
                    if (invFrom != null)
                    {
                        invFrom.InTransitQuantity = Math.Max(0, invFrom.InTransitQuantity - line.Quantity);
                    }

                    // 2. Tăng StockQuantity tại kho nhận (nếu chưa có bản ghi tồn kho thì tạo mới)
                    var invTo = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.StoreId == transfer.ToStoreId && i.VariantId == line.VariantId);
                    if (invTo == null)
                    {
                        invTo = new Inventory
                        {
                            StoreId = transfer.ToStoreId,
                            VariantId = line.VariantId,
                            StockQuantity = 0,
                            ReservedQuantity = 0,
                            InTransitQuantity = 0
                        };
                        _context.Inventories.Add(invTo);
                        await _context.SaveChangesAsync();
                    }

                    invTo.StockQuantity += line.Quantity;

                    // 3. Ghi thẻ kho InventoryLog tại kho nhận
                    var log = new InventoryLog
                    {
                        InventoryId = invTo.Id,
                        ChangeType = "NhapChuyenKho",
                        QuantityChanged = line.Quantity,
                        Note = $"Nhập chuyển kho chặng {transfer.LegIndex} (Phiếu {transfer.TransferCode}, Lô {transfer.BatchCode}) nhận từ '{transfer.FromStore?.Name}'",
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryLogs.Add(log);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                bool isAtFinalDestination = (transfer.ToStoreId == transfer.FinalDestinationStoreId);
                string msg = isAtFinalDestination
                    ? $"Đã xác nhận tiếp nhận hàng thành công tại kho đích cuối cùng '{transfer.ToStore?.Name}'. Lô hàng '{transfer.BatchCode}' đã hoàn tất toàn bộ hành trình."
                    : $"Đã tiếp nhận hàng thành công tại kho trung chuyển '{transfer.ToStore?.Name}'. Kiện hàng đang chờ tại Hub để điều phối chặng tiếp theo đến '{transfer.FinalDestinationStore?.Name}'.";

                return (true, msg, isAtFinalDestination);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi xác nhận nhận hàng: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Hủy phiếu chuyển kho khi đang trên đường (chưa nhận):
        /// - Hoàn lại tồn kho cho kho gửi
        /// - Giảm InTransitQuantity tại kho gửi
        /// - Ghi thẻ kho InventoryLog
        /// </summary>
        public async Task<(bool Success, string Message)> CancelTransferAsync(
            int transferId,
            int? userId,
            string? userName,
            string? reason)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.Lines)
                .Include(t => t.FromStore)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
            {
                return (false, "Không tìm thấy phiếu chuyển kho cần hủy.");
            }

            // Kiểm tra phạm vi kho gửi (FromStoreId) nếu người thực hiện không phải Admin
            if (userId.HasValue)
            {
                var actingUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.WarehouseScopes)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (actingUser != null && actingUser.Role?.Name != AppRoles.Admin)
                {
                    if (!actingUser.WarehouseScopes.Any(ws => ws.StoreId == transfer.FromStoreId))
                    {
                        return (false, $"Truy cập bị từ chối: Chỉ nhân viên được phân quyền phạm vi tại kho gửi '{transfer.FromStore?.Name}' mới có quyền hủy phiếu chuyển kho này.");
                    }
                }
            }

            if (transfer.Status != "InTransit")
            {
                return (false, $"Phiếu chuyển kho '{transfer.TransferCode}' đang ở trạng thái '{transfer.Status}', không thể hủy.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                transfer.Status = "Cancelled";
                transfer.CancelledAt = DateTime.Now;
                transfer.CancelledReason = reason ?? "Quản trị viên hủy phiếu";

                foreach (var line in transfer.Lines)
                {
                    var invFrom = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.StoreId == transfer.FromStoreId && i.VariantId == line.VariantId);
                    if (invFrom != null)
                    {
                        invFrom.StockQuantity += line.Quantity;
                        invFrom.InTransitQuantity = Math.Max(0, invFrom.InTransitQuantity - line.Quantity);

                        var log = new InventoryLog
                        {
                            InventoryId = invFrom.Id,
                            ChangeType = "HuyChuyenKho",
                            QuantityChanged = line.Quantity,
                            Note = $"Hoàn tồn do hủy phiếu chuyển kho '{transfer.TransferCode}' (Lô {transfer.BatchCode}): {reason}",
                            CreatedAt = DateTime.Now
                        };
                        _context.InventoryLogs.Add(log);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Đã hủy phiếu chuyển kho '{transfer.TransferCode}'. Toàn bộ số lượng hàng đã được hoàn lại kho gửi '{transfer.FromStore?.Name}'.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi hủy phiếu chuyển kho: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy toàn bộ hành trình các chặng của một lô hàng theo BatchCode
        /// </summary>
        public async Task<List<StockTransfer>> GetBatchHistoryAsync(string batchCode)
        {
            return await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.FinalDestinationStore)
                .Include(t => t.Lines)
                .ThenInclude(l => l.Variant)
                .ThenInclude(v => v!.Product)
                .Where(t => t.BatchCode == batchCode)
                .OrderBy(t => t.LegIndex)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy danh sách hàng đang chờ tại Hub (đã nhận tại Hub nhưng chưa tạo chặng tiếp theo đến kho đích)
        /// </summary>
        public async Task<List<TransitWaitingBatchItem>> GetTransitWaitingBatchesAsync(int hubStoreId = 5)
        {
            // Tìm tất cả các transfer đã nhận tại hub nhưng chưa đến đích cuối cùng
            var receivedAtHubTransfers = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.FinalDestinationStore)
                .Include(t => t.Lines)
                .ThenInclude(l => l.Variant)
                .ThenInclude(v => v!.Product)
                .Where(t => t.ToStoreId == hubStoreId && t.Status == "Received" && t.ToStoreId != t.FinalDestinationStoreId)
                .ToListAsync();

            var result = new List<TransitWaitingBatchItem>();

            foreach (var transfer in receivedAtHubTransfers)
            {
                // Kiểm tra xem lô hàng này đã có chặng tiếp theo xuất phát từ Hub chưa (không tính phiếu đã hủy)
                bool hasNextLeg = await _context.StockTransfers
                    .AnyAsync(t => t.BatchCode == transfer.BatchCode && t.FromStoreId == hubStoreId && t.Status != "Cancelled");

                if (!hasNextLeg)
                {
                    // Tìm kho xuất phát ban đầu của lô này (chặng 1)
                    var firstLeg = await _context.StockTransfers
                        .Include(t => t.FromStore)
                        .Where(t => t.BatchCode == transfer.BatchCode)
                        .OrderBy(t => t.LegIndex)
                        .FirstOrDefaultAsync();

                    result.Add(new TransitWaitingBatchItem
                    {
                        BatchCode = transfer.BatchCode,
                        LastTransferId = transfer.Id,
                        LastTransferCode = transfer.TransferCode,
                        OriginStoreId = firstLeg?.FromStoreId ?? transfer.FromStoreId,
                        OriginStoreName = firstLeg?.FromStore?.Name ?? transfer.FromStore?.Name ?? "Kho xuất phát",
                        CurrentHubStoreId = hubStoreId,
                        CurrentHubStoreName = transfer.ToStore?.Name ?? "Hub Đà Nẵng",
                        FinalDestinationStoreId = transfer.FinalDestinationStoreId,
                        FinalDestinationStoreName = transfer.FinalDestinationStore?.Name ?? "Kho đích",
                        ArrivedAtHubDate = transfer.ReceivedAt ?? transfer.CreatedAt,
                        Items = transfer.Lines.ToList(),
                        TotalQuantity = transfer.Lines.Sum(l => l.Quantity)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Bật/tắt trạng thái hoạt động của một tuyến chuyển kho
        /// </summary>
        public async Task<(bool Success, string Message)> ToggleRouteStatusAsync(int routeId)
        {
            var route = await _context.TransferRoutes
                .Include(r => r.FromStore)
                .Include(r => r.ToStore)
                .FirstOrDefaultAsync(r => r.Id == routeId);

            if (route == null)
            {
                return (false, "Không tìm thấy tuyến chuyển kho cần thay đổi.");
            }

            route.IsActive = !route.IsActive;
            await _context.SaveChangesAsync();

            string statusText = route.IsActive ? "kích hoạt mở" : "tạm dừng";
            return (true, $"Đã {statusText} tuyến chuyển kho từ '{route.FromStore?.Name}' đến '{route.ToStore?.Name}'.");
        }
    }
}
