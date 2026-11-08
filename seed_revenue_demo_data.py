import sqlite3
import random
from datetime import datetime, timedelta

DB_PATH = "beautyshop.db"
SECONDARY_DB_PATH = r"c:\Projects\BeautyShop\beautyshop.db"

def seed_demo_orders():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()

    # Lấy danh sách sản phẩm và variant có sẵn trong CSDL
    cur.execute("""
        SELECT pv.Id, pv.ProductId, p.Name, pv.RetailPrice, pv.OriginalPrice, pv.SKU, pv.ColorName
        FROM ProductVariants pv
        JOIN Products p ON pv.ProductId = p.Id
        WHERE pv.RetailPrice > 0
    """)
    variants = cur.fetchall()
    if not variants:
        print("Không tìm thấy biến thể sản phẩm nào!")
        return

    # Danh sách khách hàng mẫu
    customers = [
        ("Trần Thị Thu Thảo", "0905123456", "123 Lê Lợi, Phường Bến Nghé, Quận 1, TP.HCM"),
        ("Nguyễn Hoàng Nam", "0912345678", "456 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP.HCM"),
        ("Phạm Minh Anh", "0987654321", "78 Quang Trung, Phường 10, Quận Gò Vấp, TP.HCM"),
        ("Lê Bảo Trâm", "0934567890", "12 Hai Bà Trưng, Phường Tràng Tiền, Quận Hoàn Kiếm, Hà Nội"),
        ("Đỗ Gia Hưng", "0971239876", "89 Cầu Giấy, Phường Quan Hoa, Quận Cầu Giấy, Hà Nội"),
        ("Vũ Phương Linh", "0965432198", "234 Trần Phú, Phường Hải Châu 1, Quận Hải Châu, Đà Nẵng"),
        ("Hoàng Kim Ngân", "0943218765", "56 Nguyễn Văn Cừ, Phường An Khánh, Quận Ninh Kiều, Cần Thơ"),
        ("Bùi Đình Trọng", "0921987654", "304 Cách Mạng Tháng 8, Phường 10, Quận 3, TP.HCM"),
        ("Đặng Thúy Kiều", "0918765432", "15 Pasteur, Phường 6, Quận 3, TP.HCM"),
        ("Hồ Ngọc Hà", "0909876543", "720A Điện Biên Phủ, Phường 22, Quận Bình Thạnh, TP.HCM")
    ]

    # Dọn dẹp dữ liệu demo cũ nếu có (chỉ giữ lại đơn nguyên thủy 5, 6)
    cur.execute("DELETE FROM OrderDetails WHERE OrderId > 6")
    cur.execute("DELETE FROM Orders WHERE Id > 6")
    conn.commit()

    # Kiểm tra max ID hiện tại
    cur.execute("SELECT COALESCE(MAX(Id), 6) FROM Orders")
    max_order_id = cur.fetchone()[0]
    
    cur.execute("SELECT COALESCE(MAX(Id), 6) FROM OrderDetails")
    max_detail_id = cur.fetchone()[0]

    # Tạo các đơn hàng demo với ngày hoàn thành phân bổ thực tế:
    # Mục tiêu:
    # 1. Hôm nay (2026-11-01): 4 đơn hoàn thành -> Doanh thu hôm nay
    # 2. 6 ngày trước (2026-10-26 -> 2026-10-31): mỗi ngày 2-4 đơn hoàn thành -> Vẽ biểu đồ 7 ngày sinh động
    # 3. Giữa tháng 10 (2026-10-15 -> 2026-10-22): 6 đơn hoàn thành -> Tích lũy tổng doanh thu
    # 4. Các trạng thái khác (Pending, Confirmed, Processing, Shipping, Cancelled, Failed):
    #    để kiểm chứng nguyên tắc "Chỉ đơn Completed mới tính doanh thu" và hiển thị biểu đồ tròn đa sắc thái.

    order_specs = [
        # --- ĐƠN HOÀN THÀNH HÔM NAY (2026-11-01) ---
        {
            "status": "Completed",
            "created": "2026-11-01 08:15:00",
            "completed": "2026-11-01 09:30:00",
            "variant_ids": [1], # Dior Addict Lip Glow (890k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-11-01 10:20:00",
            "completed": "2026-11-01 11:45:00",
            "variant_ids": [3, 4], # MAC Ruby Woo (550k) + Maybelline (219k)
            "quantities": [2, 1], # 550*2 + 219 = 1319k
            "delivery_fee": 0,
            "discount": 50000,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-11-01 13:00:00",
            "completed": "2026-11-01 15:30:00",
            "variant_ids": [10], # 3CE Velvet Tint (320k)
            "quantities": [2], # 640k
            "delivery_fee": 25000,
            "discount": 0,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-11-01 16:10:00",
            "completed": "2026-11-01 18:25:00",
            "variant_ids": [6], # YSL Slim (1150k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 100000,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },

        # --- 6 NGÀY GẦN NHẤT (2026-10-26 ĐẾN 2026-10-31) ---
        # Ngày 31/10
        {
            "status": "Completed",
            "created": "2026-10-30 09:00:00",
            "completed": "2026-10-31 10:15:00",
            "variant_ids": [1, 10], # Dior (890k) + 3CE (320k) = 1210k
            "quantities": [1, 1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-30 14:00:00",
            "completed": "2026-10-31 16:30:00",
            "variant_ids": [3], # MAC (550k)
            "quantities": [2],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-31 08:30:00",
            "completed": "2026-10-31 20:00:00",
            "variant_ids": [2], # Chanel Baume (990k)
            "quantities": [1],
            "delivery_fee": 25000,
            "discount": 0,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },

        # Ngày 30/10
        {
            "status": "Completed",
            "created": "2026-10-29 10:00:00",
            "completed": "2026-10-30 11:20:00",
            "variant_ids": [1, 3], # Dior (890k) + MAC (550k) = 1440k
            "quantities": [1, 1],
            "delivery_fee": 30000,
            "discount": 50000,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-29 15:00:00",
            "completed": "2026-10-30 14:40:00",
            "variant_ids": [4, 5], # Maybelline (219k) + L'Oreal (249k) = 468k
            "quantities": [1, 1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-30 08:00:00",
            "completed": "2026-10-30 17:15:00",
            "variant_ids": [7], # Estee Lauder (850k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },

        # Ngày 29/10
        {
            "status": "Completed",
            "created": "2026-10-28 11:00:00",
            "completed": "2026-10-29 10:00:00",
            "variant_ids": [6], # YSL Slim (1150k)
            "quantities": [2],
            "delivery_fee": 0,
            "discount": 100000,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-28 14:20:00",
            "completed": "2026-10-29 14:15:00",
            "variant_ids": [10], # 3CE (320k)
            "quantities": [3],
            "delivery_fee": 30000,
            "discount": 30000,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-29 08:30:00",
            "completed": "2026-10-29 16:40:00",
            "variant_ids": [1], # Dior (890k)
            "quantities": [1],
            "delivery_fee": 25000,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-29 09:15:00",
            "completed": "2026-10-29 19:10:00",
            "variant_ids": [3], # MAC (550k)
            "quantities": [1],
            "delivery_fee": 20000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },

        # Ngày 28/10
        {
            "status": "Completed",
            "created": "2026-10-27 10:00:00",
            "completed": "2026-10-28 11:00:00",
            "variant_ids": [2], # Chanel (990k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-27 16:30:00",
            "completed": "2026-10-28 15:45:00",
            "variant_ids": [4, 10], # Maybelline (219k) + 3CE (320k) = 539k
            "quantities": [2, 1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },

        # Ngày 27/10
        {
            "status": "Completed",
            "created": "2026-10-26 09:00:00",
            "completed": "2026-10-27 10:30:00",
            "variant_ids": [1, 4], # Dior (890k) + Maybelline (219k)
            "quantities": [1, 2],
            "delivery_fee": 30000,
            "discount": 50000,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-26 13:45:00",
            "completed": "2026-10-27 14:15:00",
            "variant_ids": [8], # Lancome (1200k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-27 08:00:00",
            "completed": "2026-10-27 18:00:00",
            "variant_ids": [3], # MAC (550k)
            "quantities": [1],
            "delivery_fee": 25000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },

        # Ngày 26/10
        {
            "status": "Completed",
            "created": "2026-10-25 11:00:00",
            "completed": "2026-10-26 11:30:00",
            "variant_ids": [1], # Dior (890k)
            "quantities": [2], # 1780k
            "delivery_fee": 0,
            "discount": 80000,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-25 15:30:00",
            "completed": "2026-10-26 16:45:00",
            "variant_ids": [9], # Shiseido (750k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },

        # --- CÁC ĐƠN TRƯỚC ĐÓ TRONG THÁNG 10 (LỊCH SỬ TÍCH LŨY) ---
        {
            "status": "Completed",
            "created": "2026-10-14 10:00:00",
            "completed": "2026-10-15 14:00:00",
            "variant_ids": [1, 3], # Dior (890k) + MAC (550k)
            "quantities": [1, 1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-19 09:30:00",
            "completed": "2026-10-20 15:30:00",
            "variant_ids": [6], # YSL (1150k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        {
            "status": "Completed",
            "created": "2026-10-21 14:00:00",
            "completed": "2026-10-22 11:20:00",
            "variant_ids": [3, 10], # MAC (550k) + 3CE (320k)
            "quantities": [2, 1], # 1420k
            "delivery_fee": 25000,
            "discount": 0,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },

        # --- CÁC ĐƠN TRẠNG THÁI KHÁC (KHÔNG HOÀN THÀNH -> KHÔNG TÍNH DOANH THU) ---
        # 1. Chờ duyệt (Pending)
        {
            "status": "Pending",
            "created": "2026-11-01 10:45:00",
            "completed": None,
            "variant_ids": [1], # Dior (890k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        # 2. Đã xác nhận (Confirmed)
        {
            "status": "Confirmed",
            "created": "2026-11-01 11:30:00",
            "completed": None,
            "variant_ids": [3], # MAC (550k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        {
            "status": "Confirmed",
            "created": "2026-11-01 14:15:00",
            "completed": None,
            "variant_ids": [2], # Chanel (990k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        # 3. Đang xử lý / Đóng gói (Processing)
        {
            "status": "Processing",
            "created": "2026-11-01 09:10:00",
            "completed": None,
            "variant_ids": [10, 4], # 3CE (320k) + Maybelline (219k)
            "quantities": [1, 1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        {
            "status": "Processing",
            "created": "2026-10-31 16:50:00",
            "completed": None,
            "variant_ids": [7], # Estee Lauder (850k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 50000,
            "payment_method": "Banking",
            "payment_status": "Paid"
        },
        # 4. Đang giao hàng (Shipping)
        {
            "status": "Shipping",
            "created": "2026-10-31 14:00:00",
            "completed": None,
            "variant_ids": [1], # Dior (890k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        {
            "status": "Shipping",
            "created": "2026-10-31 11:20:00",
            "completed": None,
            "variant_ids": [6], # YSL (1150k)
            "quantities": [1],
            "delivery_fee": 0,
            "discount": 0,
            "payment_method": "VNPay",
            "payment_status": "Paid"
        },
        # 5. Giao thất bại (Failed)
        {
            "status": "Failed",
            "created": "2026-10-29 08:00:00",
            "completed": None,
            "variant_ids": [3], # MAC (550k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        # 6. Đã hủy (Cancelled)
        {
            "status": "Cancelled",
            "created": "2026-10-28 10:30:00",
            "completed": None,
            "variant_ids": [8], # Lancome (1200k)
            "quantities": [1],
            "delivery_fee": 30000,
            "discount": 100000,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        },
        {
            "status": "Cancelled",
            "created": "2026-10-30 15:20:00",
            "completed": None,
            "variant_ids": [5], # L'Oreal (249k)
            "quantities": [2],
            "delivery_fee": 30000,
            "discount": 0,
            "payment_method": "COD",
            "payment_status": "Unpaid"
        }
    ]

    variant_dict = {v[0]: v for v in variants}
    order_id = max_order_id
    detail_id = max_detail_id

    total_inserted_orders = 0
    total_inserted_details = 0
    completed_revenue = 0
    today_revenue = 0
    total_cogs = 0

    for idx, spec in enumerate(order_specs, start=1):
        order_id += 1
        cust = customers[(idx - 1) % len(customers)]
        order_code = f"BST{order_id:04d}{random.randint(1000, 9999)}"

        # Tính tổng tiền hàng và giá vốn
        order_items = []
        order_total = 0
        order_cogs = 0

        for var_id, qty in zip(spec["variant_ids"], spec["quantities"]):
            v = variant_dict[var_id]
            price = v[3]
            # Đặt giá vốn thực tế ~65% giá bán lẻ để phản ánh đúng biên lợi nhuận kinh doanh mỹ phẩm (35%)
            cost = int(price * 0.65)
            order_total += price * qty
            order_cogs += cost * qty
            order_items.append((var_id, qty, price, cost))

        final_amount = order_total + spec["delivery_fee"] - spec["discount"]

        # Insert Order
        cur.execute("""
            INSERT INTO Orders (
                Id, CartId, CreatedAt, CustomerName, DeliveryFee, DeliveryShift,
                DeliveryStatus, DeliveryZoneId, DiscountAmount, ExpectedDeliveryDate,
                FailedDeliveryAttempts, FinalAmount, GhnOrderCode, OrderCode,
                OrderStatus, PaymentMethod, PaymentStatus, PhoneNumber,
                ShippingAddress, StoreId, ToDistrictId, ToWardCode, TotalAmount,
                UpdatedAt, UserId, CompletedAt
            ) VALUES (
                ?, NULL, ?, ?, ?, ?,
                ?, NULL, ?, ?,
                0, ?, NULL, ?,
                ?, ?, ?, ?,
                ?, 1, 1442, '21307', ?,
                ?, 2, ?
            )
        """, (
            order_id,
            spec["created"],
            cust[0],
            spec["delivery_fee"],
            "Afternoon" if "13:00" <= spec["created"][11:16] <= "18:00" else "Morning",
            "Delivered" if spec["status"] == "Completed" else ("Failed" if spec["status"] == "Failed" else "Ready"),
            spec["discount"],
            spec["created"][:10] + " 00:00:00",
            final_amount,
            order_code,
            spec["status"],
            spec["payment_method"],
            spec["payment_status"],
            cust[1],
            cust[2],
            order_total,
            spec["completed"] if spec["completed"] else spec["created"],
            spec["completed"]
        ))
        total_inserted_orders += 1

        # Insert OrderDetails
        for var_id, qty, unit_price, cost_price in order_items:
            detail_id += 1
            cur.execute("""
                INSERT INTO OrderDetails (Id, OrderId, VariantId, Quantity, UnitPrice, CostPrice)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (detail_id, order_id, var_id, qty, unit_price, str(cost_price)))
            total_inserted_details += 1

        if spec["status"] == "Completed" and spec["completed"]:
            completed_revenue += final_amount
            total_cogs += order_cogs
            if spec["completed"].startswith("2026-11-01"):
                today_revenue += final_amount

    conn.commit()
    conn.close()

    print(f"✅ Đã thêm thành công {total_inserted_orders} đơn hàng và {total_inserted_details} chi tiết đơn vào {DB_PATH}.")
    print(f"📊 Doanh thu hoàn thành (Toàn thời gian): {completed_revenue:,.0f} đ")
    print(f"📊 Doanh thu hoàn thành hôm nay (01/11/2026): {today_revenue:,.0f} đ")
    print(f"📊 Tổng giá vốn hàng bán (COGS): {total_cogs:,.0f} đ")
    print(f"📊 Lợi nhuận gộp ước tính: {completed_revenue - total_cogs:,.0f} đ")

    # Đồng bộ sang database phụ c:\Projects\BeautyShop\beautyshop.db nếu có
    try:
        import os
        import shutil
        if os.path.exists(SECONDARY_DB_PATH):
            shutil.copyfile(DB_PATH, SECONDARY_DB_PATH)
            print(f"✅ Đã đồng bộ sang database phụ tại {SECONDARY_DB_PATH}")
    except Exception as e:
        print(f"⚠️ Không thể đồng bộ database phụ: {e}")

if __name__ == "__main__":
    seed_demo_orders()
