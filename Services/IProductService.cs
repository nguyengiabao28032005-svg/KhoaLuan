using System.Collections.Generic;
using System.Threading.Tasks;
using BeautyShop.Models;
using BeautyShop.ViewModels;

namespace BeautyShop.Services
{
    public interface IProductService
    {
        /// <summary>
        /// Tạo sản phẩm mới kèm biến thể mặc định (không có giá và tồn kho ban đầu)
        /// </summary>
        Task<(bool Success, string Message, Product? Product)> CreateProductAsync(CreateProductViewModel model);

        /// <summary>
        /// Thêm biến thể mới cho sản phẩm (không có giá và tồn kho)
        /// </summary>
        Task<(bool Success, string Message, ProductVariant? Variant)> CreateVariantAsync(CreateVariantViewModel model);

        /// <summary>
        /// Thiết lập giá bán lẻ và giá bán sỉ cho biến thể sau khi đã có hàng
        /// </summary>
        Task<(bool Success, string Message)> SetVariantPricingAsync(SetPricingViewModel model);

        /// <summary>
        /// Lấy danh sách các biến thể phục vụ màn hình thiết lập giá
        /// </summary>
        Task<List<VariantPricingRowViewModel>> GetVariantsForPricingAsync(int? productId = null, bool? onlyUnpriced = null);
    }
}
