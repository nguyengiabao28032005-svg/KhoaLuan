import sqlite3

conn = sqlite3.connect('beautyshop.db')
cursor = conn.cursor()

def add_col(table, col, col_type):
    cursor.execute(f"PRAGMA table_info({table})")
    cols = [r[1] for r in cursor.fetchall()]
    if col not in cols:
        cursor.execute(f"ALTER TABLE {table} ADD COLUMN {col} {col_type}")
        print(f"Added {col} to {table}")

# StockReceipts
add_col('StockReceipts', 'ReceiptStage', "TEXT DEFAULT 'Received'")
add_col('StockReceipts', 'ApprovedByUserId', 'INTEGER')
add_col('StockReceipts', 'ApprovedByName', 'TEXT')
add_col('StockReceipts', 'ApprovedAt', 'TEXT')
add_col('StockReceipts', 'DepositAmount', 'NUMERIC DEFAULT 0')
add_col('StockReceipts', 'DepositTransferDate', 'TEXT')
add_col('StockReceipts', 'DepositReceivedDate', 'TEXT')
add_col('StockReceipts', 'PaymentTransferDate', 'TEXT')
add_col('StockReceipts', 'PaymentReceivedDate', 'TEXT')
add_col('StockReceipts', 'PaymentMethod', 'TEXT')
add_col('StockReceipts', 'SupplierDeliveryNoteCode', 'TEXT')

# StockReceiptItems
add_col('StockReceiptItems', 'ActualQuantity', 'INTEGER DEFAULT 0')
add_col('StockReceiptItems', 'ReceivedDate', 'TEXT')
add_col('StockReceiptItems', 'CancelledQuantity', 'INTEGER DEFAULT 0')

# Orders
add_col('Orders', 'DepositAmount', 'NUMERIC DEFAULT 0')
add_col('Orders', 'DepositDate', 'TEXT')
add_col('Orders', 'PaymentAmount', 'NUMERIC DEFAULT 0')
add_col('Orders', 'PaymentDate', 'TEXT')
add_col('Orders', 'MoneyReceivedDate', 'TEXT')
add_col('Orders', 'StatusUpdatedAt', 'TEXT')
add_col('Orders', 'DeliveryType', "TEXT DEFAULT 'Internal'")
add_col('Orders', 'CarrierStaffName', 'TEXT')
add_col('Orders', 'CarrierHandoverAt', 'TEXT')
add_col('Orders', 'CarrierPaymentReceivedDate', 'TEXT')
add_col('Orders', 'CarrierPaymentMethod', 'TEXT')
add_col('Orders', 'InternalDeliveryStaffName', 'TEXT')
add_col('Orders', 'InternalDepartureTime', 'TEXT')
add_col('Orders', 'SettlementConfirmedBy', 'TEXT')
add_col('Orders', 'SettlementConfirmedAt', 'TEXT')

# DeliveryTrips
add_col('DeliveryTrips', 'DepartureTime', 'TEXT')
add_col('DeliveryTrips', 'HandoverStaffName', 'TEXT')

# Backfill existing data
cursor.execute("UPDATE StockReceiptItems SET ActualQuantity = Quantity WHERE ActualQuantity = 0 OR ActualQuantity IS NULL")
cursor.execute("UPDATE StockReceipts SET ReceiptStage = 'Received' WHERE ReceiptStage IS NULL")
cursor.execute("UPDATE Orders SET MoneyReceivedDate = CompletedAt WHERE CompletedAt IS NOT NULL AND MoneyReceivedDate IS NULL")
cursor.execute("UPDATE Orders SET StatusUpdatedAt = UpdatedAt WHERE StatusUpdatedAt IS NULL")
cursor.execute("UPDATE Orders SET DeliveryType = CASE WHEN ShippingAddress LIKE '%Hồ Chí Minh%' OR ShippingAddress LIKE '%TP.HCM%' OR ShippingAddress LIKE '%HCM%' THEN 'Internal' ELSE 'ThirdParty' END WHERE DeliveryType IS NULL")

conn.commit()
conn.close()
print("Schema update completed successfully.")
