import sqlite3

conn = sqlite3.connect('beautyshop.db')
cur = conn.cursor()

cur.execute('SELECT COUNT(*) FROM Orders')
print('Total orders:', cur.fetchone()[0])

cur.execute('SELECT OrderStatus, COUNT(*) FROM Orders GROUP BY OrderStatus')
print('Status distribution:', cur.fetchall())

cur.execute("SELECT SUM(FinalAmount) FROM Orders WHERE OrderStatus='Completed' AND CompletedAt IS NOT NULL")
print('Total Completed Revenue:', f"{cur.fetchone()[0]:,.0f} đ")

cur.execute("SELECT SUM(FinalAmount) FROM Orders WHERE OrderStatus='Completed' AND CompletedAt LIKE '2026-11-01%'")
print('Today Revenue:', f"{cur.fetchone()[0]:,.0f} đ")

cur.execute("""
    SELECT p.Name, SUM(od.Quantity) as TotalSold, SUM(od.Quantity * od.UnitPrice) as TotalRev
    FROM OrderDetails od 
    JOIN Orders o ON od.OrderId = o.Id 
    JOIN ProductVariants pv ON od.VariantId = pv.Id 
    JOIN Products p ON pv.ProductId = p.Id 
    WHERE o.OrderStatus='Completed' AND o.CompletedAt IS NOT NULL 
    GROUP BY pv.ProductId 
    ORDER BY TotalRev DESC 
    LIMIT 5
""")
print('\n--- TOP 5 SẢN PHẨM BÁN CHẠY ---')
for row in cur.fetchall():
    print(f"- {row[0]}: {row[1]} sản phẩm - {row[2]:,.0f} đ")

conn.close()
