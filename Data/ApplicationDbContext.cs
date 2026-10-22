using BeautyShop.Models;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Brand> Brands { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<Role> Roles { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<UserAddress> UserAddresses { get; set; }

        public DbSet<Store> Stores { get; set; }

        public DbSet<Inventory> Inventories { get; set; }

        public DbSet<InventoryLog> InventoryLogs { get; set; }
        public DbSet<Cart> Carts { get; set; }

        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderDetail> OrderDetails { get; set; }

        public DbSet<OrderHistory> OrderHistories { get; set; }
        public DbSet<Membership> Memberships { get; set; }

        public DbSet<PointHistory> PointHistories { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }

        public DbSet<FlashSale> FlashSales { get; set; }
        public DbSet<FlashSaleItem> FlashSaleItems { get; set; }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }

        public DbSet<ProductView> ProductViews { get; set; }
        public DbSet<UserBehavior> UserBehaviors { get; set; }
        public DbSet<ProductRecommendation> ProductRecommendations { get; set; }

        public DbSet<Consultation> Consultations { get; set; }
        public DbSet<ConsultationReview> ConsultationReviews { get; set; }
        public DbSet<ConsultationProduct> ConsultationProducts { get; set; }
        public DbSet<StockReceipt> StockReceipts { get; set; }
        public DbSet<ImportPort> ImportPorts { get; set; }
        public DbSet<TransferRoute> TransferRoutes { get; set; }
        public DbSet<StockTransfer> StockTransfers { get; set; }
        public DbSet<StockTransferLine> StockTransferLines { get; set; }

        // ===== QUẢN LÝ GIAO HÀNG TỐI ƯU & ĐỐI SOÁT (BƯỚC 5) =====
        public DbSet<DeliveryZone> DeliveryZones { get; set; }
        public DbSet<DeliveryTrip> DeliveryTrips { get; set; }
        public DbSet<DeliveryTripStop> DeliveryTripStops { get; set; }
        public DbSet<DailyReconciliation> DailyReconciliations { get; set; }

        // ===== PHÂN QUYỀN THEO CHỨC NĂNG & PHẠM VI DỮ LIỆU KHO (BƯỚC 6) =====
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserWarehouseScope> UserWarehouseScopes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => v.SKU)
                .IsUnique();

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => v.Barcode)
                .IsUnique();

            modelBuilder.Entity<Category>()
    .HasOne(c => c.ParentCategory)
    .WithMany(c => c.ChildCategories)
    .HasForeignKey(c => c.ParentCategoryId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<Inventory>()
        .HasIndex(i => new { i.StoreId, i.VariantId })
        .IsUnique();
            modelBuilder.Entity<Inventory>()
    .Property(i => i.RowVersion)
    .IsConcurrencyToken();

            modelBuilder.Entity<ProductVariant>()
                .Property(x => x.RetailPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired(false);

            modelBuilder.Entity<ProductVariant>()
                .Property(x => x.OriginalPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired(false);

            modelBuilder.Entity<ProductVariant>()
                .Property(x => x.WholesalePrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired(false);

            modelBuilder.Entity<Store>()
                .Property(x => x.Latitude)
                .HasColumnType("decimal(10,8)");

            modelBuilder.Entity<Store>()
                .Property(x => x.Longitude)
                .HasColumnType("decimal(11,8)");

            modelBuilder.Entity<Order>()
                .Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(x => x.DeliveryFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(x => x.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(x => x.FinalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderDetail>()
                .Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Membership>()
    .HasKey(m => m.UserId);

            modelBuilder.Entity<Membership>()
                .HasOne(m => m.User)
                .WithOne(u => u.Membership)
                .HasForeignKey<Membership>(m => m.UserId);

            modelBuilder.Entity<PointHistory>()
                .HasOne(p => p.Membership)
                .WithMany()
                .HasForeignKey(p => p.UserId);
            modelBuilder.Entity<Voucher>()
    .Property(v => v.DiscountValue)
    .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Voucher>()
                .Property(v => v.MinOrderValue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Voucher>()
                .Property(v => v.MaxDiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FlashSaleItem>()
                .Property(f => f.PromotionPrice)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ProductRecommendation>()
    .HasOne(pr => pr.Product)
    .WithMany()
    .HasForeignKey(pr => pr.ProductId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductRecommendation>()
                .HasOne(pr => pr.RecommendedProduct)
                .WithMany()
                .HasForeignKey(pr => pr.RecommendedProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== CẤU HÌNH TƯ VẤN (CONSULTATION) =====
            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Consultation>()
                .HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConsultationReview>()
                .HasOne(r => r.Consultation)
                .WithOne(c => c.Review)
                .HasForeignKey<ConsultationReview>(r => r.ConsultationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConsultationReview>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConsultationProduct>()
                .HasOne(cp => cp.Consultation)
                .WithMany(c => c.ConsultationProducts)
                .HasForeignKey(cp => cp.ConsultationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConsultationProduct>()
                .HasOne(cp => cp.Product)
                .WithMany()
                .HasForeignKey(cp => cp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== CẤU HÌNH NHẬP KHO (STOCK RECEIPT) =====
            modelBuilder.Entity<StockReceipt>()
                .Property(x => x.ImportPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<StockReceipt>()
                .HasOne(x => x.Variant)
                .WithMany()
                .HasForeignKey(x => x.VariantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockReceipt>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StockReceipt>()
                .HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockReceipt>()
                .HasOne(x => x.ImportPort)
                .WithMany()
                .HasForeignKey(x => x.ImportPortId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== CẤU HÌNH CẢNG NHẬP KHẨU (IMPORT PORT) =====
            modelBuilder.Entity<ImportPort>(entity =>
            {
                entity.HasIndex(p => p.PortCode).IsUnique();
                entity.HasIndex(p => p.ProvinceCode);
                entity.HasIndex(p => p.ProvinceName);
            });

            // Seed danh mục cảng biển và sân bay quốc tế có thật tại Việt Nam
            modelBuilder.Entity<ImportPort>().HasData(
                new ImportPort
                {
                    Id = 1,
                    PortCode = "SGN-AIR",
                    Name = "Sân bay Quốc tế Tân Sơn Nhất",
                    PortType = "Air",
                    ProvinceName = "Thành phố Hồ Chí Minh",
                    ProvinceCode = "79",
                    AllowImport = true,
                    SourceReference = "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 2,
                    PortCode = "SGN-SEA-CATLAI",
                    Name = "Cảng Tân Cảng - Cát Lái",
                    PortType = "Sea",
                    ProvinceName = "Thành phố Hồ Chí Minh",
                    ProvinceCode = "79",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Quy hoạch phát triển hệ thống cảng biển VN)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 3,
                    PortCode = "HAN-AIR",
                    Name = "Sân bay Quốc tế Nội Bài",
                    PortType = "Air",
                    ProvinceName = "Thành phố Hà Nội",
                    ProvinceCode = "01",
                    AllowImport = true,
                    SourceReference = "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 4,
                    PortCode = "HPH-SEA-LACHHUYEN",
                    Name = "Cảng Quốc tế Lạch Huyện - Hải Phòng",
                    PortType = "Sea",
                    ProvinceName = "Thành phố Hải Phòng",
                    ProvinceCode = "31",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Cảng biển loại đặc biệt)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 5,
                    PortCode = "HPH-AIR",
                    Name = "Sân bay Quốc tế Cát Bi",
                    PortType = "Air",
                    ProvinceName = "Thành phố Hải Phòng",
                    ProvinceCode = "31",
                    AllowImport = true,
                    SourceReference = "Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 6,
                    PortCode = "DAD-AIR",
                    Name = "Sân bay Quốc tế Đà Nẵng",
                    PortType = "Air",
                    ProvinceName = "Thành phố Đà Nẵng",
                    ProvinceCode = "48",
                    AllowImport = true,
                    SourceReference = "Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 7,
                    PortCode = "DAD-SEA-TIENSA",
                    Name = "Cảng Tiên Sa - Đà Nẵng",
                    PortType = "Sea",
                    ProvinceName = "Thành phố Đà Nẵng",
                    ProvinceCode = "48",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Cảng biển loại I)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 8,
                    PortCode = "VTU-SEA-CAIMEP",
                    Name = "Cụm Cảng Cái Mép - Thị Vải",
                    PortType = "Sea",
                    ProvinceName = "Tỉnh Bà Rịa - Vũng Tàu",
                    ProvinceCode = "77",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Cảng biển loại đặc biệt cửa ngõ quốc tế)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 9,
                    PortCode = "CXR-AIR",
                    Name = "Sân bay Quốc tế Cam Ranh",
                    PortType = "Air",
                    ProvinceName = "Tỉnh Khánh Hòa",
                    ProvinceCode = "56",
                    AllowImport = true,
                    SourceReference = "Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 10,
                    PortCode = "UIH-SEA-QUYNHON",
                    Name = "Cảng Quy Nhơn",
                    PortType = "Sea",
                    ProvinceName = "Tỉnh Bình Định",
                    ProvinceCode = "52",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Cảng biển loại I)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 11,
                    PortCode = "VCA-AIR",
                    Name = "Sân bay Quốc tế Cần Thơ",
                    PortType = "Air",
                    ProvinceName = "Thành phố Cần Thơ",
                    ProvinceCode = "92",
                    AllowImport = true,
                    SourceReference = "Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 12,
                    PortCode = "PQC-AIR",
                    Name = "Sân bay Quốc tế Phú Quốc",
                    PortType = "Air",
                    ProvinceName = "Tỉnh Kiên Giang",
                    ProvinceCode = "91",
                    AllowImport = true,
                    SourceReference = "Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 13,
                    PortCode = "VDO-AIR",
                    Name = "Sân bay Quốc tế Vân Đồn",
                    PortType = "Air",
                    ProvinceName = "Tỉnh Quảng Ninh",
                    ProvinceCode = "22",
                    AllowImport = true,
                    SourceReference = "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 14,
                    PortCode = "QNH-SEA-CAILAN",
                    Name = "Cảng Cái Lân - Quảng Ninh",
                    PortType = "Sea",
                    ProvinceName = "Tỉnh Quảng Ninh",
                    ProvinceCode = "22",
                    AllowImport = true,
                    SourceReference = "Quyết định 1579/QĐ-TTg (Cảng biển loại I)",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new ImportPort
                {
                    Id = 15,
                    PortCode = "DLI-AIR",
                    Name = "Sân bay Quốc tế Liên Khương",
                    PortType = "Air",
                    ProvinceName = "Tỉnh Lâm Đồng",
                    ProvinceCode = "68",
                    AllowImport = true,
                    SourceReference = "Quyết định 648/QĐ-TTg & Cục Hàng không Việt Nam",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                }
            );

            // ===== CẤU HÌNH TUYẾN CHUYỂN KHO (TRANSFER ROUTE) =====
            modelBuilder.Entity<TransferRoute>(entity =>
            {
                entity.HasOne(r => r.FromStore)
                    .WithMany()
                    .HasForeignKey(r => r.FromStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.ToStore)
                    .WithMany()
                    .HasForeignKey(r => r.ToStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.TransitStore)
                    .WithMany()
                    .HasForeignKey(r => r.TransitStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(r => r.DistanceKm)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.EstimatedHours)
                    .HasColumnType("decimal(18,2)");
            });

            // ===== CẤU HÌNH PHIẾU CHUYỂN KHO (STOCK TRANSFER) =====
            modelBuilder.Entity<StockTransfer>(entity =>
            {
                entity.HasIndex(t => t.TransferCode).IsUnique();
                entity.HasIndex(t => t.BatchCode);

                entity.HasOne(t => t.FromStore)
                    .WithMany()
                    .HasForeignKey(t => t.FromStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.ToStore)
                    .WithMany()
                    .HasForeignKey(t => t.ToStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.FinalDestinationStore)
                    .WithMany()
                    .HasForeignKey(t => t.FinalDestinationStoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(t => t.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.ReceivedByUser)
                    .WithMany()
                    .HasForeignKey(t => t.ReceivedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(t => t.Lines)
                    .WithOne(l => l.StockTransfer)
                    .HasForeignKey(l => l.StockTransferId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== CẤU HÌNH CHI TIẾT PHIẾU CHUYỂN (STOCK TRANSFER LINE) =====
            modelBuilder.Entity<StockTransferLine>(entity =>
            {
                entity.HasOne(l => l.Variant)
                    .WithMany()
                    .HasForeignKey(l => l.VariantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Seed danh mục kho hàng hệ thống
            modelBuilder.Entity<Store>().HasData(
                new Store { Id = 1, Name = "Kho tổng TP.HCM", Address = "BeautyShop HQ", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", Region = "Miền Nam", StoreType = "Kho tổng (Central)", IsActive = true },
                new Store { Id = 2, Name = "Kho Tây Nguyên (Lâm Đồng)", Address = "12 Phan Chu Trinh, Phường 9, TP. Đà Lạt", ProvinceName = "Tỉnh Lâm Đồng", ProvinceCode = "68", Region = "Tây Nguyên", StoreType = "Kho phân phối (Retail)", IsActive = true },
                new Store { Id = 3, Name = "Kho tổng Miền Bắc (Hà Nội)", Address = "Khu công nghiệp Nội Bài, Sóc Sơn, TP. Hà Nội", ProvinceName = "Thành phố Hà Nội", ProvinceCode = "01", Region = "Miền Bắc", StoreType = "Kho tổng (Central)", IsActive = true },
                new Store { Id = 4, Name = "Kho cảng Hải Phòng", Address = "Khu kinh tế Đình Vũ - Cát Hải, TP. Hải Phòng", ProvinceName = "Thành phố Hải Phòng", ProvinceCode = "31", Region = "Miền Bắc", StoreType = "Kho trung chuyển cảng (Transit)", IsActive = true },
                new Store { Id = 5, Name = "Kho miền Trung (Đà Nẵng)", Address = "Khu công nghiệp Hòa Khánh, Liên Chiểu, TP. Đà Nẵng", ProvinceName = "Thành phố Đà Nẵng", ProvinceCode = "48", Region = "Miền Trung", StoreType = "Kho trung chuyển (Transit)", IsActive = true },
                new Store { Id = 6, Name = "Kho Tây Nam Bộ (Cần Thơ)", Address = "Khu công nghiệp Trà Nóc, Bình Thủy, TP. Cần Thơ", ProvinceName = "Thành phố Cần Thơ", ProvinceCode = "92", Region = "Miền Nam", StoreType = "Kho phân phối (Retail)", IsActive = true }
            );

            // Seed danh mục tuyến chuyển kho hợp lệ
            // Mô hình: Các kho miền Bắc (HN 3, HP 4) đi các miền khác (HCM 1, Lâm Đồng 2, Cần Thơ 6) bắt buộc gom qua Hub Đà Nẵng (5); Hub Đà Nẵng rẽ tỏa đi các miền; TP.HCM đi Cần Thơ, Lâm Đồng trực tiếp.
            modelBuilder.Entity<TransferRoute>().HasData(
                // 1. Tuyến trục trực tiếp vào Hub Đà Nẵng
                new TransferRoute { Id = 1, FromStoreId = 3, ToStoreId = 5, TransitStoreId = null, DistanceKm = 760, EstimatedHours = 14, IsActive = true, Description = "Hà Nội đi Hub Đà Nẵng (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 2, FromStoreId = 4, ToStoreId = 5, TransitStoreId = null, DistanceKm = 800, EstimatedHours = 15, IsActive = true, Description = "Hải Phòng đi Hub Đà Nẵng (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },

                // 2. Tuyến từ Hà Nội đi các miền khác (BẮT BUỘC QUA HUB ĐÀ NẴNG)
                new TransferRoute { Id = 3, FromStoreId = 3, ToStoreId = 1, TransitStoreId = 5, DistanceKm = 1720, EstimatedHours = 32, IsActive = true, Description = "Hà Nội đi TP.HCM (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 4, FromStoreId = 3, ToStoreId = 2, TransitStoreId = 5, DistanceKm = 1420, EstimatedHours = 28, IsActive = true, Description = "Hà Nội đi Tây Nguyên - Lâm Đồng (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 5, FromStoreId = 3, ToStoreId = 6, TransitStoreId = 5, DistanceKm = 1890, EstimatedHours = 35, IsActive = true, Description = "Hà Nội đi Cần Thơ (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },

                // 3. Tuyến từ Hải Phòng đi các miền khác (BẮT BUỘC QUA HUB ĐÀ NẴNG)
                new TransferRoute { Id = 6, FromStoreId = 4, ToStoreId = 1, TransitStoreId = 5, DistanceKm = 1760, EstimatedHours = 33, IsActive = true, Description = "Hải Phòng đi TP.HCM (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 7, FromStoreId = 4, ToStoreId = 2, TransitStoreId = 5, DistanceKm = 1460, EstimatedHours = 29, IsActive = true, Description = "Hải Phòng đi Tây Nguyên - Lâm Đồng (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 8, FromStoreId = 4, ToStoreId = 6, TransitStoreId = 5, DistanceKm = 1930, EstimatedHours = 36, IsActive = true, Description = "Hải Phòng đi Cần Thơ (qua Hub Đà Nẵng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },

                // 4. Tuyến từ Hub Đà Nẵng rẽ đi các kho khác (TRỰC TIẾP TỪ HUB)
                new TransferRoute { Id = 9, FromStoreId = 5, ToStoreId = 1, TransitStoreId = null, DistanceKm = 960, EstimatedHours = 18, IsActive = true, Description = "Hub Đà Nẵng đi Kho tổng TP.HCM", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 10, FromStoreId = 5, ToStoreId = 2, TransitStoreId = null, DistanceKm = 660, EstimatedHours = 14, IsActive = true, Description = "Hub Đà Nẵng rẽ đi Kho Tây Nguyên (Lâm Đồng)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 11, FromStoreId = 5, ToStoreId = 6, TransitStoreId = null, DistanceKm = 1130, EstimatedHours = 21, IsActive = true, Description = "Hub Đà Nẵng rẽ đi Kho Tây Nam Bộ (Cần Thơ)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 12, FromStoreId = 5, ToStoreId = 3, TransitStoreId = null, DistanceKm = 760, EstimatedHours = 14, IsActive = true, Description = "Hub Đà Nẵng điều phối về Kho tổng Hà Nội", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 13, FromStoreId = 5, ToStoreId = 4, TransitStoreId = null, DistanceKm = 800, EstimatedHours = 15, IsActive = true, Description = "Hub Đà Nẵng điều phối về Kho cảng Hải Phòng", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },

                // 5. Tuyến nội miền Nam & Tây Nguyên
                new TransferRoute { Id = 14, FromStoreId = 1, ToStoreId = 6, TransitStoreId = null, DistanceKm = 170, EstimatedHours = 3.5m, IsActive = true, Description = "TP.HCM đi Cần Thơ (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 15, FromStoreId = 1, ToStoreId = 2, TransitStoreId = null, DistanceKm = 300, EstimatedHours = 6, IsActive = true, Description = "TP.HCM đi Lâm Đồng (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 16, FromStoreId = 6, ToStoreId = 1, TransitStoreId = null, DistanceKm = 170, EstimatedHours = 3.5m, IsActive = true, Description = "Cần Thơ về TP.HCM (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 17, FromStoreId = 2, ToStoreId = 1, TransitStoreId = null, DistanceKm = 300, EstimatedHours = 6, IsActive = true, Description = "Lâm Đồng về TP.HCM (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) },
                new TransferRoute { Id = 18, FromStoreId = 1, ToStoreId = 5, TransitStoreId = null, DistanceKm = 960, EstimatedHours = 18, IsActive = true, Description = "TP.HCM đi Hub Đà Nẵng (trực tiếp)", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) }
            );

            // ===== CẤU HÌNH KHU VỰC GIAO HÀNG (DELIVERY ZONE) =====
            modelBuilder.Entity<DeliveryZone>(entity =>
            {
                entity.HasIndex(z => new { z.ProvinceName, z.ZoneName });
                entity.HasIndex(z => z.ClusterCode);
            });

            // Seed danh mục khu vực giao hàng phân cụm (TP.HCM và Lâm Đồng)
            // Lưu ý: Cụm khu vực là mô hình phân cụm giả định cấu hình linh hoạt. Tên khu vực sử dụng thực tế: TP. Thủ Đức thay cho Q2, Q9 cũ.
            // Các đơn vị hành chính đang có đề án chuyển đổi hoặc sáp nhập được đánh dấu "Cần kiểm chứng".
            modelBuilder.Entity<DeliveryZone>().HasData(
                // 1. Cụm Tây Bắc TP.HCM (HCM_NW): Hóc Môn, Củ Chi, Quận 12
                new DeliveryZone { Id = 1, ZoneName = "Hóc Môn", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_NW", ClusterName = "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", SortOrder = 1, AdjacentClusterCodes = "HCM_CEN", Notes = "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", IsActive = true },
                new DeliveryZone { Id = 2, ZoneName = "Củ Chi", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_NW", ClusterName = "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", SortOrder = 2, AdjacentClusterCodes = "HCM_NW", Notes = "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", IsActive = true },
                new DeliveryZone { Id = 3, ZoneName = "Quận 12", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_NW", ClusterName = "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", SortOrder = 3, AdjacentClusterCodes = "HCM_CEN", Notes = "Khu vực ổn định", IsActive = true },

                // 2. Cụm Trung Tâm TP.HCM (HCM_CEN): Gò Vấp, Bình Thạnh, Phú Nhuận, Quận 1, Quận 3
                new DeliveryZone { Id = 4, ZoneName = "Gò Vấp", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_CEN", ClusterName = "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", SortOrder = 4, AdjacentClusterCodes = "HCM_NW,HCM_EST", Notes = "Khu vực ổn định kề Tây Bắc", IsActive = true },
                new DeliveryZone { Id = 5, ZoneName = "Bình Thạnh", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_CEN", ClusterName = "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", SortOrder = 5, AdjacentClusterCodes = "HCM_EST", Notes = "Cửa ngõ kề TP. Thủ Đức", IsActive = true },
                new DeliveryZone { Id = 6, ZoneName = "Phú Nhuận", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_CEN", ClusterName = "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", SortOrder = 6, AdjacentClusterCodes = "HCM_CEN", Notes = "Khu vực trung tâm nội đô", IsActive = true },
                new DeliveryZone { Id = 7, ZoneName = "Quận 1", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_CEN", ClusterName = "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", SortOrder = 7, AdjacentClusterCodes = "HCM_SOU", Notes = "Khu vực trung tâm nội đô", IsActive = true },
                new DeliveryZone { Id = 8, ZoneName = "Quận 3", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_CEN", ClusterName = "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", SortOrder = 8, AdjacentClusterCodes = "HCM_CEN", Notes = "Khu vực trung tâm nội đô", IsActive = true },

                // 3. Cụm Phía Đông TP.HCM (HCM_EST): TP. Thủ Đức (đã sáp nhập Q2, Q9, Thủ Đức cũ từ 2021)
                new DeliveryZone { Id = 9, ZoneName = "Thành phố Thủ Đức", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_EST", ClusterName = "Cụm Phía Đông (TP. Thủ Đức)", SortOrder = 9, AdjacentClusterCodes = "HCM_CEN", Notes = "Đã sáp nhập Q2, Q9 và Thủ Đức cũ thành TP. Thủ Đức từ 2021", IsActive = true },

                // 4. Cụm Phía Nam TP.HCM (HCM_SOU): Quận 7, Nhà Bè, Bình Chánh
                new DeliveryZone { Id = 10, ZoneName = "Quận 7", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_SOU", ClusterName = "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", SortOrder = 10, AdjacentClusterCodes = "HCM_CEN", Notes = "Khu vực Nam Sài Gòn", IsActive = true },
                new DeliveryZone { Id = 11, ZoneName = "Nhà Bè", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_SOU", ClusterName = "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", SortOrder = 11, AdjacentClusterCodes = "HCM_SOU", Notes = "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", IsActive = true },
                new DeliveryZone { Id = 12, ZoneName = "Bình Chánh", ProvinceName = "Thành phố Hồ Chí Minh", ProvinceCode = "79", ClusterCode = "HCM_SOU", ClusterName = "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", SortOrder = 12, AdjacentClusterCodes = "HCM_NW", Notes = "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", IsActive = true },

                // 5. Khu vực Tây Nguyên (Lâm Đồng)
                new DeliveryZone { Id = 13, ZoneName = "Thành phố Đà Lạt", ProvinceName = "Tỉnh Lâm Đồng", ProvinceCode = "68", ClusterCode = "LD_DALAT", ClusterName = "Cụm Đà Lạt", SortOrder = 13, AdjacentClusterCodes = "LD_DUCTRONG", Notes = "Cần kiểm chứng (đang sắp xếp mở rộng địa giới hành chính)", IsActive = true },
                new DeliveryZone { Id = 14, ZoneName = "Huyện Đức Trọng", ProvinceName = "Tỉnh Lâm Đồng", ProvinceCode = "68", ClusterCode = "LD_DUCTRONG", ClusterName = "Cụm Đức Trọng", SortOrder = 14, AdjacentClusterCodes = "LD_DALAT", Notes = "Cần kiểm chứng (khu vực Sân bay Liên Khương, có đề án lên thị xã)", IsActive = true }
            );

            // ===== CẤU HÌNH CHUYẾN GIAO HÀNG (DELIVERY TRIP) =====
            modelBuilder.Entity<DeliveryTrip>(entity =>
            {
                entity.HasIndex(t => t.TripCode).IsUnique();
                entity.HasIndex(t => new { t.TripDate, t.Shift, t.ClusterCode });

                entity.HasOne(t => t.ShipperUser)
                    .WithMany()
                    .HasForeignKey(t => t.ShipperUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.CashierUser)
                    .WithMany()
                    .HasForeignKey(t => t.CashierUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(t => t.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== CẤU HÌNH ĐIỂM DỪNG GIAO HÀNG (DELIVERY TRIP STOP) =====
            modelBuilder.Entity<DeliveryTripStop>(entity =>
            {
                // Ràng buộc nghiêm ngặt: Mỗi đơn hàng chỉ có DUY NHẤT 1 điểm dừng đang hoạt động (IsActive = 1) tại một thời điểm
                entity.HasIndex(s => s.OrderId)
                    .HasFilter("[IsActive] = 1")
                    .IsUnique();

                entity.HasOne(s => s.DeliveryTrip)
                    .WithMany(t => t.Stops)
                    .HasForeignKey(s => s.DeliveryTripId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Order)
                    .WithMany()
                    .HasForeignKey(s => s.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== CẤU HÌNH ĐỐI SOÁT CUỐI NGÀY (DAILY RECONCILIATION) =====
            modelBuilder.Entity<DailyReconciliation>(entity =>
            {
                entity.HasIndex(r => r.Code).IsUnique();
                entity.HasIndex(r => r.ReconciliationDate);

                entity.HasOne(r => r.CashierUser)
                    .WithMany()
                    .HasForeignKey(r => r.CashierUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.AdminApprovedUser)
                    .WithMany()
                    .HasForeignKey(r => r.AdminApprovedUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== LIÊN KẾT ĐƠN HÀNG VỚI KHU VỰC GIAO HÀNG =====
            modelBuilder.Entity<Order>()
                .HasOne(o => o.DeliveryZone)
                .WithMany()
                .HasForeignKey(o => o.DeliveryZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== CẤU HÌNH PHÂN QUYỀN CHỨC NĂNG & PHẠM VI KHO (BƯỚC 6) =====
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasIndex(p => p.Code).IsUnique();
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

                entity.HasOne(rp => rp.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserWarehouseScope>(entity =>
            {
                entity.HasIndex(s => new { s.UserId, s.StoreId }).IsUnique();

                entity.HasOne(s => s.User)
                    .WithMany(u => u.WarehouseScopes)
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Store)
                    .WithMany()
                    .HasForeignKey(s => s.StoreId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(s => s.AssignedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => a.TargetUserId);

                entity.HasOne(a => a.PerformedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.PerformedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.TargetUser)
                    .WithMany()
                    .HasForeignKey(a => a.TargetUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Seed 14 quyền chức năng chuẩn (không phụ thuộc vùng miền)
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Code = AppPermissions.ProductManage, Name = "Quản lý sản phẩm", Category = "Sản phẩm", Description = "Xem, thêm, sửa, xóa sản phẩm, danh mục và thương hiệu" },
                new Permission { Id = 2, Code = AppPermissions.PriceSet, Name = "Thiết lập giá bán", Category = "Giá & Khuyến mãi", Description = "Cấu hình giá bán lẻ, giá bán sỉ, voucher và chương trình Flash Sale" },
                new Permission { Id = 3, Code = AppPermissions.InventoryView, Name = "Xem tồn kho", Category = "Kho bãi", Description = "Xem số lượng tồn kho thực tế, hàng đang giữ và lịch sử biến động kho thuộc phạm vi" },
                new Permission { Id = 4, Code = AppPermissions.InventoryReceive, Name = "Lập phiếu nhập kho", Category = "Kho bãi", Description = "Lập phiếu nhập khẩu theo cảng và nhập hàng nội địa cho các kho thuộc phạm vi" },
                new Permission { Id = 5, Code = AppPermissions.InventoryTransfer, Name = "Luân chuyển kho", Category = "Kho bãi", Description = "Tạo phiếu xuất chuyển kho và xác nhận nhận hàng chuyển kho thuộc phạm vi" },
                new Permission { Id = 6, Code = AppPermissions.DeliveryDispatch, Name = "Điều phối chuyến giao", Category = "Giao hàng", Description = "Gom chuyến giao tự động, gán nhân viên giao hàng và sắp xếp thứ tự điểm dừng" },
                new Permission { Id = 7, Code = AppPermissions.DeliveryExecute, Name = "Thực hiện giao hàng", Category = "Giao hàng", Description = "Xem chuyến giao của chính mình, cập nhật trạng thái điểm dừng và ghi nhận tiền thu hộ COD" },
                new Permission { Id = 8, Code = AppPermissions.PaymentProcess, Name = "Thanh toán đơn hàng", Category = "Thu ngân", Description = "Xử lý đơn hàng, xác nhận thanh toán và cập nhật trạng thái đơn trong phạm vi kho" },
                new Permission { Id = 9, Code = AppPermissions.CashierReconcile, Name = "Quyết toán & Đối soát COD", Category = "Thu ngân", Description = "Nhận tiền COD từ nhân viên giao hàng, quyết toán chuyến và lập bảng đối soát cuối ngày nộp Admin" },
                new Permission { Id = 10, Code = AppPermissions.ReportView, Name = "Xem báo cáo kinh doanh", Category = "Báo cáo", Description = "Xem thống kê doanh thu, sản phẩm bán chạy và sản phẩm bán chậm trong phạm vi" },
                new Permission { Id = 11, Code = AppPermissions.UserManage, Name = "Quản lý nhân viên", Category = "Hệ thống", Description = "Tạo tài khoản nhân viên, gán vai trò, phân công phạm vi kho và khóa/mở tài khoản" },
                new Permission { Id = 12, Code = AppPermissions.RoleManage, Name = "Quản lý vai trò & Phân quyền", Category = "Hệ thống", Description = "Xem và cấu hình ma trận Vai trò x Quyền, tra cứu nhật ký thay đổi phân quyền (AuditLog)" },
                new Permission { Id = 13, Code = AppPermissions.PortManage, Name = "Quản lý cảng nhập khẩu", Category = "Kho bãi", Description = "Thêm, sửa, bật/tắt cảng nhập khẩu hợp lệ trong danh mục quốc gia" },
                new Permission { Id = 14, Code = AppPermissions.RouteManage, Name = "Cấu hình tuyến chuyển kho", Category = "Kho bãi", Description = "Cấu hình tuyến luân chuyển liên tỉnh và kho trung chuyển (Hub)" }
            );
        }
    }
}