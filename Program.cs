using BeautyShop.Data;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// Tự động tìm chính xác file beautyshop.db trong thư mục dự án (tránh lệch thư mục khi chạy từ solution/root)
string dbPath = Path.Combine(builder.Environment.ContentRootPath, "beautyshop.db");
if (!File.Exists(dbPath))
{
    var subPath = Path.Combine(builder.Environment.ContentRootPath, "BeautyShop", "beautyshop.db");
    if (File.Exists(subPath))
    {
        dbPath = subPath;
    }
    else
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "beautyshop.db");
        if (File.Exists(basePath))
        {
            dbPath = basePath;
        }
    }
}
Console.WriteLine($"[Database] Sử dụng SQLite database tại: {Path.GetFullPath(dbPath)}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient<BeautyShop.Services.GhnService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BeautyShop.Services.ICurrentUserScope, BeautyShop.Services.CurrentUserScope>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, BeautyShop.Services.PermissionAuthorizationHandler>();
builder.Services.AddScoped<BeautyShop.Services.AprioriService>();
builder.Services.AddScoped<BeautyShop.Services.RecommendationService>();
builder.Services.AddScoped<BeautyShop.Services.IEmailService, BeautyShop.Services.EmailService>();
builder.Services.AddScoped<BeautyShop.Services.IProductService, BeautyShop.Services.ProductService>();
builder.Services.AddScoped<BeautyShop.Services.IInventoryService, BeautyShop.Services.InventoryService>();
builder.Services.AddScoped<BeautyShop.Services.ITransferService, BeautyShop.Services.TransferService>();
builder.Services.AddScoped<BeautyShop.Services.IDeliveryOptimizationService, BeautyShop.Services.DeliveryOptimizationService>();
builder.Services.AddHttpClient();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permCode in BeautyShop.Models.AppPermissions.All)
    {
        options.AddPolicy(permCode, policy =>
            policy.Requirements.Add(new BeautyShop.Services.PermissionRequirement(permCode)));
    }
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();

// ===== SEED ROLE, ADMIN VÀ TỰ ĐỘNG CẬP NHẬT ẢNH LOCAL =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Tự động tạo bảng nếu database chưa có
    db.Database.EnsureCreated();

    // Tự động tạo bảng StockReceipts nếu chưa có trong DB SQLite hiện hữu
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""StockReceipts"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_StockReceipts"" PRIMARY KEY AUTOINCREMENT,
                ""ReceiptCode"" TEXT NOT NULL,
                ""VariantId"" INTEGER NOT NULL,
                ""Quantity"" INTEGER NOT NULL,
                ""ImportPrice"" decimal(18,2) NOT NULL DEFAULT 0,
                ""Supplier"" TEXT NULL,
                ""ImportDate"" TEXT NOT NULL,
                ""CreatedByUserId"" INTEGER NULL,
                ""CreatedByName"" TEXT NULL,
                ""Note"" TEXT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                CONSTRAINT ""FK_StockReceipts_ProductVariants_VariantId"" FOREIGN KEY (""VariantId"") REFERENCES ""ProductVariants"" (""Id"") ON DELETE RESTRICT,
                CONSTRAINT ""FK_StockReceipts_Users_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_VariantId"" ON ""StockReceipts"" (""VariantId"");
            CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_CreatedByUserId"" ON ""StockReceipts"" (""CreatedByUserId"");

            CREATE TABLE IF NOT EXISTS ""Suppliers"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Suppliers"" PRIMARY KEY AUTOINCREMENT,
                ""Name"" TEXT NOT NULL,
                ""ContactPerson"" TEXT NULL,
                ""Email"" TEXT NULL,
                ""Phone"" TEXT NULL,
                ""Address"" TEXT NOT NULL,
                ""TaxCode"" TEXT NULL,
                ""Country"" TEXT NOT NULL DEFAULT 'Việt Nam',
                ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                ""Note"" TEXT NULL,
                ""CreatedAt"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_Suppliers_Name"" ON ""Suppliers"" (""Name"");
            CREATE INDEX IF NOT EXISTS ""IX_Suppliers_Phone"" ON ""Suppliers"" (""Phone"");

            CREATE TABLE IF NOT EXISTS ""SupplierProducts"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SupplierProducts"" PRIMARY KEY AUTOINCREMENT,
                ""SupplierId"" INTEGER NOT NULL,
                ""ProductId"" INTEGER NOT NULL,
                ""SuppliedPrice"" decimal(18,2) NULL,
                ""Notes"" TEXT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                CONSTRAINT ""FK_SupplierProducts_Suppliers_SupplierId"" FOREIGN KEY (""SupplierId"") REFERENCES ""Suppliers"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_SupplierProducts_Products_ProductId"" FOREIGN KEY (""ProductId"") REFERENCES ""Products"" (""Id"") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SupplierProducts_SupplierId_ProductId"" ON ""SupplierProducts"" (""SupplierId"", ""ProductId"");

            CREATE TABLE IF NOT EXISTS ""StockReceiptItems"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_StockReceiptItems"" PRIMARY KEY AUTOINCREMENT,
                ""StockReceiptId"" INTEGER NOT NULL,
                ""VariantId"" INTEGER NOT NULL,
                ""Quantity"" INTEGER NOT NULL,
                ""ImportPrice"" decimal(18,2) NOT NULL,
                ""TotalPrice"" decimal(18,2) NOT NULL,
                ""OldRetailPrice"" decimal(18,2) NULL,
                ""NewRetailPrice"" decimal(18,2) NULL,
                ""CreatedAt"" TEXT NOT NULL,
                CONSTRAINT ""FK_StockReceiptItems_StockReceipts_StockReceiptId"" FOREIGN KEY (""StockReceiptId"") REFERENCES ""StockReceipts"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_StockReceiptItems_ProductVariants_VariantId"" FOREIGN KEY (""VariantId"") REFERENCES ""ProductVariants"" (""Id"") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS ""IX_StockReceiptItems_StockReceiptId"" ON ""StockReceiptItems"" (""StockReceiptId"");
            CREATE INDEX IF NOT EXISTS ""IX_StockReceiptItems_VariantId"" ON ""StockReceiptItems"" (""VariantId"");
        ");

        // Cập nhật các cột mới cho StockReceipts nếu chưa có
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""SupplierId"" INTEGER NULL;"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""InvoiceNumber"" TEXT NULL;"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""OriginCountry"" TEXT NOT NULL DEFAULT 'Việt Nam';"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""PaymentStatus"" TEXT NOT NULL DEFAULT 'Paid';"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""TotalAmount"" decimal(18,2) NOT NULL DEFAULT 0;"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""PaidAmount"" decimal(18,2) NOT NULL DEFAULT 0;"); } catch {}
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""StockReceipts"" ADD COLUMN ""DebtAmount"" decimal(18,2) NOT NULL DEFAULT 0;"); } catch {}
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database] Lỗi khởi tạo bảng kho và nhà cung cấp: {ex.Message}");
    }

    // 1. Seed Roles
    var roleNames = new[] { "Admin", "Customer", "Employee" };
    foreach (var roleName in roleNames)
    {
        if (!db.Roles.Any(r => r.Name == roleName))
        {
            db.Roles.Add(new BeautyShop.Models.Role { Name = roleName });
        }
    }
    db.SaveChanges();

    // 2. Seed Tài khoản Admin
    var adminRole = db.Roles.FirstOrDefault(r => r.Name == "Admin");
    if (adminRole != null && !db.Users.Any(u => u.RoleId == adminRole.Id))
    {
        db.Users.Add(new BeautyShop.Models.User
        {
            FullName = "Nguyễn Minh Hoàng",
            Email = "admin@beautyshop.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = adminRole.Id,
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        db.SaveChanges();
    }

    // 3. Đảm bảo kho lưu trữ và cấu hình tỉnh/thành, miền, loại kho
    var mainStore = db.Stores.FirstOrDefault(s => s.Id == 1);
    if (mainStore != null)
    {
        mainStore.Name = "Kho tổng TP.HCM";
        mainStore.ProvinceName = "Thành phố Hồ Chí Minh";
        mainStore.ProvinceCode = "79";
        mainStore.Region = "Miền Nam";
        mainStore.StoreType = "Kho tổng (Central)";
    }
    else
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho tổng TP.HCM",
            Address = "Quận 1, TP. Hồ Chí Minh",
            ProvinceName = "Thành phố Hồ Chí Minh",
            ProvinceCode = "79",
            Region = "Miền Nam",
            StoreType = "Kho tổng (Central)",
            IsActive = true
        });
    }

    var storeLamDong = db.Stores.FirstOrDefault(s => s.ProvinceCode == "68");
    if (storeLamDong != null)
    {
        storeLamDong.Region = "Tây Nguyên";
        storeLamDong.StoreType = "Kho phân phối (Retail)";
    }
    else
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho Tây Nguyên (Lâm Đồng)",
            Address = "12 Phan Chu Trinh, Phường 9, TP. Đà Lạt",
            ProvinceName = "Tỉnh Lâm Đồng",
            ProvinceCode = "68",
            Region = "Tây Nguyên",
            StoreType = "Kho phân phối (Retail)",
            IsActive = true
        });
    }

    // Kho Hà Nội (có Cảng hàng không quốc tế Nội Bài)
    if (!db.Stores.Any(s => s.ProvinceCode == "01"))
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho tổng Miền Bắc (Hà Nội)",
            Address = "Khu công nghiệp Nội Bài, Sóc Sơn, TP. Hà Nội",
            ProvinceName = "Thành phố Hà Nội",
            ProvinceCode = "01",
            Region = "Miền Bắc",
            StoreType = "Kho tổng (Central)",
            IsActive = true
        });
    }

    // Kho Hải Phòng (có Cảng Quốc tế Lạch Huyện & Sân bay Cát Bi)
    if (!db.Stores.Any(s => s.ProvinceCode == "31"))
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho cảng Hải Phòng",
            Address = "Khu kinh tế Đình Vũ - Cát Hải, TP. Hải Phòng",
            ProvinceName = "Thành phố Hải Phòng",
            ProvinceCode = "31",
            Region = "Miền Bắc",
            StoreType = "Kho trung chuyển cảng (Transit)",
            IsActive = true
        });
    }

    // Kho Đà Nẵng (có Cảng Tiên Sa & Sân bay Quốc tế Đà Nẵng)
    if (!db.Stores.Any(s => s.ProvinceCode == "48"))
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho miền Trung (Đà Nẵng)",
            Address = "Khu công nghiệp Hòa Khánh, Liên Chiểu, TP. Đà Nẵng",
            ProvinceName = "Thành phố Đà Nẵng",
            ProvinceCode = "48",
            Region = "Miền Trung",
            StoreType = "Kho trung chuyển (Transit)",
            IsActive = true
        });
    }

    // Kho Cần Thơ (có Sân bay Quốc tế Cần Thơ)
    if (!db.Stores.Any(s => s.ProvinceCode == "92"))
    {
        db.Stores.Add(new BeautyShop.Models.Store
        {
            Name = "Kho Tây Nam Bộ (Cần Thơ)",
            Address = "Khu công nghiệp Trà Nóc, Bình Thủy, TP. Cần Thơ",
            ProvinceName = "Thành phố Cần Thơ",
            ProvinceCode = "92",
            Region = "Miền Nam",
            StoreType = "Kho phân phối (Retail)",
            IsActive = true
        });
    }

    db.SaveChanges();

    // 4. Seed vai trò chức năng, ma trận quyền (Permission), phạm vi kho (UserWarehouseScope) và chuyển đổi tài khoản cũ (Bước 6)
    try
    {
        BeautyShop.Services.RoleAndScopeSeeder.EnsureSeededAsync(db, app.Environment.IsDevelopment()).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RoleAndScopeSeeder] Lỗi seed dữ liệu phân quyền Bước 6: {ex.Message}");
    }

    // 5. Seed Nhà cung cấp và bảng Cung ứng trung gian
    try
    {
        BeautyShop.Services.SupplierSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SupplierSeeder] Lỗi seed nhà cung cấp và bảng cung ứng: {ex.Message}");
    }

    // ===== ĐỒNG BỘ CƯỠNG BỨC: GHI ĐÈ TOÀN BỘ URL ẢNH VỀ ĐƯỜNG DẪN NỘI BỘ =====
    try
    {
        var startLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var imageDirectory = Path.Combine(app.Environment.WebRootPath, "uploads", "products");

        var prefixConfig = new Dictionary<string, (int CategoryId, int Offset)>(StringComparer.OrdinalIgnoreCase)
        {
            { "son",       (1, 0)  },   // son1 -> ProductId 1
            { "cn",        (2, 0)  },   // cn11 -> ProductId 11
            { "srm",       (3, 20) },   // srm1 -> ProductId 21
            { "toner",     (4, 30) },   // toner1 -> ProductId 31
            { "serum",     (5, 40) },   // serum1 -> ProductId 41
            { "kemduong",  (6, 50) },   // kemduong1 -> ProductId 51
            { "trangdiem", (7, 60) },   // trangdiem1 -> ProductId 61
            { "tocthan",   (8, 70) },   // tocthan1 -> ProductId 71
        };

        var fileRegex = new Regex(
            @"^(?<prefix>[a-zA-Z]+?)(?<seq>\d+)(?:-bt(?<btIndex>\d+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Bước 1: Cache toàn bộ Products + Variants
        var allProducts = db.Products
            .Include(p => p.ProductVariants)
            .ToDictionary(p => p.Id);

        var variantsUpdatedLocally = new HashSet<int>();
        int cntThumb = 0, cntVariant = 0, cntSkip = 0;

        if (Directory.Exists(imageDirectory))
        {
            // Bước 2: Quét thư mục, ghi đè DB
            var imageFiles = Directory.GetFiles(imageDirectory, "*.*")
                .Where(f =>
                    f.EndsWith(".jpg",  StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png",  StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f);

            foreach (var filePath in imageFiles)
            {
                var fileName     = Path.GetFileName(filePath);
                var fileNameOnly = Path.GetFileNameWithoutExtension(fileName);
                var localPath    = $"/uploads/products/{fileName}";

                var match = fileRegex.Match(fileNameOnly);
                if (!match.Success) { cntSkip++; continue; }

                string prefix = match.Groups["prefix"].Value;

                if (!prefixConfig.TryGetValue(prefix, out var cfg))
                { cntSkip++; continue; }

                if (!int.TryParse(match.Groups["seq"].Value, out int fileSeq))
                { cntSkip++; continue; }

                int productId = fileSeq + cfg.Offset;

                if (!allProducts.TryGetValue(productId, out var product))
                { cntSkip++; continue; }

                if (product.CategoryId != cfg.CategoryId)
                {
                    startLog.LogWarning(
                        "[ForceSync] SKIP - '{File}': prefix '{Pre}' (Cat {EC}) != Product {Id} (Cat {AC})",
                        fileName, prefix, cfg.CategoryId, productId, product.CategoryId);
                    cntSkip++;
                    continue;
                }

                bool isBtFile = match.Groups["btIndex"].Success;

                if (isBtFile)
                {
                    if (!int.TryParse(match.Groups["btIndex"].Value, out int btIdx) || btIdx < 1)
                    { cntSkip++; continue; }

                    var sortedVariants = product.ProductVariants?.OrderBy(v => v.Id).ToList();
                    if (sortedVariants == null || btIdx > sortedVariants.Count)
                    { cntSkip++; continue; }

                    var tv = sortedVariants[btIdx - 1];
                    tv.ImageUrl = localPath;
                    variantsUpdatedLocally.Add(tv.Id);
                    cntVariant++;
                }
                else
                {
                    product.ThumbnailUrl = localPath;
                    cntThumb++;
                }
            }
        }
        else
        {
            startLog.LogWarning("[ForceSync] Thư mục ảnh không tồn tại: {Dir}", imageDirectory);
        }

        // Bước 3: Reset link ảnh ngoài
        int cntReset = 0;
        foreach (var prod in allProducts.Values)
        {
            if (prod.ProductVariants == null) continue;
            foreach (var v in prod.ProductVariants)
            {
                if (!variantsUpdatedLocally.Contains(v.Id)
                    && !string.IsNullOrWhiteSpace(v.ImageUrl)
                    && (v.ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        v.ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    v.ImageUrl = null;
                    cntReset++;
                }
            }
        }

        // Bước 4: Đảm bảo Variant có Inventories
        var variantsWithoutInv = db.ProductVariants
            .Where(v => !db.Inventories.Any(i => i.VariantId == v.Id))
            .ToList();

        foreach (var v in variantsWithoutInv)
        {
            db.Inventories.Add(new BeautyShop.Models.Inventory
            {
                StoreId          = 1,
                VariantId        = v.Id,
                StockQuantity    = 50,
                ReservedQuantity = 0
            });
        }

        db.SaveChanges();

        startLog.LogInformation(
            "[ForceSync] Hoàn tất - ThumbnailUrl: {T} | Variant ImageUrl local: {V} | " +
            "Link ngoài reset: {R} | Inventory tạo mới: {I} | Bỏ qua: {S}",
            cntThumb, cntVariant, cntReset, variantsWithoutInv.Count, cntSkip);
    }
    catch (Exception ex)
    {
        var errLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        errLog.LogError(ex, "[ForceSync] Lỗi khi đồng bộ cưỡng bức ảnh sản phẩm.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();