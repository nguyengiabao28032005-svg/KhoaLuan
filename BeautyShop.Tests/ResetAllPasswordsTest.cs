using System;
using System.IO;
using System.Threading.Tasks;
using BeautyShop.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeautyShop.Tests
{
    public class ResetAllPasswordsTest
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public ResetAllPasswordsTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ResetAllUsersPasswordTo123456()
        {
            string dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../beautyshop.db"));
            if (!File.Exists(dbPath))
            {
                dbPath = Path.GetFullPath("beautyshop.db");
            }
            if (!File.Exists(dbPath))
            {
                dbPath = @"c:\KhoaLuan-main\beautyshop.db";
            }
            Assert.True(File.Exists(dbPath), $"Database file does not exist at {dbPath}");

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var db = new ApplicationDbContext(options);
            var users = await db.Users.Include(u => u.Role).ToListAsync();
            Assert.NotEmpty(users);

            string newHash = BCrypt.Net.BCrypt.HashPassword("123456");

            foreach (var user in users)
            {
                user.PasswordHash = newHash;
                _output.WriteLine($"Account: {user.Email} | Name: {user.FullName} | Role: {user.Role?.Name ?? user.Position ?? "Customer"}");
            }

            int count = await db.SaveChangesAsync();

            // Verify
            var reloadedUsers = await db.Users.ToListAsync();
            foreach (var u in reloadedUsers)
            {
                Assert.True(BCrypt.Net.BCrypt.Verify("123456", u.PasswordHash), $"Password verify failed for {u.Email}");
            }
        }
    }
}
