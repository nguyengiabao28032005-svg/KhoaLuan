using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.PaymentProcess)]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GhnService _ghn;
        private readonly IEmailService _emailService;
        private readonly ICurrentUserScope _userScope;
        private readonly IDeliveryOptimizationService _deliveryService;

        public OrdersController(
            ApplicationDbContext context,
            GhnService ghn,
            IEmailService emailService,
            ICurrentUserScope userScope,
            IDeliveryOptimizationService deliveryService)
        {
            _context = context;
            _ghn = ghn;
            _emailService = emailService;
            _userScope = userScope;
            _deliveryService = deliveryService;
        }

        public async Task<IActionResult> Index(string searchCode, string status, int? storeId)
        {
            var query = _context.Orders
                .Include(o => o.Store)
                .AsQueryable();

            // Bộ lọc phạm vi dữ liệu tập trung theo kho xuất hàng (Order.StoreId)
            query = await _userScope.ApplyOrderScopeAsync(query);

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(x => x.StoreId == storeId.Value);
            }

            // Tìm kiếm theo mã đơn hàng hoặc tên khách hàng hoặc SĐT
            if (!string.IsNullOrEmpty(searchCode))
            {
                query = query.Where(x =>
                    x.OrderCode.Contains(searchCode) ||
                    x.CustomerName.Contains(searchCode) ||
                    x.PhoneNumber.Contains(searchCode));
            }

            // Lọc theo trạng thái đơn hàng
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(x => x.OrderStatus == status);
            }

            ViewBag.CurrentSearchCode = searchCode;
            ViewBag.CurrentStatus = status;
            ViewBag.SelectedStoreId = storeId;

            var orders = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string paymentStatus, string orderStatus)
        {
            var order = await _context.Orders
                .Include(x => x.OrderDetails!)
                    .ThenInclude(x => x.Variant!)
                        .ThenInclude(x => x!.Product)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                return RedirectToAction(nameof(Index));

            // Kiểm tra phạm vi kho của đơn hàng (chặn truy cập trực tiếp ID đơn của kho khác)
            if (!await _userScope.IsAdminAsync())
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                int effectiveStoreId = order.StoreId ?? 1;
                if (!allowedStores.Contains(effectiveStoreId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    ViewBag.RequiredPermission = "payment.process (Ngoài phạm vi kho của đơn hàng)";
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            string oldStatus = order.OrderStatus;

            // 1. Cập nhật trạng thái thanh toán
            order.PaymentStatus = paymentStatus;

            // 2. Cập nhật trạng thái vận đơn & Lưu lịch sử nếu có thay đổi
            if (oldStatus != orderStatus)
            {
                order.OrderStatus = orderStatus;

                _context.OrderHistories.Add(new OrderHistory
                {
                    OrderId = order.Id,
                    OldStatus = oldStatus,
                    NewStatus = orderStatus,
                    CreatedAt = DateTime.Now
                });

                // Nếu đơn bị hủy khi đang nằm trong chuyến giao -> tự động gỡ khỏi chuyến và cập nhật tổng của chuyến (Bước 5.6)
                if (orderStatus == "Cancelled" || orderStatus == "Hủy bỏ đơn" || orderStatus == "Đã hủy")
                {
                    await _deliveryService.CancelOrderAndRemoveFromTripAsync(order.Id, "Hủy đơn hàng từ màn hình quản lý đơn");
                }

                // ============================================================
                // TRỪ TỒN KHO KHI ADMIN XÁC NHẬN/DUYỆT ĐƠN HÀNG
                // ============================================================
                var unapprovedStatuses = new[] { "Pending", "Cho xac nhan", "Chờ xác nhận", "Chờ duyệt" };
                bool wasUnapproved = unapprovedStatuses.Contains(oldStatus);
                bool isNowApproved = !unapprovedStatuses.Contains(orderStatus) && orderStatus != "Cancelled" && orderStatus != "Hủy bỏ đơn" && orderStatus != "Đã hủy";

                int targetStoreId = order.StoreId ?? 1;

                if (wasUnapproved && isNowApproved)
                {
                    foreach (var detail in order.OrderDetails ?? new List<OrderDetail>())
                    {
                        var inv = await _context.Inventories.FirstOrDefaultAsync(i => i.VariantId == detail.VariantId && i.StoreId == targetStoreId)
                                  ?? await _context.Inventories.FirstOrDefaultAsync(i => i.VariantId == detail.VariantId);
                        if (inv == null)
                        {
                            inv = new Inventory
                            {
                                StoreId = targetStoreId,
                                VariantId = detail.VariantId,
                                StockQuantity = 0,
                                ReservedQuantity = 0
                            };
                            _context.Inventories.Add(inv);
                            await _context.SaveChangesAsync();
                        }

                        int deduct = Math.Min(detail.Quantity, inv.StockQuantity);
                        inv.StockQuantity -= deduct;

                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            InventoryId = inv.Id,
                            ChangeType = "OrderApproved",
                            QuantityChanged = -deduct,
                            Note = $"Duyệt đơn hàng #{order.OrderCode}",
                            CreatedAt = DateTime.Now
                        });
                    }
                }
                else if (!wasUnapproved && (orderStatus == "Cancelled" || orderStatus == "Hủy bỏ đơn" || orderStatus == "Đã hủy"))
                {
                    // NẾU HỦY ĐƠN ĐÃ DUYỆT -> CỘNG HOÀN KHO
                    foreach (var detail in order.OrderDetails ?? new List<OrderDetail>())
                    {
                        var inv = await _context.Inventories.FirstOrDefaultAsync(i => i.VariantId == detail.VariantId && i.StoreId == targetStoreId)
                                  ?? await _context.Inventories.FirstOrDefaultAsync(i => i.VariantId == detail.VariantId);
                        if (inv != null)
                        {
                            inv.StockQuantity += detail.Quantity;

                            _context.InventoryLogs.Add(new InventoryLog
                            {
                                InventoryId = inv.Id,
                                ChangeType = "OrderCancelled",
                                QuantityChanged = detail.Quantity,
                                Note = $"Hủy đơn hàng đã duyệt #{order.OrderCode}",
                                CreatedAt = DateTime.Now
                            });
                        }
                    }
                }

                // ============================================================
                // KHI ADMIN XÁC NHẬN ĐƠN → TỰ ĐỘNG TẠO ĐƠN TRÊN GHN
                // ============================================================
                if (orderStatus == "Confirmed" && string.IsNullOrEmpty(order.GhnOrderCode))
                {
                    try
                    {
                        int totalWeight = (order.OrderDetails?.Sum(x => x.Quantity) ?? 1) * 300;
                        if (totalWeight < 1) totalWeight = 300;

                        string? ghnCode = await _ghn.CreateOrderAsync(
                            toName: order.CustomerName,
                            toPhone: order.PhoneNumber,
                            toAddress: order.ShippingAddress,
                            toDistrictId: order.ToDistrictId > 0 ? order.ToDistrictId : 1442,
                            toWardCode: !string.IsNullOrEmpty(order.ToWardCode) ? order.ToWardCode : "21307",
                            codAmount: order.PaymentMethod == "COD" ? (int)order.FinalAmount : 0,
                            weightGram: totalWeight,
                            clientOrderCode: order.OrderCode
                        );

                        if (!string.IsNullOrEmpty(ghnCode))
                        {
                            order.GhnOrderCode = ghnCode;
                        }
                    }
                    catch
                    {
                    }
                }

                // ============================================================
                // GỬI EMAIL THÔNG BÁO CẬP NHẬT TRẠNG THÁI TỰ ĐỘNG
                // ============================================================
                var userObj = order.UserId.HasValue ? await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId.Value) : null;
                string recipientEmail = userObj?.Email ?? "";
                if (!string.IsNullOrEmpty(recipientEmail))
                {
                    _ = _emailService.SendOrderStatusUpdateEmailAsync(order, recipientEmail, oldStatus, orderStatus);
                }
            }

            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(x => x.Store)
                .Include(x => x.OrderDetails!)
                .ThenInclude(x => x.Variant)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (order == null)
                return RedirectToAction(nameof(Index));

            // Chặn truy cập trực tiếp bằng ID vào đơn hàng của kho khác
            if (!await _userScope.IsAdminAsync())
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                int effectiveStoreId = order.StoreId ?? 1;
                if (!allowedStores.Contains(effectiveStoreId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    ViewBag.RequiredPermission = "payment.process (Ngoài phạm vi kho của đơn hàng)";
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            return View(order);
        }
    }
}