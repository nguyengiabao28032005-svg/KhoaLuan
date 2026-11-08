using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string keyword, string status)
        {
            var query = _context.Users
                .Include(x => x.Role)
                .Where(x => x.Role != null && x.Role.Name == "Customer")
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(x =>
                    x.FullName.Contains(keyword) ||
                    x.Email.Contains(keyword) ||
                    (x.PhoneNumber != null && x.PhoneNumber.Contains(keyword)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "active")
                    query = query.Where(x => x.IsActive);
                else if (status == "inactive")
                    query = query.Where(x => !x.IsActive);
            }

            ViewBag.Keyword = keyword;
            ViewBag.CurrentStatus = status;
            ViewBag.TotalCount = await query.CountAsync();

            return View(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
        }

        public IActionResult Create()
        {
            ViewBag.Roles = _context.Roles.ToList();

            return View();
        }
        [HttpPost]
        public IActionResult Create(AdminUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _context.Roles.ToList();
                return View(model);
            }

            User user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = model.RoleId,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
        public IActionResult ToggleStatus(int id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
                return RedirectToAction(nameof(Index));

            user.IsActive = !user.IsActive;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Detail(int id)
        {
            var user = _context.Users
                .Include(x => x.Role)
                .Include(x => x.UserAddresses)
                .FirstOrDefault(x => x.Id == id);

            if (user == null)
                return NotFound();

            return View(user);
        }
        public IActionResult Edit(int id)
        {
            var user = _context.Users.Find(id);

            if (user == null)
                return NotFound();

            var vm = new AdminUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                IsActive = user.IsActive
            };

            ViewBag.Roles = _context.Roles.ToList();

            return View(vm);
        }
        [HttpPost]
        public IActionResult Edit(AdminUserViewModel model)
        {
            var user = _context.Users.Find(model.Id);

            if (user == null)
                return NotFound();

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.RoleId = model.RoleId;
            user.IsActive = model.IsActive;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}