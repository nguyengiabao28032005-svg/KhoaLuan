using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;

namespace BeautyShop.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tạo sản phẩm mới và biến thể ban đầu theo đúng quy trình:
        /// Chưa có giá bán lẻ, giá bán sỉ, giá gốc và tồn kho (tồn kho ban đầu = 0).
        /// Tất cả ghi nhiều bảng được bọc trong Transaction.
        /// </summary>
        public async Task<(bool Success, string Message, Product? Product)> CreateProductAsync(CreateProductViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tạo thực thể Product
                var product = new Product
                {
                    Name = model.Name.Trim(),
                    CategoryId = model.CategoryId,
                    BrandId = model.BrandId,
                    ThumbnailUrl = model.ThumbnailUrl?.Trim(),
                    Description = model.Description?.Trim(),
                    Ingredients = model.Ingredients?.Trim(),
                    HowToUse = model.HowToUse?.Trim(),
                    IsActive = model.IsActive
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // 2. Tạo mã SKU duy nhất cho biến thể mặc định
                string sku = $"SKU-{product.Id}-DEF";
                if (await _context.ProductVariants.AnyAsync(v => v.SKU == sku))
                {
                    sku = $"SKU-{product.Id}-DEF-{DateTime.Now.Ticks % 10000}";
                }

                // 3. Tạo biến thể ban đầu (Giá gốc, Giá lẻ, Giá sỉ để null; chưa nhập hàng)
                var defaultVariant = new ProductVariant
                {
                    ProductId = product.Id,
                    SKU = sku,
                    Volume = !string.IsNullOrWhiteSpace(model.Volume) ? model.Volume.Trim() : "Mặc định",
                    ColorName = !string.IsNullOrWhiteSpace(model.ColorName) ? model.ColorName.Trim() : null,
                    OriginalPrice = null,  // Chưa có giá gốc (chờ phiếu nhập)
                    RetailPrice = null,    // Chưa có giá bán lẻ (chờ thiết lập giá)
                    WholesalePrice = null, // Chưa có giá bán sỉ (chờ thiết lập giá)
                    ImageUrl = product.ThumbnailUrl
                };

                _context.ProductVariants.Add(defaultVariant);
                await _context.SaveChangesAsync();

                // 4. Khởi tạo liên kết tồn kho bằng 0 tại kho chính
                var store = await _context.Stores.FirstOrDefaultAsync();
                if (store == null)
                {
                    store = new Store { Name = "Kho chính", Address = "BeautyShop HQ", IsActive = true };
                    _context.Stores.Add(store);
                    await _context.SaveChangesAsync();
                }

                _context.Inventories.Add(new Inventory
                {
                    StoreId = store.Id,
                    VariantId = defaultVariant.Id,
                    StockQuantity = 0,
                    ReservedQuantity = 0
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return (true, $"Thêm sản phẩm \"{product.Name}\" thành công! Vui lòng tạo Phiếu nhập hàng để ghi nhận tồn kho và giá vốn.", product);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi lưu sản phẩm vào cơ sở dữ liệu: " + (ex.InnerException?.Message ?? ex.Message), null);
            }
        }

        /// <summary>
        /// Tạo thêm biến thể phân loại (Dung tích/Màu sắc) cho sản phẩm đã có.
        /// Biến thể mới chưa có giá và tồn kho = 0. Bọc trong Transaction.
        /// </summary>
        public async Task<(bool Success, string Message, ProductVariant? Variant)> CreateVariantAsync(CreateVariantViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product == null)
                {
                    return (false, "Không tìm thấy sản phẩm tương ứng.", null);
                }

                string sku = model.SKU?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sku))
                {
                    sku = $"SKU-{product.Id}-{DateTime.Now.Ticks % 10000}";
                }
                else if (await _context.ProductVariants.AnyAsync(v => v.SKU == sku))
                {
                    sku = $"{sku}-{DateTime.Now.Ticks % 1000}";
                }

                var variant = new ProductVariant
                {
                    ProductId = model.ProductId,
                    SKU = sku,
                    Volume = model.Volume?.Trim(),
                    ColorName = model.ColorName?.Trim(),
                    OriginalPrice = null,  // Chưa có giá gốc
                    RetailPrice = null,    // Chưa có giá bán lẻ
                    WholesalePrice = null, // Chưa có giá bán sỉ
                    ImageUrl = product.ThumbnailUrl
                };

                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();

                var store = await _context.Stores.FirstOrDefaultAsync();
                if (store == null)
                {
                    store = new Store { Name = "Kho chính", Address = "BeautyShop HQ", IsActive = true };
                    _context.Stores.Add(store);
                    await _context.SaveChangesAsync();
                }

                _context.Inventories.Add(new Inventory
                {
                    StoreId = store.Id,
                    VariantId = variant.Id,
                    StockQuantity = 0,
                    ReservedQuantity = 0
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return (true, "Thêm biến thể sản phẩm thành công!", variant);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi khi thêm biến thể: " + (ex.InnerException?.Message ?? ex.Message), null);
            }
        }

        /// <summary>
        /// Thiết lập giá bán lẻ và giá bán sỉ cho biến thể sau khi đã nhập hàng vào kho.
        /// Ràng buộc nghiệp vụ: Biến thể phải có tồn kho > 0 mới được thiết lập giá bán.
        /// </summary>
        public async Task<(bool Success, string Message)> SetVariantPricingAsync(SetPricingViewModel model)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Inventories)
                .FirstOrDefaultAsync(v => v.Id == model.VariantId);

            if (variant == null)
            {
                return (false, "Không tìm thấy biến thể sản phẩm.");
            }

            // Kiểm tra biến thể đã có hàng trong kho chưa theo yêu cầu nghiệp vụ
            int totalStock = variant.Inventories?.Sum(i => i.StockQuantity) ?? 0;
            if (totalStock <= 0)
            {
                return (false, $"Biến thể \"{variant.SKU}\" hiện chưa có hàng trong kho (Tồn kho: 0). Vui lòng lập phiếu nhập kho trước khi thiết lập giá bán.");
            }

            if (model.RetailPrice <= 0)
            {
                return (false, "Giá bán lẻ phải lớn hơn 0đ.");
            }

            variant.RetailPrice = model.RetailPrice;
            variant.WholesalePrice = model.WholesalePrice;

            await _context.SaveChangesAsync();
            return (true, $"Đã cập nhật giá bán cho biến thể \"{variant.SKU}\" ({variant.Product?.Name}) thành công!");
        }

        /// <summary>
        /// Lấy danh sách các biến thể phục vụ màn hình quản lý & thiết lập giá bán
        /// </summary>
        public async Task<List<VariantPricingRowViewModel>> GetVariantsForPricingAsync(int? productId = null, bool? onlyUnpriced = null)
        {
            var query = _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Inventories)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(v => v.ProductId == productId.Value);
            }

            if (onlyUnpriced == true)
            {
                query = query.Where(v => v.RetailPrice == null || v.RetailPrice <= 0);
            }

            var list = await query
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.Id)
                .Select(v => new VariantPricingRowViewModel
                {
                    VariantId = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    BrandName = v.Product.Brand != null ? v.Product.Brand.Name : null,
                    CategoryName = v.Product.Category != null ? v.Product.Category.Name : null,
                    SKU = v.SKU,
                    Volume = v.Volume,
                    ColorName = v.ColorName,
                    ThumbnailUrl = !string.IsNullOrEmpty(v.ImageUrl) ? v.ImageUrl : v.Product.ThumbnailUrl,
                    StockQuantity = v.Inventories.Sum(i => i.StockQuantity),
                    CostPrice = v.OriginalPrice,
                    RetailPrice = v.RetailPrice,
                    WholesalePrice = v.WholesalePrice
                })
                .ToListAsync();

            return list;
        }
    }
}
