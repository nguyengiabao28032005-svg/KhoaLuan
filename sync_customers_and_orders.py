import sqlite3

DB_PATH = "beautyshop.db"
SECONDARY_DB_PATH = r"c:\Projects\BeautyShop\beautyshop.db"

def sync_customers():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()

    # Danh sách các khách hàng tương ứng với dữ liệu đơn hàng
    demo_customers = [
        ("Trần Thị Thu Thảo", "thuthao.tran@gmail.com", "0905123456"),
        ("Nguyễn Hoàng Nam", "hoangnam.nguyen@gmail.com", "0912345678"),
        ("Phạm Minh Anh", "minhanh.pham@gmail.com", "0987654321"),
        ("Lê Bảo Trâm", "baotram.le@gmail.com", "0934567890"),
        ("Đỗ Gia Hưng", "giahung.do@gmail.com", "0971239876"),
        ("Vũ Phương Linh", "phuonglinh.vu@gmail.com", "0965432198"),
        ("Hoàng Kim Ngân", "kimngan.hoang@gmail.com", "0943218765"),
        ("Bùi Đình Trọng", "dinhtrong.bui@gmail.com", "0921987654"),
        ("Đặng Thúy Kiều", "thuykieu.dang@gmail.com", "0918765432"),
        ("Hồ Ngọc Hà", "ngocha.ho@gmail.com", "0909876999")
    ]

    # Kiểm tra max ID hiện tại của Users
    cur.execute("SELECT COALESCE(MAX(Id), 10) FROM Users")
    max_user_id = cur.fetchone()[0]

    user_map = {} # Tên -> UserId

    # Lấy tài khoản Nguyễn Gia Bảo có sẵn (ID = 2)
    user_map["Nguyễn Gia Bảo"] = 2

    # Mật khẩu mặc định băm (123456)
    default_hash = "$2a$11$9JcyG5YJokKIT1XdR2ssI.Zrz1vboUvuWsadi8g6VjewLMI51uK/a"

    added_users = 0
    for name, email, phone in demo_customers:
        # Kiểm tra xem đã có email này trong Users chưa
        cur.execute("SELECT Id FROM Users WHERE Email = ?", (email,))
        row = cur.fetchone()
        if row:
            user_map[name] = row[0]
        else:
            max_user_id += 1
            cur.execute("""
                INSERT INTO Users (
                    Id, RoleId, FullName, Email, PasswordHash,
                    PhoneNumber, AvatarUrl, IsActive, CreatedAt, Position, SecurityStamp
                ) VALUES (?, 2, ?, ?, ?, ?, NULL, 1, '2026-09-15 10:00:00', NULL, '')
            """, (max_user_id, name, email, default_hash, phone))
            user_map[name] = max_user_id
            added_users += 1

    cur.execute("UPDATE Orders SET PhoneNumber = '0909876999' WHERE CustomerName = 'Hồ Ngọc Hà'")
    
    # Cập nhật trường UserId của từng đơn hàng tương ứng với CustomerName
    updated_orders = 0
    for name, uid in user_map.items():
        cur.execute("UPDATE Orders SET UserId = ? WHERE CustomerName = ?", (uid, name))
        updated_orders += cur.rowcount

    conn.commit()

    # Kiểm tra số lượng khách hàng sau khi cập nhật
    cur.execute("SELECT COUNT(*) FROM Users WHERE RoleId = 2")
    total_customers = cur.fetchone()[0]

    conn.close()

    print(f"✅ Đã thêm mới {added_users} tài khoản khách hàng vào bảng Users.")
    print(f"✅ Đã đồng bộ UserId cho {updated_orders} đơn hàng theo đúng tên khách hàng.")
    print(f"👥 Tổng số khách hàng hiện tại trong hệ thống (RoleId = 2): {total_customers} khách hàng.")

    # Đồng bộ sang database phụ c:\Projects\BeautyShop\beautyshop.db nếu có
    try:
        import os, shutil
        if os.path.exists(SECONDARY_DB_PATH):
            shutil.copyfile(DB_PATH, SECONDARY_DB_PATH)
            print(f"✅ Đã đồng bộ sang database phụ tại {SECONDARY_DB_PATH}")
    except Exception as e:
        print(f"⚠️ Không thể đồng bộ database phụ: {e}")

if __name__ == "__main__":
    sync_customers()
