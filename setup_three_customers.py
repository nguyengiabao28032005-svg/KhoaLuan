import sqlite3
import os
import shutil

DB_PATH = "beautyshop.db"
SECONDARY_DB_PATH = r"c:\Projects\BeautyShop\beautyshop.db"

# Hash BCrypt chuẩn cho mật khẩu "123456" đã được test trong ResetAllPasswordsTest
HASH_123456 = "$2a$11$OJLlAeiRy6h83l5Bm4MP4uQzmy4x32bkn4YinMHz5pBrAzncfH7f."

def setup_three_customers():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()

    # 1. Định nghĩa 3 khách hàng duy nhất
    three_customers = [
        {
            "id": 2,
            "name": "Nguyễn Gia Bảo",
            "email": "nguyengiabao28032005@gmail.com",
            "phone": "0988111222",
            "address": "120 Nguyễn Đình Chiểu, Phường Đa Kao, Quận 1, TP.HCM"
        },
        {
            "id": 11,
            "name": "Trần Thị Thu Thảo",
            "email": "thuthao.tran@gmail.com",
            "phone": "0905123456",
            "address": "123 Lê Lợi, Phường Bến Nghé, Quận 1, TP.HCM"
        },
        {
            "id": 12,
            "name": "Nguyễn Hoàng Nam",
            "email": "hoangnam.nguyen@gmail.com",
            "phone": "0912345678",
            "address": "456 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP.HCM"
        }
    ]

    target_ids = [c["id"] for c in three_customers]

    # Xóa các tài khoản khách hàng thừa (RoleId = 2 và không thuộc 3 ID trên)
    cur.execute(f"DELETE FROM Users WHERE RoleId = 2 AND Id NOT IN ({','.join(map(str, target_ids))})")
    deleted_count = cur.rowcount
    print(f"🗑️ Đã xóa {deleted_count} tài khoản khách hàng demo thừa.")

    # Đảm bảo 3 tài khoản tồn tại và có thông tin chính xác, mật khẩu 123456
    for c in three_customers:
        cur.execute("SELECT Id FROM Users WHERE Id = ?", (c["id"],))
        existing = cur.fetchone()
        if existing:
            cur.execute("""
                UPDATE Users
                SET FullName = ?, Email = ?, PasswordHash = ?, PhoneNumber = ?, IsActive = 1, RoleId = 2
                WHERE Id = ?
            """, (c["name"], c["email"], HASH_123456, c["phone"], c["id"]))
        else:
            cur.execute("""
                INSERT INTO Users (Id, RoleId, FullName, Email, PasswordHash, PhoneNumber, AvatarUrl, IsActive, CreatedAt, Position, SecurityStamp)
                VALUES (?, 2, ?, ?, ?, ?, NULL, 1, '2026-09-01 08:00:00', NULL, '')
            """, (c["id"], c["name"], c["email"], HASH_123456, c["phone"]))

    # 2. Cập nhật lại toàn bộ đơn hàng trong Orders:
    # Chỉ xuất hiện đúng 3 cái tên đó (chia đều lần lượt cho 3 khách hàng)
    cur.execute("SELECT Id FROM Orders ORDER BY Id ASC")
    order_ids = [r[0] for r in cur.fetchall()]

    for idx, oid in enumerate(order_ids):
        cust = three_customers[idx % len(three_customers)]
        cur.execute("""
            UPDATE Orders
            SET CustomerName = ?, PhoneNumber = ?, ShippingAddress = ?, UserId = ?
            WHERE Id = ?
        """, (cust["name"], cust["phone"], cust["address"], cust["id"], oid))

    conn.commit()

    # Kiểm tra lại số lượng khách hàng RoleId = 2
    cur.execute("SELECT Id, FullName, Email, PhoneNumber FROM Users WHERE RoleId = 2")
    active_customers = cur.fetchall()

    print("\n👥 DANH SÁCH 3 KHÁCH HÀNG DUY NHẤT:")
    for cust in active_customers:
        cur.execute("SELECT COUNT(*) FROM Orders WHERE UserId = ?", (cust[0],))
        order_count = cur.fetchone()[0]
        print(f"- ID #{cust[0]}: {cust[1]} | Email: {cust[2]} | SĐT: {cust[3]} | Số đơn hàng: {order_count} | Mật khẩu: 123456")

    # Kiểm tra tên người nhận trong Orders
    cur.execute("SELECT DISTINCT CustomerName FROM Orders")
    distinct_names = [r[0] for r in cur.fetchall()]
    print(f"\n📦 Các tên người nhận xuất hiện trên toàn bộ {len(order_ids)} đơn hàng: {distinct_names}")

    conn.close()

    # Đồng bộ sang database phụ
    if os.path.exists(SECONDARY_DB_PATH):
        try:
            shutil.copyfile(DB_PATH, SECONDARY_DB_PATH)
            print(f"✅ Đã đồng bộ sang database phụ tại {SECONDARY_DB_PATH}")
        except Exception as e:
            print(f"⚠️ Đồng bộ database phụ: {e}")

if __name__ == "__main__":
    setup_three_customers()
