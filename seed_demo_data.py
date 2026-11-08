import sys
import sqlite3
import datetime

sys.stdout.reconfigure(encoding='utf-8')

db_path = 'KhoaLuan-main/beautyshop.db'
conn = sqlite3.connect(db_path)
cur = conn.cursor()

today = datetime.date.today().strftime('%Y-%m-%d')
now = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

print(f"Seeding demo data for date: {today} at {now}")

# 1. Fetch some real Variant IDs
cur.execute("SELECT Id, RetailPrice FROM ProductVariants LIMIT 6;")
variants = cur.fetchall()
if not variants:
    print("No variants found!")
    exit(1)

v1_id, p1 = variants[0]
v2_id, p2 = variants[1] if len(variants) > 1 else variants[0]
v3_id, p3 = variants[2] if len(variants) > 2 else variants[0]

# ====================================================================
# A. SEED PURCHASE ORDERS & STOCK RECEIPTS (MODULE 1 & 2)
# ====================================================================
cur.execute("DELETE FROM StockReceiptItems WHERE StockReceiptId IN (SELECT Id FROM StockReceipts WHERE ReceiptCode LIKE 'PO-DEMO-%');")
cur.execute("DELETE FROM StockReceipts WHERE ReceiptCode LIKE 'PO-DEMO-%';")

# 1. PO-DEMO-001: PendingApproval (Chờ QL duyệt)
cur.execute("""
INSERT INTO StockReceipts (
    ReceiptCode, ReceiptStage, ReceiptType, StoreId, VariantId, Quantity, ImportPrice,
    SupplierId, Supplier, ImportDate, CreatedAt, CreatedByName,
    TotalAmount, PaidAmount, DebtAmount, PaymentStatus, Note
) VALUES (
    'PO-DEMO-001', 'PendingApproval', 'Domestic', 1, ?, 30, 216666.67,
    1, 'Công ty TNHH L''Oreal Việt Nam', ?, ?, 'Nhân viên Mua hàng',
    6500000, 0, 6500000, 'Unpaid', 'Đơn đặt hàng định kỳ tháng 11 - Chờ Quản lý kho duyệt'
);
""", (v1_id, now, now))
po1_id = cur.lastrowid

cur.execute("""
INSERT INTO StockReceiptItems (StockReceiptId, VariantId, Quantity, ImportPrice, TotalPrice, ActualQuantity, CancelledQuantity, CreatedAt)
VALUES (?, ?, 10, 250000, 2500000, 0, 0, ?),
       (?, ?, 20, 200000, 4000000, 0, 0, ?);
""", (po1_id, v1_id, now, po1_id, v2_id, now))

# 2. PO-DEMO-002: Approved (QL đã duyệt -> Chờ gửi NCC & Cọc)
cur.execute("""
INSERT INTO StockReceipts (
    ReceiptCode, ReceiptStage, ReceiptType, StoreId, VariantId, Quantity, ImportPrice,
    SupplierId, Supplier, ApprovedByName, ApprovedAt,
    ImportDate, CreatedAt, CreatedByName, TotalAmount, PaidAmount, DebtAmount,
    PaymentStatus, Note
) VALUES (
    'PO-DEMO-002', 'Approved', 'Domestic', 1, ?, 30, 300000,
    2, 'Công ty TNHH Estee Lauder Việt Nam', 'Quản lý Trần Hoàng', ?,
    ?, ?, 'Nhân viên Mua hàng', 9000000, 0, 9000000,
    'Unpaid', 'Đã được Quản lý duyệt - Sẵn sàng gửi NCC và đặt cọc'
);
""", (v2_id, now, now, now))
po2_id = cur.lastrowid

cur.execute("""
INSERT INTO StockReceiptItems (StockReceiptId, VariantId, Quantity, ImportPrice, TotalPrice, ActualQuantity, CancelledQuantity, CreatedAt)
VALUES (?, ?, 15, 300000, 4500000, 0, 0, ?),
       (?, ?, 15, 300000, 4500000, 0, 0, ?);
""", (po2_id, v2_id, now, po2_id, v3_id, now))

# 3. PO-DEMO-003: Ordered (Đã gửi NCC & Cọc -> Chờ hàng về đối chiếu PXK)
cur.execute("""
INSERT INTO StockReceipts (
    ReceiptCode, ReceiptStage, ReceiptType, StoreId, VariantId, Quantity, ImportPrice,
    SupplierId, Supplier, ApprovedByName, ApprovedAt,
    DepositAmount, DepositTransferDate, DepositReceivedDate, PaymentMethod,
    ImportDate, CreatedAt, CreatedByName, TotalAmount, PaidAmount, DebtAmount,
    PaymentStatus, Note
) VALUES (
    'PO-DEMO-003', 'Ordered', 'Domestic', 1, ?, 40, 250000,
    3, 'Công ty TNHH Mỹ phẩm Shiseido Việt Nam', 'Quản lý Trần Hoàng', ?,
    3000000, ?, ?, 'Chuyển khoản Vietcombank',
    ?, ?, 'Nhân viên Mua hàng', 10000000, 3000000, 7000000,
    'Partial', 'Đã chuyển cọc 30% - Đang chờ NCC giao hàng kèm Phiếu xuất kho'
);
""", (v1_id, now, today, today, now, now))
po3_id = cur.lastrowid

cur.execute("""
INSERT INTO StockReceiptItems (StockReceiptId, VariantId, Quantity, ImportPrice, TotalPrice, ActualQuantity, CancelledQuantity, CreatedAt)
VALUES (?, ?, 20, 250000, 5000000, 0, 0, ?),
       (?, ?, 20, 250000, 5000000, 0, 0, ?);
""", (po3_id, v1_id, now, po3_id, v3_id, now))

# 4. PO-DEMO-004: Received (Hàng đã về, đối chiếu PXK: đặt 20 nhận 15, hủy 5; thanh toán chỉ tính số thực nhận)
# Đặt: 10 sp v1 (250k) = 2.500.000 + 10 sp v2 (200k) = 2.000.000 -> Tổng đặt ban đầu = 4.500.000
# Thực nhận: 5 sp v1 = 1.250.000 + 10 sp v2 = 2.000.000 -> Tổng thực nhận = 3.250.000 (Hủy 5 sp v1)
# Phiếu thanh toán và tồn kho tính theo số thực nhận: 3.250.000 đ!
cur.execute("""
INSERT INTO StockReceipts (
    ReceiptCode, ReceiptStage, ReceiptType, StoreId, VariantId, Quantity, ImportPrice,
    SupplierId, Supplier, ApprovedByName, ApprovedAt,
    SupplierDeliveryNoteCode, InvoiceNumber, PaymentTransferDate, PaymentReceivedDate, PaymentMethod,
    ImportDate, CreatedAt, CreatedByName, TotalAmount, PaidAmount, DebtAmount,
    PaymentStatus, Note
) VALUES (
    'PO-DEMO-004', 'Received', 'Domestic', 1, ?, 15, 216666.67,
    1, 'Công ty TNHH L''Oreal Việt Nam', 'Quản lý Trần Hoàng', ?,
    'PXK-LOREAL-8899', 'HDGTGT-10492', ?, ?, 'Chuyển khoản Vietcombank',
    ?, ?, 'Thủ kho Nguyễn Văn Nam', 3250000, 3250000, 0,
    'Paid', 'Đã đối chiếu khớp PXK NCC #PXK-LOREAL-8899: Đặt 20, thực nhận 15, hủy 5 sp do NCC thiếu hàng'
);
""", (v1_id, now, today, today, now, now))
po4_id = cur.lastrowid

cur.execute("""
INSERT INTO StockReceiptItems (StockReceiptId, VariantId, Quantity, ImportPrice, TotalPrice, ActualQuantity, CancelledQuantity, ReceivedDate, CreatedAt)
VALUES (?, ?, 10, 250000, 1250000, 5, 5, ?, ?),
       (?, ?, 10, 200000, 2000000, 10, 0, ?, ?);
""", (po4_id, v1_id, now, now, po4_id, v2_id, now, now))

print("Seeded 4 demo purchase orders and stock receipts successfully!")

# ====================================================================
# B. SEED LOGISTICS & ORDERS (MODULE 3 & 4)
# ====================================================================
cur.execute("DELETE FROM OrderDetails WHERE OrderId IN (SELECT Id FROM Orders WHERE OrderCode LIKE 'ORD-DEMO-%');")
cur.execute("DELETE FROM DeliveryTrips WHERE TripCode = 'TRIP-DEMO-HCM01';")
cur.execute("DELETE FROM Orders WHERE OrderCode LIKE 'ORD-DEMO-%';")

# Tạo 1 Chuyến giao nội bộ
cur.execute("""
INSERT INTO DeliveryTrips (
    TripCode, ClusterCode, TripDate, Shift, Status, SettlementStatus, StoreId, ShipperUserId,
    TotalOrders, TotalCodExpected, TotalCodCollected,
    DepartureTime, HandoverStaffName, CreatedAt
) VALUES (
    'TRIP-DEMO-HCM01', 'CLUSTER-HCM-01', ?, 'Morning', 'Completed', 'Settled', 1, 1,
    2, 2350000, 2350000,
    '08:30:00', 'Kho trưởng Lê Hoàng', ?
);
""", (today, now))
trip_id = cur.lastrowid

# Đơn 1: Giao nội bộ TP.HCM - Đã giao & Nộp tiền về kho -> Quyết toán hoàn thành (Completed)
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    InternalDeliveryStaffName, InternalDepartureTime,
    SettlementConfirmedBy, SettlementConfirmedAt,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt, CompletedAt
) VALUES (
    'ORD-DEMO-001', 'Nguyễn Thu Hà (Q.1, TP.HCM)', '0908123456', '120 Nguyễn Huệ, P. Bến Nghé, Quận 1, TP.HCM', 1, 1442,
    1470000, 30000, 0, 1500000,
    'Completed', 'Paid', 'COD', 'Internal', 'Delivered', 0,
    'Lê Văn Tốc (Shipper)', '08:30:00',
    'Thủ quỹ Nguyễn Mai', ?,
    0, NULL, 1500000, ?, ?, ?,
    ?, ?, ?
);
""", (now, today, today, now, now, now, now))
ord1_id = cur.lastrowid

# Đơn 2: Giao nội bộ TP.HCM - Shipper đã giao thành công, đang giữ COD chờ nộp về kho
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    InternalDeliveryStaffName, InternalDepartureTime,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt
) VALUES (
    'ORD-DEMO-002', 'Trần Minh Quân (Bình Thạnh)', '0912345678', '45 Ung Văn Khiêm, P.25, Bình Thạnh, TP.HCM', 1, 1444,
    820000, 30000, 0, 850000,
    'Delivering', 'Unpaid', 'COD', 'Internal', 'Delivered', 0,
    'Lê Văn Tốc (Shipper)', '08:30:00',
    0, NULL, 0, NULL, NULL, ?,
    ?, ?
);
""", (now, now, now))
ord2_id = cur.lastrowid

# Đơn 3: Bên thứ 3 (GHN - Đi tỉnh) - GHN đã chuyển khoản đối soát -> Hoàn thành (Completed)
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    CarrierStaffName, CarrierHandoverAt, GhnOrderCode,
    CarrierPaymentReceivedDate, CarrierPaymentMethod,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt, CompletedAt
) VALUES (
    'ORD-DEMO-003', 'Phạm Thị Bích (Đà Nẵng)', '0933987654', '88 Bạch Đằng, Hải Châu, Đà Nẵng', 1, 1500,
    2150000, 50000, 0, 2200000,
    'Completed', 'Paid', 'COD', 'ThirdParty', 'Delivered', 0,
    'Trần Giao Hàng (Bưu tá GHN)', ?, 'GHN-DN-99210',
    ?, 'Chuyển khoản Vietcombank',
    0, NULL, 2200000, ?, ?, ?,
    ?, ?, ?
);
""", (now, today, today, today, now, now, now, now))
ord3_id = cur.lastrowid

# Đơn 4: Bên thứ 3 (GHN - Đi tỉnh) - Đã bàn giao bưu tá, đang vận chuyển
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    CarrierStaffName, CarrierHandoverAt, GhnOrderCode,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt
) VALUES (
    'ORD-DEMO-004', 'Hoàng Kim Long (Cần Thơ)', '0977654321', '33 Đại lộ Hòa Bình, Ninh Kiều, Cần Thơ', 1, 1600,
    1100000, 50000, 0, 1150000,
    'Shipping', 'Unpaid', 'COD', 'ThirdParty', 'Shipping', 0,
    'Trần Giao Hàng (Bưu tá GHN)', ?, 'GHN-CT-88301',
    0, NULL, 0, NULL, NULL, ?,
    ?, ?
);
""", (now, now, now, now))
ord4_id = cur.lastrowid

# Đơn 5: Đơn có Đặt Cọc - Khách đã cọc 1.000.000 đ qua chuyển khoản, đang đóng gói xử lý
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt
) VALUES (
    'ORD-DEMO-005', 'Đỗ Mỹ Linh (Quận 7, TP.HCM)', '0988112233', '101 Nguyễn Thị Thập, Tân Phú, Quận 7, TP.HCM', 1, 1450,
    3470000, 30000, 0, 3500000,
    'Processing', 'Partial', 'BankTransfer', 'Internal', 'Pending', 0,
    1000000, ?, 1000000, ?, NULL, ?,
    ?, ?
);
""", (today, today, now, now, now))
ord5_id = cur.lastrowid

# Đơn 6: Đơn Đã Hủy - Khách hủy đơn, thể hiện để đối soát khớp tổng tiền
cur.execute("""
INSERT INTO Orders (
    OrderCode, CustomerName, PhoneNumber, ShippingAddress, StoreId, ToDistrictId,
    TotalAmount, DeliveryFee, DiscountAmount, FinalAmount,
    OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, DeliveryStatus, FailedDeliveryAttempts,
    DepositAmount, DepositDate, PaymentAmount, PaymentDate, MoneyReceivedDate, StatusUpdatedAt,
    CreatedAt, UpdatedAt
) VALUES (
    'ORD-DEMO-006', 'Vũ Đình Trọng (Thủ Đức)', '0944556677', '22 Võ Văn Ngân, Linh Chiểu, TP. Thủ Đức', 1, 1448,
    920000, 30000, 0, 950000,
    'Cancelled', 'Unpaid', 'COD', 'Internal', 'Cancelled', 0,
    0, NULL, 0, NULL, NULL, ?,
    ?, ?
);
""", (now, now, now))
ord6_id = cur.lastrowid

# Chi tiết đơn hàng OrderDetails
for o_id in [ord1_id, ord2_id, ord3_id, ord4_id, ord5_id, ord6_id]:
    cur.execute("""
    INSERT INTO OrderDetails (OrderId, VariantId, Quantity, UnitPrice, CostPrice)
    VALUES (?, ?, 2, 400000, 250000);
    """, (o_id, v1_id))

conn.commit()
conn.close()

print("Successfully seeded all demo orders and delivery data!")
