using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSupplierProductToVariantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // MIGRATION GIAI ĐOẠN 1: Chuyển đổi bảng SupplierProducts sang VariantId
            // Đánh dấu các dòng cũ: 'chuyển đổi từ dữ liệu theo sản phẩm, cần rà soát'
            // =========================================================================
            migrationBuilder.Sql(@"
                -- 1. Lưu dữ liệu cũ từ bảng SupplierProducts vào bảng tạm
                CREATE TABLE IF NOT EXISTS _old_SupplierProducts AS SELECT * FROM SupplierProducts;

                -- 2. Xóa bảng SupplierProducts cũ
                DROP TABLE IF EXISTS SupplierProducts;

                -- 3. Tạo bảng SupplierProducts mới liên kết theo VariantId và có UpdatedAt
                CREATE TABLE SupplierProducts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierId INTEGER NOT NULL,
                    VariantId INTEGER NOT NULL,
                    SuppliedPrice decimal(18,2) NULL,
                    Notes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NULL,
                    CONSTRAINT FK_SupplierProducts_ProductVariants_VariantId FOREIGN KEY (VariantId) REFERENCES ProductVariants (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_SupplierProducts_Suppliers_SupplierId FOREIGN KEY (SupplierId) REFERENCES Suppliers (Id) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IX_SupplierProducts_SupplierId_VariantId ON SupplierProducts (SupplierId, VariantId);
                CREATE INDEX IX_SupplierProducts_VariantId ON SupplierProducts (VariantId);

                -- 4. Chuyển dữ liệu: map từ ProductId sang từng VariantId tương ứng
                INSERT INTO SupplierProducts (SupplierId, VariantId, SuppliedPrice, Notes, CreatedAt, UpdatedAt)
                SELECT 
                    old.SupplierId, 
                    pv.Id AS VariantId, 
                    old.SuppliedPrice, 
                    'chuyển đổi từ dữ liệu theo sản phẩm, cần rà soát' AS Notes, 
                    old.CreatedAt, 
                    old.CreatedAt AS UpdatedAt
                FROM _old_SupplierProducts old
                JOIN ProductVariants pv ON old.ProductId = pv.ProductId;

                -- 5. Xóa bảng tạm
                DROP TABLE IF EXISTS _old_SupplierProducts;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // DOWN MIGRATION: Khôi phục lại SupplierProducts theo ProductId
            // LƯU Ý MẤT DỮ LIỆU: Nếu các biến thể khác nhau của cùng một sản phẩm
            // đã được người dùng chỉnh các mức giá khác nhau, khi Down() hệ thống
            // buộc phải gộp lại theo ProductId và lấy MIN(SuppliedPrice), do đó
            // sự khác biệt về giá theo từng biến thể riêng biệt sẽ bị mất.
            // =========================================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS _temp_SupplierProducts AS SELECT * FROM SupplierProducts;

                DROP TABLE IF EXISTS SupplierProducts;

                CREATE TABLE SupplierProducts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    SuppliedPrice decimal(18,2) NULL,
                    Notes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT FK_SupplierProducts_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_SupplierProducts_Suppliers_SupplierId FOREIGN KEY (SupplierId) REFERENCES Suppliers (Id) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IX_SupplierProducts_SupplierId_ProductId ON SupplierProducts (SupplierId, ProductId);
                CREATE INDEX IX_SupplierProducts_ProductId ON SupplierProducts (ProductId);

                INSERT INTO SupplierProducts (SupplierId, ProductId, SuppliedPrice, Notes, CreatedAt)
                SELECT 
                    sp.SupplierId, 
                    pv.ProductId, 
                    MIN(sp.SuppliedPrice), 
                    'Khôi phục từ biến thể khi rollback migration' AS Notes, 
                    MIN(sp.CreatedAt)
                FROM _temp_SupplierProducts sp
                JOIN ProductVariants pv ON sp.VariantId = pv.Id
                GROUP BY sp.SupplierId, pv.ProductId;

                DROP TABLE IF EXISTS _temp_SupplierProducts;
            ");
        }
    }
}
