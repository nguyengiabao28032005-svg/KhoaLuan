using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Categories
        public IActionResult Index()
        {
            var categories = _context.Categories.ToList();
            return View(categories);
        }

        // GET: Admin/Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Categories/Create
        [HttpPost]
        public IActionResult Create(Category model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Name))
                {
                    ModelState.AddModelError("Name", "Tên danh mục không được để trống.");
                    return View(model);
                }

                // TỰ ĐỘNG TẠO SLUG: Biến "Nước tẩy trang" thành "nuoc-tay-trang" để thỏa mãn cột NOT NULL trong DB
                string generatedSlug = GenerateSlug(model.Name);

                var newCategory = new Category
                {
                    Name = model.Name,
                    Slug = generatedSlug // ĐÃ BỔ SUNG: Truyền giá trị Slug hợp lệ vào đây
                };

                _context.Categories.Add(newCategory);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                string errorMessage = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError("", "Không thể lưu vào Database. Chi tiết lỗi: " + errorMessage);
            }

            return View(model);
        }

        // GET: Admin/Categories/Edit/5
        public IActionResult Edit(int id)
        {
            var category = _context.Categories.Find(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost]
        public IActionResult Edit(Category model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Name))
                {
                    ModelState.AddModelError("Name", "Tên danh mục không được để trống.");
                    return View(model);
                }

                var existingCategory = _context.Categories.Find(model.Id);
                if (existingCategory == null)
                {
                    return NotFound();
                }

                // Cập nhật cả Name và tự động tính toán lại Slug mới theo Name vừa sửa
                existingCategory.Name = model.Name;
                existingCategory.Slug = GenerateSlug(model.Name);

                _context.Categories.Update(existingCategory);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                string errorMessage = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError("", "Không thể cập nhật Database. Chi tiết lỗi: " + errorMessage);
            }

            return View(model);
        }

        // GET: Admin/Categories/Delete/5
        public IActionResult Delete(int id)
        {
            var category = _context.Categories.Find(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                var category = _context.Categories.Find(id);
                if (category != null)
                {
                    _context.Categories.Remove(category);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                TempData["DeleteError"] = "Không thể xóa danh mục này vì đang có ràng buộc dữ liệu! " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Hàm Helper: Tự động xóa dấu tiếng Việt, ký tự đặc biệt và tạo chuỗi URL Slug chuẩn SEO
        /// </summary>
        private string GenerateSlug(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return "";

            string str = phrase.ToLower();

            // Thay thế các ký tự có dấu tiếng Việt thành không dấu
            string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
                                            "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
                                            "í","ì","ỉ","ĩ","ị",
                                            "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
                                            "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
                                            "ý","ỳ","ỷ","ỹ","ỵ",
                                            "đ" };
            string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
                                            "e","e","e","e","e","e","e","e","e","e","e",
                                            "i","i","i","i","i",
                                            "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
                                            "u","u","u","u","u","u","u","u","u","u","u","u","u","u","u","u","u",
                                            "y","y","y","y","y",
                                            "d" };
            for (int i = 0; i < arr1.Length; i++)
            {
                str = str.Replace(arr1[i], arr2[i]);
            }

            // Xóa ký tự đặc biệt, giữ lại chữ cái, số và khoảng trắng
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

            // Thay thế nhiều khoảng trắng liên tiếp thành 1 khoảng trắng
            str = Regex.Replace(str, @"\s+", " ").Trim();

            // Thay thế khoảng trắng bằng dấu gạch ngang
            str = str.Replace(" ", "-");

            return str;
        }
    }
}