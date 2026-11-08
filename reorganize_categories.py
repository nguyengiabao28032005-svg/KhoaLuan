import sqlite3, sys
sys.stdout.reconfigure(encoding='utf-8')

conn = sqlite3.connect('beautyshop.db')
cursor = conn.cursor()

# 1. Update existing categories to clear professional names
cursor.execute("UPDATE Categories SET Name = 'Son Thỏi & Son Kem Lì' WHERE Id = 1")
cursor.execute("UPDATE Categories SET Name = 'Kem Chống Nắng Bảo Vệ Da' WHERE Id = 2")
cursor.execute("UPDATE Categories SET Name = 'Sữa Rửa Mặt Hằng Ngày' WHERE Id = 3")
cursor.execute("UPDATE Categories SET Name = 'Nước Cân Bằng / Toner' WHERE Id = 4")
cursor.execute("UPDATE Categories SET Name = 'Serum Cấp Ẩm & Phục Hồi' WHERE Id = 5")

# 2. Add 5 new categories if not exist
new_cats = [
    (6, 'Son Dưỡng & Trang Điểm Môi', 'son-duong-trang-diem-moi'),
    (7, 'Chống Nắng Khoáng & Nâng Tông', 'chong-nang-khoang-nang-tong'),
    (8, 'Tẩy Trang & Làm Sạch Sâu', 'tay-trang-lam-sach-sau'),
    (9, 'Xịt Khoáng & Lotion Dưỡng Da', 'xit-khoang-lotion-duong-da'),
    (10, 'Tinh Chất Trẻ Hóa & Chống Lão Hóa', 'tinh-chat-tre-hoa-chong-lao-hoa')
]

for cat_id, name, slug in new_cats:
    cursor.execute("SELECT COUNT(*) FROM Categories WHERE Id = ?", (cat_id,))
    if cursor.fetchone()[0] == 0:
        cursor.execute("INSERT INTO Categories (Id, Name, Slug) VALUES (?, ?, ?)", (cat_id, name, slug))
    else:
        cursor.execute("UPDATE Categories SET Name = ?, Slug = ? WHERE Id = ?", (name, slug, cat_id))

# 3. Reassign products so each category has 5 products (total 50 products across 10 categories)
cat_product_map = {
    1: [1, 2, 3, 4, 5],
    6: [6, 7, 8, 9, 10],
    2: [11, 12, 13, 14, 15],
    7: [16, 17, 18, 19, 20],
    3: [21, 22, 23, 24, 25],
    8: [26, 27, 28, 29, 30],
    4: [31, 32, 33, 34, 35],
    9: [36, 37, 38, 39, 40],
    5: [41, 42, 43, 44, 45],
    10: [46, 47, 48, 49, 50]
}

for cat_id, prod_ids in cat_product_map.items():
    for pid in prod_ids:
        cursor.execute("UPDATE Products SET CategoryId = ? WHERE Id = ?", (cat_id, pid))

conn.commit()

# Verify counts
cursor.execute("SELECT c.Id, c.Name, COUNT(p.Id) FROM Categories c LEFT JOIN Products p ON c.Id = p.CategoryId GROUP BY c.Id ORDER BY c.Id")
print("Updated Categories & Products distribution:")
for row in cursor.fetchall():
    print(f"  Category {row[0]}: {row[1]} -> {row[2]} products")

conn.close()
