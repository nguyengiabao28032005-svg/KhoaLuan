using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.PortManage)]
    public class ImportPortsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ImportPortsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/ImportPorts
        public async Task<IActionResult> Index(string? keyword, string? portType, string? status)
        {
            var query = _context.ImportPorts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    p.PortCode.Contains(keyword) ||
                    p.ProvinceName.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(portType))
            {
                query = query.Where(p => p.PortType == portType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                    query = query.Where(p => p.AllowImport);
                else if (status == "inactive")
                    query = query.Where(p => !p.AllowImport);
            }

            var ports = await query.OrderBy(p => p.ProvinceName).ThenBy(p => p.Name).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.PortType = portType;
            ViewBag.Status = status;
            ViewBag.TotalPorts = await _context.ImportPorts.CountAsync();
            ViewBag.ActivePorts = await _context.ImportPorts.CountAsync(p => p.AllowImport);
            ViewBag.AirPorts = await _context.ImportPorts.CountAsync(p => p.PortType == "Air");
            ViewBag.SeaPorts = await _context.ImportPorts.CountAsync(p => p.PortType == "Sea");

            return View(ports);
        }

        // POST: /Admin/ImportPorts/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var port = await _context.ImportPorts.FindAsync(id);
            if (port == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy cảng nhập khẩu được yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            port.AllowImport = !port.AllowImport;
            port.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            string statusLabel = port.AllowImport ? "MỞ TIẾP NHẬN" : "TẠM ĐÓNG TIẾP NHẬN";
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái cảng \"{port.Name}\" ({port.ProvinceName}) thành: {statusLabel} nhập khẩu.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/ImportPorts/Create
        [HttpPost]
        public async Task<IActionResult> Create(ImportPort model)
        {

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.PortCode) || string.IsNullOrWhiteSpace(model.ProvinceName))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ tên cảng, mã cảng và tỉnh/thành phố.";
                return RedirectToAction(nameof(Index));
            }

            model.PortCode = model.PortCode.Trim().ToUpper();
            model.Name = model.Name.Trim();
            model.ProvinceName = model.ProvinceName.Trim();
            model.ProvinceCode = model.ProvinceCode?.Trim();
            model.PortType = model.PortType == "Sea" ? "Sea" : "Air";
            model.AllowImport = true;
            model.CreatedAt = DateTime.Now;

            bool exists = await _context.ImportPorts.AnyAsync(p => p.PortCode == model.PortCode);
            if (exists)
            {
                TempData["ErrorMessage"] = $"Mã cảng '{model.PortCode}' đã tồn tại trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            _context.ImportPorts.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm thành công cảng \"{model.Name}\" ({model.ProvinceName}) vào danh mục cảng nhập khẩu.";
            return RedirectToAction(nameof(Index));
        }
    }
}
