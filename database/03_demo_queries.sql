USE [KTX_Demo];
GO

-- Số giường còn trống theo phòng.
SELECT building_code, room_number, total_beds, occupied_beds, available_beds
FROM dbo.vw_RoomOccupancy
ORDER BY building_code, room_number;

-- Sinh viên đang ở và vị trí giường.
SELECT st.student_code, st.full_name, b.building_code, r.room_number, bed.bed_number, s.start_date
FROM dbo.Stays AS s
JOIN dbo.Students AS st ON st.student_id = s.student_id
JOIN dbo.Beds AS bed ON bed.bed_id = s.bed_id
JOIN dbo.Rooms AS r ON r.room_id = bed.room_id
JOIN dbo.Buildings AS b ON b.building_id = r.building_id
WHERE s.stay_status = 'ACTIVE'
ORDER BY b.building_code, r.room_number, bed.bed_number;

-- Hóa đơn và số tiền còn phải trả.
SELECT student_code, full_name, billing_month, amount, paid_amount, balance_due, payment_status
FROM dbo.vw_InvoiceBalances
ORDER BY student_code;

-- Yêu cầu bảo trì chưa hoàn tất.
SELECT b.building_code, r.room_number, mr.title, mr.priority, mr.request_status, mr.created_at
FROM dbo.MaintenanceRequests AS mr
JOIN dbo.Rooms AS r ON r.room_id = mr.room_id
JOIN dbo.Buildings AS b ON b.building_id = r.building_id
WHERE mr.request_status <> 'RESOLVED'
ORDER BY mr.created_at;
GO
