using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public class DeliveryOptimizationService : IDeliveryOptimizationService
    {
        private readonly ApplicationDbContext _context;

        public DeliveryOptimizationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Từ điển ánh xạ tên Phường/Xã (mô hình chính quyền 2 cấp mới chỉ có Phường/Xã + Tỉnh/TP) và tên khu vực quen dùng sang ZoneName
        private static readonly Dictionary<string, string[]> WardAndAliasToZoneMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Hóc Môn"] = new[] { "hóc môn", "xã hóc môn", "xã bà điểm", "bà điểm", "xã xuân thới sơn", "xuân thới sơn", "xã đông thạnh", "đông thạnh", "thới tam thôn", "trung chánh", "tân xuân" },
            ["Củ Chi"] = new[] { "củ chi", "xã củ chi", "xã tân an hội", "tân an hội", "xã phú hòa đông", "phú hòa đông", "xã an nhơn tây", "an nhơn tây", "xã nhuận đức", "nhuận đức", "xã thái mỹ", "xã bình mỹ" },
            ["Quận 12"] = new[] { "quận 12", "phường đông hưng thuận", "đông hưng thuận", "phường trung mỹ tây", "trung mỹ tây", "phường tân thới hiệp", "tân thới hiệp", "phường thới an", "thới an", "phường an phú đông", "an phú đông" },
            ["Gò Vấp"] = new[] { "gò vấp", "phường hạnh thông", "hạnh thông", "phường an nhơn", "phường gò vấp", "phường thông tây hội", "thông tây hội", "phường an hội tây", "an hội tây", "phường an hội đông", "an hội đông" },
            ["Bình Thạnh"] = new[] { "bình thạnh", "phường gia định", "gia định", "phường bình thạnh", "phường bình lợi trung", "bình lợi trung", "phường thạnh mỹ tây", "thạnh mỹ tây", "phường bình quới", "bình quới" },
            ["Phú Nhuận"] = new[] { "phú nhuận", "phường đức nhuận", "đức nhuận", "phường cầu kiệu", "cầu kiệu", "phường phú nhuận" },
            ["Quận 1"] = new[] { "quận 1", "phường sài gòn", "phường tân định", "tân định", "phường bến thành", "bến thành", "phường cầu ông lãnh", "cầu ông lãnh", "phường bến nghé", "bến nghé", "phường đa kao", "đa kao", "phường nguyễn thái bình", "phường phạm ngũ lão" },
            ["Quận 3"] = new[] { "quận 3", "phường bàn cờ", "bàn cờ", "phường xuân hòa", "xuân hòa", "phường nhiêu lộc", "nhiêu lộc", "phường võ thị sáu" },
            ["Thành phố Thủ Đức"] = new[] { "thủ đức", "tp. thủ đức", "tp thủ đức", "quận 2", "quận 9", "phường thủ đức", "phường hiệp bình", "hiệp bình", "phường hiệp phú", "hiệp phú", "phường linh xuân", "linh xuân", "phường tam bình", "phường tăng nhơn phú", "tăng nhơn phú", "phường long bình", "phường cát lái", "cát lái", "phường bình trưng", "phường phước long", "phường an khánh", "an khánh", "phường thảo điền", "thảo điền", "phường an phú" },
            ["Quận 7"] = new[] { "quận 7", "phường tân mỹ", "tân mỹ", "phường tân hưng", "tân hưng", "phường tân thuận", "tân thuận", "phường phú thuận", "phú thuận", "phú mỹ hưng", "phường tân phong", "phường tân quy" },
            ["Nhà Bè"] = new[] { "nhà bè", "xã nhà bè", "xã hiệp phước", "hiệp phước", "xã phước kiển", "phước kiển", "xã nhơn đức" },
            ["Bình Chánh"] = new[] { "bình chánh", "xã bình chánh", "xã vĩnh lộc", "vĩnh lộc", "xã tân vĩnh lộc", "tân vĩnh lộc", "xã bình lợi", "xã tân nhựt", "xã hưng long", "xã bình hưng", "bình hưng" },
            ["Thành phố Đà Lạt"] = new[] { "đà lạt", "phường xuân hương", "xuân hương - đà lạt", "phường cam ly", "cam ly - đà lạt", "phường lâm viên", "lâm viên - đà lạt", "phường lang biang", "lang biang - đà lạt", "phường xuân trường" },
            ["Huyện Đức Trọng"] = new[] { "đức trọng", "xã liên nghĩa", "thị trấn liên nghĩa", "liên nghĩa", "xã hiệp thạnh", "xã ninh gia", "liên khương" }
        };

        public async Task<DeliveryZone?> DetectZoneAsync(string shippingAddress)
        {
            if (string.IsNullOrWhiteSpace(shippingAddress)) return null;

            var activeZones = await _context.DeliveryZones
                .Where(z => z.IsActive)
                .OrderByDescending(z => z.ZoneName.Length) // Ưu tiên tên dài, cụ thể hơn
                .ToListAsync();

            string normalizedAddress = shippingAddress.ToLower();

            // 1. Khớp trực tiếp theo tên khu vực đầy đủ
            foreach (var zone in activeZones)
            {
                if (normalizedAddress.Contains(zone.ZoneName.ToLower()))
                {
                    return zone;
                }
            }

            // 2. Khớp theo tên rút gọn (bỏ tiền tố Thành phố / Huyện / Quận)
            foreach (var zone in activeZones)
            {
                string keyword = zone.ZoneName.Replace("Thành phố ", "").Replace("Huyện ", "").Replace("Quận ", "").Trim().ToLower();
                if (keyword.Length > 2 && normalizedAddress.Contains(keyword))
                {
                    return zone;
                }
            }

            // 3. Khớp theo tên Phường/Xã kiểu mới (chỉ có Phường/Xã + Thành phố/Tỉnh) hoặc tên quen dùng
            foreach (var kvp in WardAndAliasToZoneMap)
            {
                foreach (var alias in kvp.Value)
                {
                    if (normalizedAddress.Contains(alias.ToLower()))
                    {
                        var matchedZone = activeZones.FirstOrDefault(z =>
                            z.ZoneName.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                            z.ZoneName.Replace("Thành phố ", "").Replace("Huyện ", "").Replace("Quận ", "").Trim().Equals(kvp.Key.Replace("Thành phố ", "").Replace("Huyện ", "").Replace("Quận ", "").Trim(), StringComparison.OrdinalIgnoreCase));

                        if (matchedZone != null)
                        {
                            return matchedZone;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<bool> AssignZoneToOrderAsync(int orderId, int zoneId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var zone = await _context.DeliveryZones.FindAsync(zoneId);
            if (order == null || zone == null) return false;

            order.DeliveryZoneId = zone.Id;
            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Order>> GetPendingDeliveryOrdersAsync(DateTime tripDate, string shift, string? clusterCode = null)
        {
            var activeStopOrderIds = await _context.DeliveryTripStops
                .Where(s => s.IsActive)
                .Select(s => s.OrderId)
                .ToListAsync();

            var query = _context.Orders
                .Include(o => o.DeliveryZone)
                .Include(o => o.OrderDetails!)
                    .ThenInclude(d => d.Variant!)
                        .ThenInclude(v => v!.Product)
                .Where(o => !activeStopOrderIds.Contains(o.Id))
                .Where(o => o.OrderStatus != "Cancelled" && o.OrderStatus != "Hủy bỏ đơn" && o.OrderStatus != "Đã hủy")
                .Where(o => o.DeliveryStatus == "Ready" || o.DeliveryStatus == "Failed" || o.DeliveryStatus == "ReadyForDelivery" || string.IsNullOrEmpty(o.DeliveryStatus))
                .AsQueryable();

            query = query.Where(o => o.ExpectedDeliveryDate.HasValue && o.ExpectedDeliveryDate.Value.Date == tripDate.Date);

            if (!string.IsNullOrEmpty(shift))
            {
                query = query.Where(o => o.DeliveryShift == shift);
            }

            if (!string.IsNullOrEmpty(clusterCode))
            {
                query = query.Where(o => o.DeliveryZone != null && o.DeliveryZone.ClusterCode == clusterCode);
            }

            return await query.OrderBy(o => o.DeliveryZone != null ? o.DeliveryZone.SortOrder : 999)
                              .ThenBy(o => o.Id)
                              .ToListAsync();
        }

        public async Task<(bool Success, string Message, List<DeliveryTrip> CreatedTrips)> AutoBatchTripsAsync(
            DateTime tripDate, 
            string shift, 
            int? maxOrdersPerTrip = null, 
            int? createdByUserId = null)
        {
            int n = maxOrdersPerTrip ?? DeliveryConfig.MaxOrdersPerTrip;
            if (n <= 0) n = 8;

            var pendingOrders = await GetPendingDeliveryOrdersAsync(tripDate, shift);
            if (!pendingOrders.Any())
            {
                return (false, $"Không có đơn hàng nào chờ giao trong ca {DeliveryConfig.GetShiftDisplayName(shift)} ngày {tripDate:dd/MM/yyyy}.", new List<DeliveryTrip>());
            }

            var unzonedCount = pendingOrders.Count(o => o.DeliveryZoneId == null);
            if (unzonedCount > 0)
            {
                // Tự động nhận diện cho các đơn chưa gán zone
                foreach (var unzoned in pendingOrders.Where(o => o.DeliveryZoneId == null))
                {
                    var detected = await DetectZoneAsync(unzoned.ShippingAddress);
                    if (detected != null)
                    {
                        unzoned.DeliveryZoneId = detected.Id;
                        unzoned.DeliveryZone = detected;
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Gom nhóm theo Cụm khu vực (ClusterCode)
            var clusterGroups = pendingOrders
                .Where(o => o.DeliveryZone != null)
                .GroupBy(o => o.DeliveryZone!.ClusterCode)
                .ToDictionary(g => g.Key, g => g.ToList());

            var otherOrders = pendingOrders.Where(o => o.DeliveryZone == null).ToList();

            var tripBatches = new List<(string ClusterCode, List<Order> Orders)>();

            var processedClusters = new HashSet<string>();
            var allZones = await _context.DeliveryZones.Where(z => z.IsActive).ToListAsync();

            foreach (var kvp in clusterGroups)
            {
                string cluster = kvp.Key;
                if (processedClusters.Contains(cluster)) continue;

                var ordersInCluster = kvp.Value;

                // Nếu số đơn trong cụm > N -> Tách thành nhiều chuyến (mỗi chuyến tối đa N đơn)
                if (ordersInCluster.Count > n)
                {
                    var sorted = ordersInCluster
                        .OrderBy(o => o.DeliveryZone!.SortOrder)
                        .ThenBy(o => o.DeliveryZoneId)
                        .ThenBy(o => o.Id)
                        .ToList();

                    for (int i = 0; i < sorted.Count; i += n)
                    {
                        var chunk = sorted.Skip(i).Take(n).ToList();
                        tripBatches.Add((cluster, chunk));
                    }
                    processedClusters.Add(cluster);
                }
                else
                {
                    // Số đơn trong cụm < N -> Kiểm tra xem có cụm kề nhau (AdjacentClusterCodes) nào cũng < N và tổng <= N không
                    var zoneInCluster = allZones.FirstOrDefault(z => z.ClusterCode == cluster);
                    var adjacentList = zoneInCluster?.AdjacentClusterCodes?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        ?? Array.Empty<string>();

                    string? pairedCluster = null;
                    foreach (var adj in adjacentList)
                    {
                        if (!processedClusters.Contains(adj) && clusterGroups.ContainsKey(adj))
                        {
                            var adjOrders = clusterGroups[adj];
                            if (adjOrders.Count + ordersInCluster.Count <= n)
                            {
                                pairedCluster = adj;
                                break;
                            }
                        }
                    }

                    if (pairedCluster != null)
                    {
                        // Ghép 2 cụm kề nhau vào 1 chuyến
                        var combinedOrders = ordersInCluster.Concat(clusterGroups[pairedCluster])
                            .OrderBy(o => o.DeliveryZone!.SortOrder)
                            .ThenBy(o => o.DeliveryZoneId)
                            .ThenBy(o => o.Id)
                            .ToList();

                        string combinedClusterCode = $"{cluster} + {pairedCluster}";
                        tripBatches.Add((combinedClusterCode, combinedOrders));

                        processedClusters.Add(cluster);
                        processedClusters.Add(pairedCluster);
                    }
                    else
                    {
                        // Không có cụm kề ghép vừa -> Đi chuyến riêng
                        var sorted = ordersInCluster
                            .OrderBy(o => o.DeliveryZone!.SortOrder)
                            .ThenBy(o => o.DeliveryZoneId)
                            .ThenBy(o => o.Id)
                            .ToList();

                        tripBatches.Add((cluster, sorted));
                        processedClusters.Add(cluster);
                    }
                }
            }

            // Nếu còn đơn chưa gán cụm khu vực, gom riêng
            if (otherOrders.Any())
            {
                for (int i = 0; i < otherOrders.Count; i += n)
                {
                    tripBatches.Add(("CHUA_PHAN_CUM", otherOrders.Skip(i).Take(n).ToList()));
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var createdTrips = new List<DeliveryTrip>();

            try
            {
                int todayTripCount = await _context.DeliveryTrips.CountAsync(t => t.TripDate.Date == tripDate.Date) + 1;
                string dateStr = tripDate.ToString("yyyyMMdd");

                foreach (var batch in tripBatches)
                {
                    string tripCode = $"TRIP-{dateStr}-{todayTripCount:D4}";
                    todayTripCount++;

                    decimal totalCodExpected = batch.Orders
                        .Where(o => o.PaymentMethod == "COD")
                        .Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);

                    var trip = new DeliveryTrip
                    {
                        TripCode = tripCode,
                        TripDate = tripDate.Date,
                        Shift = shift,
                        ClusterCode = batch.ClusterCode,
                        StoreId = batch.Orders.FirstOrDefault()?.StoreId ?? 1,
                        Status = "Draft",
                        TotalOrders = batch.Orders.Count,
                        TotalCodExpected = totalCodExpected,
                        TotalCodCollected = 0,
                        SettlementStatus = "Unsettled",
                        CreatedByUserId = createdByUserId,
                        CreatedAt = DateTime.Now
                    };

                    _context.DeliveryTrips.Add(trip);
                    await _context.SaveChangesAsync(); // Sinh Trip.Id

                    int stopIndex = 1;
                    foreach (var order in batch.Orders)
                    {
                        decimal codAmount = order.PaymentMethod == "COD" ? (order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount) : 0;

                        var stop = new DeliveryTripStop
                        {
                            DeliveryTripId = trip.Id,
                            OrderId = order.Id,
                            StopOrder = stopIndex++,
                            StopStatus = "Pending",
                            CodExpectedAmount = codAmount,
                            CodCollectedAmount = 0,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        _context.DeliveryTripStops.Add(stop);

                        order.DeliveryStatus = "Assigned";
                        order.UpdatedAt = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();
                    createdTrips.Add(trip);
                }

                await transaction.CommitAsync();
                return (true, $"Đã tạo thành công {createdTrips.Count} chuyến giao hàng tối ưu cho {pendingOrders.Count} đơn.", createdTrips);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi trong quá trình gom chuyến tự động: {ex.Message}", new List<DeliveryTrip>());
            }
        }

        public async Task<(bool Success, string Message, DeliveryTrip? Trip)> CreateManualTripAsync(
            DateTime tripDate, 
            string shift, 
            string clusterCode, 
            int? shipperUserId, 
            List<int> orderIds, 
            int? createdByUserId = null)
        {
            if (orderIds == null || !orderIds.Any())
            {
                return (false, "Vui lòng chọn ít nhất một đơn hàng để lập chuyến.", null);
            }

            // Kiểm tra shipper nếu được chọn (phải có vai trò Shipper)
            if (shipperUserId.HasValue)
            {
                var shipper = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == shipperUserId.Value);
                if (shipper == null || !shipper.IsActive)
                {
                    return (false, "Nhân viên giao hàng không tồn tại hoặc đã bị vô hiệu hóa.", null);
                }
                bool isValidShipper = shipper.Role?.Name == AppRoles.Shipper || string.Equals(shipper.Position, "Shipper", StringComparison.OrdinalIgnoreCase);
                if (!isValidShipper)
                {
                    return (false, $"Người dùng '{shipper.FullName}' không có vai trò Nhân viên giao hàng (Shipper), không thể phân công chuyến.", null);
                }
            }

            // Kiểm tra xem có đơn nào đang nằm trong chuyến khác không
            var existingActiveStops = await _context.DeliveryTripStops
                .Where(s => orderIds.Contains(s.OrderId) && s.IsActive)
                .Select(s => s.OrderId)
                .ToListAsync();

            if (existingActiveStops.Any())
            {
                var conflictOrders = await _context.Orders.Where(o => existingActiveStops.Contains(o.Id)).Select(o => o.OrderCode).ToListAsync();
                return (false, $"Các đơn sau đang nằm trong một chuyến giao khác: {string.Join(", ", conflictOrders)}. Mỗi đơn chỉ được gán vào 1 chuyến tại một thời điểm.", null);
            }

            var orders = await _context.Orders
                .Include(o => o.DeliveryZone)
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            // Sắp xếp thứ tự điểm dừng tối ưu theo khu vực (SortOrder) để không chạy vòng ngược
            var sortedOrders = orders
                .OrderBy(o => o.DeliveryZone != null ? o.DeliveryZone.SortOrder : 999)
                .ThenBy(o => o.DeliveryZoneId)
                .ThenBy(o => o.Id)
                .ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int todayTripCount = await _context.DeliveryTrips.CountAsync(t => t.TripDate.Date == tripDate.Date) + 1;
                string tripCode = $"TRIP-{tripDate:yyyyMMdd}-{todayTripCount:D4}";

                decimal totalCodExpected = sortedOrders
                    .Where(o => o.PaymentMethod == "COD")
                    .Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);

                var trip = new DeliveryTrip
                {
                    TripCode = tripCode,
                    TripDate = tripDate.Date,
                    Shift = shift,
                    ClusterCode = string.IsNullOrWhiteSpace(clusterCode) ? "MANUAL" : clusterCode.Trim(),
                    StoreId = sortedOrders.FirstOrDefault()?.StoreId ?? 1,
                    ShipperUserId = shipperUserId,
                    Status = shipperUserId.HasValue ? "Assigned" : "Draft",
                    TotalOrders = sortedOrders.Count,
                    TotalCodExpected = totalCodExpected,
                    TotalCodCollected = 0,
                    SettlementStatus = "Unsettled",
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.Now
                };

                _context.DeliveryTrips.Add(trip);
                await _context.SaveChangesAsync();

                int stopOrder = 1;
                foreach (var o in sortedOrders)
                {
                    decimal cod = o.PaymentMethod == "COD" ? (o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount) : 0;
                    var stop = new DeliveryTripStop
                    {
                        DeliveryTripId = trip.Id,
                        OrderId = o.Id,
                        StopOrder = stopOrder++,
                        StopStatus = "Pending",
                        CodExpectedAmount = cod,
                        CodCollectedAmount = 0,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.DeliveryTripStops.Add(stop);

                    o.DeliveryStatus = "Assigned";
                    o.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Đã lập thành công chuyến giao hàng {trip.TripCode} với {sortedOrders.Count} điểm dừng.", trip);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi khi lập chuyến giao hàng: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateTripStopsOrderAsync(int tripId, List<int> orderedStopIds)
        {
            var trip = await _context.DeliveryTrips
                .Include(t => t.Stops)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null) return (false, "Không tìm thấy chuyến giao hàng.");
            if (trip.Status == "Completed" || trip.Status == "Cancelled")
            {
                return (false, "Chuyến giao hàng đã hoàn tất hoặc bị hủy, không thể đổi thứ tự.");
            }

            int index = 1;
            foreach (var stopId in orderedStopIds)
            {
                var stop = trip.Stops.FirstOrDefault(s => s.Id == stopId);
                if (stop != null)
                {
                    stop.StopOrder = index++;
                }
            }

            await _context.SaveChangesAsync();
            return (true, "Đã cập nhật thứ tự các điểm dừng trong chuyến.");
        }

        public async Task<(bool Success, string Message)> AssignShipperAsync(int tripId, int shipperUserId)
        {
            var trip = await _context.DeliveryTrips.FindAsync(tripId);
            if (trip == null) return (false, "Không tìm thấy chuyến giao hàng.");

            var shipper = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == shipperUserId);
            if (shipper == null || !shipper.IsActive) return (false, "Nhân viên giao hàng không hợp lệ.");

            bool isValidShipper = shipper.Role?.Name == AppRoles.Shipper || string.Equals(shipper.Position, "Shipper", StringComparison.OrdinalIgnoreCase);
            if (!isValidShipper)
            {
                return (false, $"Người dùng '{shipper.FullName}' không có vai trò Nhân viên giao hàng (Shipper).");
            }

            trip.ShipperUserId = shipperUserId;
            if (trip.Status == "Draft") trip.Status = "Assigned";
            await _context.SaveChangesAsync();

            return (true, $"Đã gán chuyến {trip.TripCode} cho shipper {shipper.FullName}.");
        }

        public async Task<(bool Success, string Message)> StartTripAsync(int tripId, int shipperUserId)
        {
            var trip = await _context.DeliveryTrips
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null) return (false, "Không tìm thấy chuyến giao hàng.");
            if (trip.ShipperUserId != shipperUserId) return (false, "Bạn không phải là nhân viên được phân công cho chuyến này.");

            trip.Status = "InTransit";
            foreach (var stop in trip.Stops.Where(s => s.StopStatus == "Pending"))
            {
                stop.StopStatus = "Delivering";
                if (stop.Order != null) stop.Order.DeliveryStatus = "Delivering";
            }

            await _context.SaveChangesAsync();
            return (true, $"Chuyến {trip.TripCode} đã bắt đầu xuất phát giao hàng.");
        }

        public async Task<(bool Success, string Message)> RemoveOrderFromTripAsync(int tripId, int orderId, string reason)
        {
            var stop = await _context.DeliveryTripStops
                .Include(s => s.Order)
                .FirstOrDefaultAsync(s => s.DeliveryTripId == tripId && s.OrderId == orderId && s.IsActive);

            if (stop == null) return (false, "Đơn hàng không nằm trong chuyến này hoặc không còn hoạt động.");

            var trip = await _context.DeliveryTrips
                .Include(t => t.Stops)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null) return (false, "Không tìm thấy chuyến giao hàng.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                stop.StopStatus = "Removed";
                stop.IsActive = false;
                stop.Notes = reason;
                stop.CompletedAt = DateTime.Now;

                // Hoàn lại trạng thái đơn nếu đơn chưa bị hủy
                if (stop.Order != null && stop.Order.OrderStatus != "Cancelled")
                {
                    stop.Order.DeliveryStatus = "Ready";
                    stop.Order.UpdatedAt = DateTime.Now;
                }

                // Cập nhật lại tổng của chuyến
                var remainingActiveStops = trip.Stops.Where(s => s.Id != stop.Id && s.IsActive && s.StopStatus != "Removed").ToList();
                trip.TotalOrders = remainingActiveStops.Count;
                trip.TotalCodExpected = remainingActiveStops.Sum(s => s.CodExpectedAmount);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Đã gỡ đơn hàng khỏi chuyến {trip.TripCode} và cập nhật lại tổng số đơn.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi khi gỡ đơn khỏi chuyến: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CancelOrderAndRemoveFromTripAsync(int orderId, string reason)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return (false, "Không tìm thấy đơn hàng.");

            var activeStop = await _context.DeliveryTripStops
                .Include(s => s.DeliveryTrip)
                    .ThenInclude(t => t!.Stops)
                .FirstOrDefaultAsync(s => s.OrderId == orderId && s.IsActive);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.OrderStatus = "Cancelled";
                order.DeliveryStatus = "Cancelled";
                order.UpdatedAt = DateTime.Now;

                if (activeStop != null)
                {
                    activeStop.StopStatus = "Removed";
                    activeStop.IsActive = false;
                    activeStop.Notes = $"Đơn hàng bị hủy: {reason}";
                    activeStop.CompletedAt = DateTime.Now;

                    var trip = activeStop.DeliveryTrip;
                    if (trip != null)
                    {
                        var remainingActiveStops = trip.Stops
                            .Where(s => s.Id != activeStop.Id && s.IsActive && s.StopStatus != "Removed")
                            .ToList();
                        trip.TotalOrders = remainingActiveStops.Count;
                        trip.TotalCodExpected = remainingActiveStops.Sum(s => s.CodExpectedAmount);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, activeStop != null
                    ? $"Đã hủy đơn hàng {order.OrderCode} và tự động gỡ khỏi chuyến giao hàng {activeStop.DeliveryTrip?.TripCode}."
                    : $"Đã hủy đơn hàng {order.OrderCode}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi khi hủy đơn hàng: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateStopStatusAsync(
            int stopId, 
            string newStatus, 
            decimal? codCollected, 
            string? failureReason, 
            string? notes, 
            int currentUserId)
        {
            var stop = await _context.DeliveryTripStops
                .Include(s => s.Order)
                .Include(s => s.DeliveryTrip)
                    .ThenInclude(t => t!.Stops)
                .FirstOrDefaultAsync(s => s.Id == stopId);

            if (stop == null) return (false, "Không tìm thấy thông tin điểm dừng.");
            if (!stop.IsActive) return (false, "Điểm dừng này không còn hoạt động.");

            var trip = stop.DeliveryTrip;
            var order = stop.Order;

            // Kiểm tra quyền sở hữu chuyến giao nếu người thực hiện là Shipper
            var currentUser = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (currentUser != null && currentUser.Role?.Name == AppRoles.Shipper)
            {
                if (trip?.ShipperUserId != currentUserId)
                {
                    return (false, "Truy cập bị từ chối: Nhân viên giao hàng chỉ được phép cập nhật chuyến giao của chính mình.");
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (newStatus == "Success" || newStatus == "Delivered")
                {
                    stop.StopStatus = "Success";
                    stop.IsActive = false; // Hoàn thành -> không còn active để tránh trùng lặp
                    stop.CompletedAt = DateTime.Now;
                    stop.Notes = notes;

                    // Nếu là đơn COD, ghi nhận số tiền thực thu
                    if (order != null && order.PaymentMethod == "COD")
                    {
                        decimal collected = codCollected ?? stop.CodExpectedAmount;
                        stop.CodCollectedAmount = collected;
                        order.PaymentStatus = "Paid";
                    }
                    else
                    {
                        stop.CodCollectedAmount = 0; // Đơn không COD có CodCollectedAmount = 0
                    }

                    if (order != null)
                    {
                        order.DeliveryStatus = "Delivered";
                        order.OrderStatus = "Completed";
                        order.UpdatedAt = DateTime.Now;
                    }
                }
                else if (newStatus == "Failed")
                {
                    if (string.IsNullOrWhiteSpace(failureReason))
                    {
                        return (false, "Bắt buộc phải chọn hoặc nhập lý do giao hàng thất bại.");
                    }

                    stop.StopStatus = "Failed";
                    stop.IsActive = false; // Giao thất bại -> tắt active để có thể xếp vào chuyến mới lần sau
                    stop.FailureReason = failureReason.Trim();
                    stop.Notes = notes;
                    stop.CompletedAt = DateTime.Now;
                    stop.CodCollectedAmount = 0;

                    if (order != null)
                    {
                        order.FailedDeliveryAttempts += 1;
                        if (order.FailedDeliveryAttempts >= DeliveryConfig.MaxFailedAttempts)
                        {
                            // Vượt quá giới hạn giao lại -> Chuyển sang chờ xử lý
                            order.DeliveryStatus = "PendingResolution";
                        }
                        else
                        {
                            // Quay lại hàng chờ để xếp chuyến tiếp theo
                            order.DeliveryStatus = "Ready";
                        }
                        order.UpdatedAt = DateTime.Now;
                    }
                }
                else if (newStatus == "Delivering")
                {
                    stop.StopStatus = "Delivering";
                    if (order != null) order.DeliveryStatus = "Delivering";
                }

                // Cập nhật lại tổng tiền COD thực thu của chuyến
                if (trip != null)
                {
                    trip.TotalCodCollected = trip.Stops
                        .Where(s => s.StopStatus == "Success" || s.StopStatus == "Delivered")
                        .Sum(s => s.CodCollectedAmount);

                    // Kiểm tra xem tất cả điểm dừng trong chuyến đã kết thúc chưa
                    bool allFinished = trip.Stops.All(s => s.StopStatus == "Success" || s.StopStatus == "Delivered" || s.StopStatus == "Failed" || s.StopStatus == "Removed");
                    if (allFinished)
                    {
                        trip.Status = "Completed";
                        trip.CompletedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Đã cập nhật trạng thái điểm dừng thành công.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi cập nhật điểm dừng: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SettleTripCodAsync(
            int tripId, 
            int cashierUserId, 
            decimal receivedAmount, 
            string? notes)
        {
            var trip = await _context.DeliveryTrips.FindAsync(tripId);
            if (trip == null) return (false, "Không tìm thấy chuyến giao hàng.");

            var cashier = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.WarehouseScopes)
                .FirstOrDefaultAsync(u => u.Id == cashierUserId);

            if (cashier == null || !cashier.IsActive) return (false, "Thu ngân không hợp lệ.");

            // Kiểm tra phạm vi kho của thu ngân (nếu không phải Admin)
            if (cashier.Role?.Name != AppRoles.Admin && cashier.WarehouseScopes.Any())
            {
                var allowedStoreIds = cashier.WarehouseScopes.Select(ws => ws.StoreId).ToList();
                int tripStoreId = trip.StoreId ?? 1;
                if (!allowedStoreIds.Contains(tripStoreId))
                {
                    return (false, "Truy cập bị từ chối: Chuyến giao hàng này thuộc kho khác nằm ngoài phạm vi quản lý của bạn.");
                }
            }

            trip.CashierUserId = cashierUserId;
            trip.SettlementStatus = "Settled";
            trip.SettledAt = DateTime.Now;
            trip.SettlementReceivedAmount = receivedAmount;
            trip.SettlementDifference = receivedAmount - trip.TotalCodCollected;
            trip.SettlementNotes = notes?.Trim();

            await _context.SaveChangesAsync();

            string diffNotice = trip.SettlementDifference == 0 
                ? "Khớp số tiền." 
                : $"Chênh lệch: {trip.SettlementDifference:N0} đ";

            return (true, $"Đã xác nhận thu ngân nhận tiền chuyến {trip.TripCode}. {diffNotice}");
        }

        public async Task<(bool Success, string Message, DailyReconciliation? Reconciliation)> CreateDailyReconciliationAsync(
            DateTime date, 
            int cashierUserId, 
            decimal totalCashSubmitted, 
            string? differenceReason)
        {
            var cashier = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.WarehouseScopes)
                .FirstOrDefaultAsync(u => u.Id == cashierUserId);

            var allowedStoreIds = cashier?.WarehouseScopes.Select(ws => ws.StoreId).ToList() ?? new List<int>();
            bool isAdmin = cashier?.Role?.Name == AppRoles.Admin;
            int targetStoreId = allowedStoreIds.FirstOrDefault() > 0 ? allowedStoreIds.First() : 1;

            var tripsQuery = _context.DeliveryTrips
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order!)
                        .ThenInclude(o => o.OrderDetails!)
                            .ThenInclude(d => d.Variant!)
                                .ThenInclude(v => v!.Product)
                .Where(t => t.TripDate.Date == date.Date && t.SettlementStatus == "Settled");

            if (!isAdmin && allowedStoreIds.Any())
            {
                tripsQuery = tripsQuery.Where(t => allowedStoreIds.Contains(t.StoreId ?? 1));
            }

            var trips = await tripsQuery.ToListAsync();

            if (!trips.Any())
            {
                return (false, $"Chưa có chuyến giao hàng nào thuộc phạm vi kho của bạn được quyết toán (Settled) trong ngày {date:dd/MM/yyyy}.", null);
            }

            targetStoreId = trips.First().StoreId ?? targetStoreId;

            var allStops = trips.SelectMany(t => t.Stops).ToList();
            int totalDelivered = allStops.Count(s => s.StopStatus == "Success");
            int totalFailed = allStops.Count(s => s.StopStatus == "Failed");
            decimal totalCodExpected = trips.Sum(t => t.TotalCodExpected);
            decimal totalCodReceived = trips.Sum(t => t.SettlementReceivedAmount ?? t.TotalCodCollected);

            // Thống kê sản phẩm bán chạy và bán chậm trong ngày (lọc theo phạm vi kho của thu ngân)
            var deliveredOrders = allStops
                .Where(s => s.StopStatus == "Success" && s.Order != null)
                .Select(s => s.Order!)
                .ToList();

            var productStats = deliveredOrders
                .SelectMany(o => o.OrderDetails ?? new List<OrderDetail>())
                .GroupBy(d => d.Variant?.Product?.Name ?? $"SP #{d.VariantId}")
                .Select(g => new { ProductName = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
                .ToList();

            var topSelling = productStats.OrderByDescending(p => p.Quantity).Take(5).ToList();
            var slowSelling = productStats.OrderBy(p => p.Quantity).Take(5).ToList();

            string topJson = JsonSerializer.Serialize(topSelling);
            string slowJson = JsonSerializer.Serialize(slowSelling);

            decimal difference = totalCashSubmitted - totalCodReceived;

            string code = targetStoreId == 1 ? $"REC-{date:yyyyMMdd}" : $"REC-{date:yyyyMMdd}-S{targetStoreId}";

            // Kiểm tra xem đã tồn tại bản đối soát của ngày này và kho này chưa
            var existing = await _context.DailyReconciliations
                .FirstOrDefaultAsync(r => r.ReconciliationDate.Date == date.Date && r.StoreId == targetStoreId);
            if (existing != null && existing.Status == "Approved")
            {
                return (false, $"Bảng đối soát ngày {date:dd/MM/yyyy} đã được phê duyệt và khóa sổ, không thể tạo mới.", existing);
            }

            var reconciliation = existing ?? new DailyReconciliation();
            reconciliation.Code = code;
            reconciliation.StoreId = targetStoreId;
            reconciliation.ReconciliationDate = date.Date;
            reconciliation.CashierUserId = cashierUserId;
            reconciliation.TotalTripsSettled = trips.Count;
            reconciliation.TotalOrdersDelivered = totalDelivered;
            reconciliation.TotalOrdersFailed = totalFailed;
            reconciliation.TotalCodExpected = totalCodExpected;
            reconciliation.TotalCodReceived = totalCodReceived;
            reconciliation.TotalCashSubmitted = totalCashSubmitted;
            reconciliation.DifferenceAmount = difference;
            reconciliation.DifferenceReason = differenceReason?.Trim();
            reconciliation.TopSellingProductsJson = topJson;
            reconciliation.SlowSellingProductsJson = slowJson;
            reconciliation.Status = "Draft";
            reconciliation.CreatedAt = DateTime.Now;

            if (existing == null) _context.DailyReconciliations.Add(reconciliation);

            await _context.SaveChangesAsync();
            return (true, $"Đã tạo bảng đối soát ngày {date:dd/MM/yyyy}. Chờ Admin phê duyệt chốt sổ.", reconciliation);
        }

        public async Task<(bool Success, string Message)> ApproveDailyReconciliationAsync(int reconciliationId, int adminUserId)
        {
            var reconciliation = await _context.DailyReconciliations.FindAsync(reconciliationId);
            if (reconciliation == null) return (false, "Không tìm thấy bảng đối soát.");

            if (reconciliation.Status == "Approved")
            {
                return (false, "Bảng đối soát này đã được phê duyệt trước đó và đã bị khóa sổ.");
            }

            reconciliation.Status = "Approved";
            reconciliation.AdminApprovedUserId = adminUserId;
            reconciliation.ApprovedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return (true, $"Đã phê duyệt và khóa sổ thành công bảng đối soát {reconciliation.Code}.");
        }
    }
}
