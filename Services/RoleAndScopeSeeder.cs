using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    /// <summary>
    /// Dịch vụ khởi tạo Vai trò chức năng, Ma trận Quyền (RolePermission),
    /// và chuyển đổi an toàn người dùng cũ sang mô hình phân quyền theo chức năng + phạm vi kho (Bước 6).
    /// </summary>
    public static class RoleAndScopeSeeder
    {
        public static async Task EnsureSeededAsync(ApplicationDbContext db, bool isDevelopment = false)
        {
            // 1. Đảm bảo 14 quyền chức năng chuẩn tồn tại
            var defaultPermissions = new List<(string Code, string Name, string Category, string Desc)>
            {
                (AppPermissions.ProductManage, "Quản lý sản phẩm", "Sản phẩm", "Xem, thêm, sửa, xóa sản phẩm, danh mục và thương hiệu"),
                (AppPermissions.PriceSet, "Thiết lập giá bán", "Giá & Khuyến mãi", "Cấu hình giá bán lẻ, giá bán sỉ, voucher và chương trình Flash Sale"),
                (AppPermissions.PortManage, "Quản lý cảng nhập khẩu", "Kho bãi", "Thêm, sửa, bật/tắt cảng nhập khẩu hợp lệ trong danh mục quốc gia"),
                (AppPermissions.RouteManage, "Cấu hình tuyến chuyển kho", "Kho bãi", "Cấu hình tuyến luân chuyển liên tỉnh và kho trung chuyển (Hub)"),
                (AppPermissions.InventoryView, "Xem tồn kho", "Kho bãi", "Xem số lượng tồn kho thực tế, hàng đang giữ và lịch sử biến động kho thuộc phạm vi"),
                (AppPermissions.InventoryReceive, "Lập phiếu nhập kho", "Kho bãi", "Lập phiếu nhập khẩu theo cảng và nhập hàng nội địa cho các kho thuộc phạm vi"),
                (AppPermissions.InventoryTransfer, "Luân chuyển kho", "Kho bãi", "Tạo phiếu xuất chuyển kho và xác nhận nhận hàng chuyển kho thuộc phạm vi"),
                (AppPermissions.DeliveryDispatch, "Điều phối chuyến giao", "Giao hàng", "Gom chuyến giao tự động, gán nhân viên giao hàng và sắp xếp thứ tự điểm dừng"),
                (AppPermissions.DeliveryExecute, "Thực hiện giao hàng", "Giao hàng", "Xem chuyến giao của chính mình, cập nhật trạng thái điểm dừng và ghi nhận tiền thu hộ COD"),
                (AppPermissions.PaymentProcess, "Thanh toán đơn hàng", "Thu ngân", "Xử lý đơn hàng, xác nhận thanh toán và cập nhật trạng thái đơn trong phạm vi kho"),
                (AppPermissions.CashierReconcile, "Quyết toán & Đối soát COD", "Thu ngân", "Nhận tiền COD từ nhân viên giao hàng, quyết toán chuyến và lập bảng đối soát cuối ngày nộp Admin"),
                (AppPermissions.ReportView, "Xem báo cáo kinh doanh", "Báo cáo", "Xem thống kê doanh thu, sản phẩm bán chạy và sản phẩm bán chậm trong phạm vi"),
                (AppPermissions.UserManage, "Quản lý nhân viên", "Hệ thống", "Tạo tài khoản nhân viên, gán vai trò, phân công phạm vi kho và khóa/mở tài khoản"),
                (AppPermissions.RoleManage, "Quản lý vai trò & Phân quyền", "Hệ thống", "Xem và cấu hình ma trận Vai trò x Quyền, tra cứu nhật ký thay đổi phân quyền (AuditLog)")
            };

            foreach (var p in defaultPermissions)
            {
                var existingPerm = await db.Permissions.FirstOrDefaultAsync(x => x.Code == p.Code);
                if (existingPerm == null)
                {
                    db.Permissions.Add(new Permission
                    {
                        Code = p.Code,
                        Name = p.Name,
                        Category = p.Category,
                        Description = p.Desc
                    });
                }
            }
            await db.SaveChangesAsync();

            // 2. Đảm bảo các Vai trò chức năng tồn tại (Admin, Customer, Warehouse, Cashier, Shipper)
            var roleDefinitions = new List<(string Name, string DisplayName, string Description)>
            {
                (AppRoles.Admin, "Quản trị viên (Admin)", "Chủ cửa hàng/quản trị: quản lý sản phẩm, thiết lập giá bán, danh mục cảng, tuyến chuyển kho, lập chuyến giao, quản lý nhân viên và vai trò, xem mọi báo cáo, nhận tiền cuối ngày."),
                (AppRoles.Customer, "Khách hàng", "Khách hàng mua sắm trực tuyến trên hệ thống."),
                (AppRoles.Warehouse, "Nhân viên kho", "Phụ trách nghiệp vụ kho tại các kho được phân công: xem tồn kho, lập phiếu nhập hàng, lập và xác nhận nhận phiếu chuyển kho."),
                (AppRoles.Cashier, "Thu ngân", "Thanh toán đơn hàng, nhận tiền COD từ nhân viên giao hàng, đối soát chuyến, xem báo cáo bán chạy/bán chậm và nộp tiền cuối ngày cho Admin."),
                (AppRoles.Shipper, "Nhân viên giao hàng", "Chỉ xem và cập nhật chuyến giao của chính mình, ghi nhận tiền thu hộ COD và nộp tiền cho thu ngân.")
            };

            foreach (var rd in roleDefinitions)
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == rd.Name);
                if (role == null)
                {
                    db.Roles.Add(new Role
                    {
                        Name = rd.Name,
                        DisplayName = rd.DisplayName,
                        Description = rd.Description
                    });
                }
                else
                {
                    role.DisplayName = rd.DisplayName;
                    role.Description = rd.Description;
                }
            }
            await db.SaveChangesAsync();

            var allPerms = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);
            var allRoles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r);

            // 3. Seed Ma trận Vai trò x Quyền mặc định (nếu vai trò chưa có quyền nào)
            var roleMatrix = new Dictionary<string, string[]>
            {
                [AppRoles.Admin] = AppPermissions.All,
                [AppRoles.Warehouse] = new[]
                {
                    AppPermissions.InventoryView,
                    AppPermissions.InventoryReceive,
                    AppPermissions.InventoryTransfer
                },
                [AppRoles.Cashier] = new[]
                {
                    AppPermissions.PaymentProcess,
                    AppPermissions.CashierReconcile,
                    AppPermissions.ReportView
                },
                [AppRoles.Shipper] = new[]
                {
                    AppPermissions.DeliveryExecute
                }
            };

            foreach (var kvp in roleMatrix)
            {
                if (!allRoles.TryGetValue(kvp.Key, out var roleObj)) continue;

                var existingPermIds = await db.RolePermissions
                    .Where(rp => rp.RoleId == roleObj.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                if (!existingPermIds.Any())
                {
                    foreach (var code in kvp.Value)
                    {
                        if (allPerms.TryGetValue(code, out int permId))
                        {
                            db.RolePermissions.Add(new RolePermission
                            {
                                RoleId = roleObj.Id,
                                PermissionId = permId
                            });
                        }
                    }
                }
            }
            await db.SaveChangesAsync();

            // 4. Chuyển đổi dữ liệu người dùng cũ (Role Employee hoặc role theo vùng miền) sang vai trò chức năng mới
            var usersToCheck = await db.Users.Include(u => u.Role).ToListAsync();
            int defaultStoreId = await db.Stores.OrderBy(s => s.Id).Select(s => s.Id).FirstOrDefaultAsync();
            if (defaultStoreId == 0) defaultStoreId = 1;

            int hanoiStoreId = await db.Stores
                .Where(s => s.ProvinceCode == "01" || s.Name.Contains("Hà Nội"))
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            if (hanoiStoreId == 0) hanoiStoreId = defaultStoreId;

            foreach (var u in usersToCheck)
            {
                string currentRoleName = u.Role?.Name ?? "";
                string pos = (u.Position ?? "").Trim();

                // Admin: Đảm bảo không lưu dòng dư thừa trong UserWarehouseScopes (Admin mặc định IsAdmin = true)
                if (currentRoleName == AppRoles.Admin)
                {
                    var adminScopes = await db.UserWarehouseScopes.Where(s => s.UserId == u.Id).ToListAsync();
                    if (adminScopes.Any())
                    {
                        db.UserWarehouseScopes.RemoveRange(adminScopes);
                    }
                    continue;
                }

                if (currentRoleName == AppRoles.Customer)
                {
                    continue;
                }

                // Nếu đang ở Role "Employee" cũ hoặc Role theo miền ("Kho miền Bắc", "Kho miền Nam"...)
                bool isLegacyOrRegionalRole = currentRoleName == AppRoles.LegacyEmployee
                    || currentRoleName.Contains("Miền", StringComparison.OrdinalIgnoreCase)
                    || currentRoleName.Contains("Kho ", StringComparison.OrdinalIgnoreCase);

                if (isLegacyOrRegionalRole)
                {
                    string targetRoleName;
                    int? targetStoreScope = null;

                    if (pos.Equals("Shipper", StringComparison.OrdinalIgnoreCase)
                        || u.Email.Contains("shipper", StringComparison.OrdinalIgnoreCase)
                        || u.Email.Equals("hoang@gmail.com", StringComparison.OrdinalIgnoreCase))
                    {
                        targetRoleName = AppRoles.Shipper;
                        u.Position = "Nhân viên giao hàng";
                        if (u.FullName.Contains("(Shipper Tây Bắc)"))
                        {
                            u.FullName = u.FullName.Replace("(Shipper Tây Bắc)", "").Trim();
                        }
                    }
                    else if (pos.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
                        || u.Email.Contains("thungan", StringComparison.OrdinalIgnoreCase))
                    {
                        targetRoleName = AppRoles.Cashier;
                        u.Position = "Thu ngân";
                        targetStoreScope = currentRoleName.Contains("Bắc", StringComparison.OrdinalIgnoreCase) ? hanoiStoreId : defaultStoreId;
                    }
                    else
                    {
                        targetRoleName = AppRoles.Warehouse;
                        u.Position = "Nhân viên kho";
                        targetStoreScope = currentRoleName.Contains("Bắc", StringComparison.OrdinalIgnoreCase) ? hanoiStoreId : defaultStoreId;
                    }

                    if (allRoles.TryGetValue(targetRoleName, out var newRole))
                    {
                        string oldRoleDisplay = currentRoleName;
                        u.RoleId = newRole.Id;
                        u.SecurityStamp = Guid.NewGuid().ToString();

                        db.AuditLogs.Add(new AuditLog
                        {
                            PerformedByUserName = "System Migration",
                            Action = "MigrateLegacyUserRole",
                            TargetUserId = u.Id,
                            TargetUserName = u.FullName,
                            OldValue = $"Role: {oldRoleDisplay}, Position: {pos}",
                            NewValue = $"Role: {targetRoleName}" + (targetStoreScope.HasValue ? $", StoreId: {targetStoreScope.Value}" : ""),
                            Note = "Chuyển đổi tự động tài khoản cũ sang vai trò chức năng và phạm vi kho (Bước 6)",
                            CreatedAt = DateTime.Now
                        });
                    }

                    if (targetStoreScope.HasValue && await db.Stores.AnyAsync(s => s.Id == targetStoreScope.Value))
                    {
                        bool hasScope = await db.UserWarehouseScopes.AnyAsync(s => s.UserId == u.Id && s.StoreId == targetStoreScope.Value);
                        if (!hasScope)
                        {
                            db.UserWarehouseScopes.Add(new UserWarehouseScope
                            {
                                UserId = u.Id,
                                StoreId = targetStoreScope.Value,
                                AssignedAt = DateTime.Now
                            });
                        }
                    }
                }
            }
            await db.SaveChangesAsync();

            // Đảm bảo thu ngân hiện hữu có ít nhất 1 phạm vi kho
            if (allRoles.TryGetValue(AppRoles.Cashier, out var cashierRole))
            {
                var cashiers = await db.Users.Where(u => u.RoleId == cashierRole.Id).ToListAsync();
                foreach (var c in cashiers)
                {
                    bool hasAnyScope = await db.UserWarehouseScopes.AnyAsync(s => s.UserId == c.Id);
                    if (!hasAnyScope && await db.Stores.AnyAsync(s => s.Id == defaultStoreId))
                    {
                        int storeToAssign = c.Email.Contains("_hn") ? hanoiStoreId : defaultStoreId;
                        db.UserWarehouseScopes.Add(new UserWarehouseScope
                        {
                            UserId = c.Id,
                            StoreId = storeToAssign,
                            AssignedAt = DateTime.Now
                        });
                    }
                }
                await db.SaveChangesAsync();
            }

            // Xóa các role vùng miền không hợp lệ nếu không còn user nào gắn vào
            var invalidRegionalRoles = await db.Roles
                .Where(r => r.Name.Contains("Miền") || r.Name.Contains("Kho miền"))
                .ToListAsync();
            foreach (var regRole in invalidRegionalRoles)
            {
                if (!await db.Users.AnyAsync(u => u.RoleId == regRole.Id))
                {
                    db.Roles.Remove(regRole);
                }
            }
            await db.SaveChangesAsync();

            // 5. Trong môi trường Development: Seed tài khoản mẫu phục vụ demo (Admin, Thu ngân HCM & Hà Nội, Nhân viên kho HCM & HN, Shipper)
            if (isDevelopment && await db.Stores.AnyAsync())
            {
                string devPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

                // Admin mặc định (nếu chưa có)
                if (allRoles.TryGetValue(AppRoles.Admin, out var admRole) && !await db.Users.AnyAsync(u => u.Email == "admin@beautyshop.com"))
                {
                    db.Users.Add(new User
                    {
                        FullName = "Nguyễn Minh Hoàng",
                        Email = "admin@beautyshop.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        PhoneNumber = "0900000001",
                        RoleId = admRole.Id,
                        Position = "Admin",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                }

                // Thu ngân thứ 1 tại Kho TP.HCM (StoreId = defaultStoreId)
                if (allRoles.TryGetValue(AppRoles.Cashier, out var cRole1) && !await db.Users.AnyAsync(u => u.Email == "thungan@beautyshop.com"))
                {
                    var cashierHcm = new User
                    {
                        FullName = "Phạm Thị Mai Hương",
                        Email = "thungan@beautyshop.com",
                        PasswordHash = devPasswordHash,
                        PhoneNumber = "0912345601",
                        RoleId = cRole1.Id,
                        Position = "Thu ngân",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.Users.Add(cashierHcm);
                    await db.SaveChangesAsync();

                    db.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = cashierHcm.Id,
                        StoreId = defaultStoreId,
                        AssignedAt = DateTime.Now
                    });
                }

                // Thu ngân thứ 2 tại Kho Hà Nội (StoreId = hanoiStoreId)
                if (allRoles.TryGetValue(AppRoles.Cashier, out var cRole) && !await db.Users.AnyAsync(u => u.Email == "thungan_hn@beautyshop.com"))
                {
                    var cashierHn = new User
                    {
                        FullName = "Nguyễn Ngọc Lan Anh",
                        Email = "thungan_hn@beautyshop.com",
                        PasswordHash = devPasswordHash,
                        PhoneNumber = "0912345602",
                        RoleId = cRole.Id,
                        Position = "Thu ngân",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.Users.Add(cashierHn);
                    await db.SaveChangesAsync();

                    db.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = cashierHn.Id,
                        StoreId = hanoiStoreId,
                        AssignedAt = DateTime.Now
                    });
                }

                // Shipper 1 & Shipper 2 (nếu chưa có)
                if (allRoles.TryGetValue(AppRoles.Shipper, out var sRole))
                {
                    if (!await db.Users.AnyAsync(u => u.Email == "shipper1@beautyshop.com"))
                    {
                        var sh1 = new User
                        {
                            FullName = "Đặng Văn Khánh",
                            Email = "shipper1@beautyshop.com",
                            PasswordHash = devPasswordHash,
                            PhoneNumber = "0909111222",
                            RoleId = sRole.Id,
                            Position = "Nhân viên giao hàng",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        db.Users.Add(sh1);
                        await db.SaveChangesAsync();
                        db.UserWarehouseScopes.Add(new UserWarehouseScope { UserId = sh1.Id, StoreId = defaultStoreId, AssignedAt = DateTime.Now });
                    }

                    if (!await db.Users.AnyAsync(u => u.Email == "shipper2@beautyshop.com"))
                    {
                        var sh2 = new User
                        {
                            FullName = "Trần Quốc Tuấn",
                            Email = "shipper2@beautyshop.com",
                            PasswordHash = devPasswordHash,
                            PhoneNumber = "0909333444",
                            RoleId = sRole.Id,
                            Position = "Nhân viên giao hàng",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        db.Users.Add(sh2);
                        await db.SaveChangesAsync();
                        db.UserWarehouseScopes.Add(new UserWarehouseScope { UserId = sh2.Id, StoreId = defaultStoreId, AssignedAt = DateTime.Now });
                    }
                }

                // Nhân viên kho tại TP.HCM (StoreId = defaultStoreId)
                if (allRoles.TryGetValue(AppRoles.Warehouse, out var wRole) && !await db.Users.AnyAsync(u => u.Email == "kho_hcm@beautyshop.com"))
                {
                    var whHcm = new User
                    {
                        FullName = "Lê Đức Mạnh",
                        Email = "kho_hcm@beautyshop.com",
                        PasswordHash = devPasswordHash,
                        PhoneNumber = "0912345603",
                        RoleId = wRole.Id,
                        Position = "Nhân viên kho",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.Users.Add(whHcm);
                    await db.SaveChangesAsync();

                    db.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = whHcm.Id,
                        StoreId = defaultStoreId,
                        AssignedAt = DateTime.Now
                    });
                }

                // Nhân viên kho tại Hà Nội (StoreId = hanoiStoreId)
                if (allRoles.TryGetValue(AppRoles.Warehouse, out var wRole2) && !await db.Users.AnyAsync(u => u.Email == "kho_hn@beautyshop.com"))
                {
                    var whHn = new User
                    {
                        FullName = "Vũ Thị Thanh Thảo",
                        Email = "kho_hn@beautyshop.com",
                        PasswordHash = devPasswordHash,
                        PhoneNumber = "0912345604",
                        RoleId = wRole2.Id,
                        Position = "Nhân viên kho",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.Users.Add(whHn);
                    await db.SaveChangesAsync();

                    db.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = whHn.Id,
                        StoreId = hanoiStoreId,
                        AssignedAt = DateTime.Now
                    });
                }

                await db.SaveChangesAsync();
            }

            // Chuẩn hóa họ tên nhân viên hiện có trong CSDL sang tên người thật (bỏ tên đặt theo chức danh/vùng miền như "Lê Văn Kho", "Phạm Thị Thu Ngân", "Lê Văn Giao"...)
            var realisticEmployeeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin@beautyshop.com"] = "Nguyễn Minh Hoàng",
                ["thungan@beautyshop.com"] = "Phạm Thị Mai Hương",
                ["thungan_hn@beautyshop.com"] = "Nguyễn Ngọc Lan Anh",
                ["kho_hcm@beautyshop.com"] = "Lê Đức Mạnh",
                ["kho_hn@beautyshop.com"] = "Vũ Thị Thanh Thảo",
                ["shipper1@beautyshop.com"] = "Đặng Văn Khánh",
                ["shipper2@beautyshop.com"] = "Trần Quốc Tuấn",
                ["hoang@gmail.com"] = "Hoàng Gia Bảo"
            };

            var staffToRename = await db.Users
                .Where(u => realisticEmployeeNames.Keys.Contains(u.Email))
                .ToListAsync();

            foreach (var staff in staffToRename)
            {
                if (realisticEmployeeNames.TryGetValue(staff.Email, out string? realisticName) &&
                    !string.IsNullOrEmpty(realisticName) &&
                    staff.FullName != realisticName)
                {
                    staff.FullName = realisticName;
                }
            }
            await db.SaveChangesAsync();

            // 6. Đảm bảo các đơn hàng, chuyến giao, bảng đối soát cũ chưa có StoreId được gán StoreId mặc định (1 - Kho tổng TP.HCM)
            var ordersWithoutStore = await db.Orders.Where(o => o.StoreId == null).ToListAsync();
            if (ordersWithoutStore.Any() && await db.Stores.AnyAsync(s => s.Id == defaultStoreId))
            {
                foreach (var ord in ordersWithoutStore)
                {
                    ord.StoreId = DetermineStoreIdFromAddress(ord.ShippingAddress, defaultStoreId, hanoiStoreId);
                }
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Quy tắc xác định đơn hàng thuộc Kho xuất hàng nào dựa trên địa chỉ giao:
        /// - Địa chỉ tại Hà Nội -> Kho tổng Miền Bắc (Hà Nội, StoreId = 3)
        /// - Địa chỉ tại Hải Phòng -> Kho cảng Hải Phòng (StoreId = 4)
        /// - Địa chỉ tại Đà Nẵng / Miền Trung -> Kho miền Trung (Đà Nẵng, StoreId = 5)
        /// - Địa chỉ tại Lâm Đồng / Đà Lạt -> Kho Tây Nguyên (Lâm Đồng, StoreId = 2)
        /// - Địa chỉ tại Cần Thơ -> Kho Tây Nam Bộ (Cần Thơ, StoreId = 6)
        /// - Mặc định / TP.HCM -> Kho tổng TP.HCM (StoreId = 1)
        /// </summary>
        public static int DetermineStoreIdFromAddress(string? shippingAddress, int defaultStoreId = 1, int hanoiStoreId = 3)
        {
            if (string.IsNullOrWhiteSpace(shippingAddress)) return defaultStoreId;
            string addr = shippingAddress.ToLower();

            if (addr.Contains("hà nội") || addr.Contains("ha noi") || addr.Contains("hoàn kiếm") || addr.Contains("cầu giấy") || addr.Contains("đống đa") || addr.Contains("ba đình") || addr.Contains("nội bài"))
                return hanoiStoreId;
            if (addr.Contains("hải phòng") || addr.Contains("hai phong"))
                return 4;
            if (addr.Contains("đà nẵng") || addr.Contains("da nang") || addr.Contains("liên chiểu") || addr.Contains("hải châu"))
                return 5;
            if (addr.Contains("lâm đồng") || addr.Contains("đà lạt") || addr.Contains("đức trọng") || addr.Contains("liên khương"))
                return 2;
            if (addr.Contains("cần thơ") || addr.Contains("can tho") || addr.Contains("ninh kiều") || addr.Contains("bình thủy"))
                return 6;

            return defaultStoreId;
        }
    }
}
