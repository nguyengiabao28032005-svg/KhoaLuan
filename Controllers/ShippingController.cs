using BeautyShop.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeautyShop.Controllers
{
    public class ShippingController : Controller
    {
        private readonly GhnService _ghn;

        public ShippingController(GhnService ghn)
        {
            _ghn = ghn;
        }

        // Lấy danh sách Tỉnh
        [HttpGet]
        public async Task<IActionResult> GetProvinces()
        {
            var result = await _ghn.GetProvincesAsync();
            return Json(result);
        }

        // Lấy danh sách Quận theo Tỉnh
        [HttpGet]
        public async Task<IActionResult> GetDistricts(int provinceId)
        {
            var result = await _ghn.GetDistrictsAsync(provinceId);
            return Json(result);
        }

        // Lấy danh sách Phường theo Quận
        [HttpGet]
        public async Task<IActionResult> GetWards(int districtId)
        {
            var result = await _ghn.GetWardsAsync(districtId);
            return Json(result);
        }

        // Tính phí ship - AJAX endpoint (gọi từ trang Checkout)
        [HttpPost]
        public async Task<IActionResult> CalculateFee(int toDistrictId, string toWardCode)
        {
            // Mặc định 500gram cho mỹ phẩm
            var fee = await _ghn.CalculateFeeAsync(toDistrictId, toWardCode, 500);
            return Json(new { fee, feeFormatted = fee.ToString("N0") + "đ" });
        }
    }
}