export interface Choice { value: string; label: string; }
export interface FieldConfig {
  key: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'datetime' | 'select' | 'lookup' | 'textarea';
  required?: boolean;
  lookup?: string;
  options?: Choice[];
  defaultValue?: string | number;
  min?: number;
  step?: string;
}
export interface ColumnConfig {
  key: string;
  label: string;
  format?: 'date' | 'datetime' | 'money' | 'status';
}
export interface ResourceConfig {
  title: string;
  description: string;
  singular: string;
  columns: ColumnConfig[];
  fields: FieldConfig[];
}

const availability: Choice[] = [
  { value: 'AVAILABLE', label: 'Sẵn sàng' },
  { value: 'MAINTENANCE', label: 'Bảo trì' }
];

export const resourceConfigs: Record<string, ResourceConfig> = {
  buildings: {
    title: 'Tòa nhà', singular: 'tòa nhà', description: 'Quản lý các khu nhà trong ký túc xá.',
    columns: [
      { key: 'buildingCode', label: 'Mã tòa' }, { key: 'buildingName', label: 'Tên tòa' },
      { key: 'address', label: 'Địa chỉ' }, { key: 'genderPolicy', label: 'Khu vực', format: 'status' },
      { key: 'floorCount', label: 'Số tầng' }
    ],
    fields: [
      { key: 'buildingCode', label: 'Mã tòa', type: 'text' },
      { key: 'buildingName', label: 'Tên tòa nhà', type: 'text' },
      { key: 'address', label: 'Địa chỉ', type: 'text' },
      { key: 'genderPolicy', label: 'Khu vực', type: 'select', defaultValue: 'MIXED', options: [
        { value: 'MALE', label: 'Nam' }, { value: 'FEMALE', label: 'Nữ' }, { value: 'MIXED', label: 'Nam và nữ' }
      ] },
      { key: 'floorCount', label: 'Số tầng', type: 'number', min: 1 }
    ]
  },
  rooms: {
    title: 'Phòng ở', singular: 'phòng', description: 'Theo dõi phòng, giá thuê và tình trạng sử dụng.',
    columns: [
      { key: 'buildingCode', label: 'Tòa' }, { key: 'roomNumber', label: 'Phòng' },
      { key: 'floorNumber', label: 'Tầng' }, { key: 'monthlyRate', label: 'Giá/tháng', format: 'money' },
      { key: 'occupiedBeds', label: 'Đang ở' }, { key: 'availableBeds', label: 'Giường trống' },
      { key: 'roomStatus', label: 'Trạng thái', format: 'status' }
    ],
    fields: [
      { key: 'buildingId', label: 'Tòa nhà', type: 'lookup', lookup: 'buildings' },
      { key: 'roomNumber', label: 'Số phòng', type: 'text' },
      { key: 'floorNumber', label: 'Tầng', type: 'number', min: 1 },
      { key: 'monthlyRate', label: 'Giá mỗi tháng (₫)', type: 'number', min: 1, step: '1000' },
      { key: 'roomStatus', label: 'Trạng thái', type: 'select', defaultValue: 'AVAILABLE', options: [
        ...availability, { value: 'CLOSED', label: 'Đóng' }
      ] }
    ]
  },
  beds: {
    title: 'Giường ở', singular: 'giường', description: 'Sắp xếp giường và xem người đang sử dụng.',
    columns: [
      { key: 'roomLabel', label: 'Phòng' }, { key: 'bedNumber', label: 'Số giường' },
      { key: 'occupantName', label: 'Sinh viên đang ở' }, { key: 'bedStatus', label: 'Trạng thái', format: 'status' }
    ],
    fields: [
      { key: 'roomId', label: 'Phòng', type: 'lookup', lookup: 'rooms' },
      { key: 'bedNumber', label: 'Số giường', type: 'text' },
      { key: 'bedStatus', label: 'Trạng thái', type: 'select', defaultValue: 'AVAILABLE', options: availability }
    ]
  },
  students: {
    title: 'Sinh viên', singular: 'sinh viên', description: 'Hồ sơ sinh viên đăng ký ký túc xá.',
    columns: [
      { key: 'studentCode', label: 'Mã SV' }, { key: 'fullName', label: 'Họ tên' },
      { key: 'faculty', label: 'Khoa' }, { key: 'phone', label: 'Điện thoại' },
      { key: 'currentBed', label: 'Đang ở' }, { key: 'studentStatus', label: 'Trạng thái', format: 'status' }
    ],
    fields: [
      { key: 'studentCode', label: 'Mã sinh viên', type: 'text' },
      { key: 'fullName', label: 'Họ và tên', type: 'text' },
      { key: 'dateOfBirth', label: 'Ngày sinh', type: 'date' },
      { key: 'gender', label: 'Giới tính', type: 'select', options: [
        { value: 'MALE', label: 'Nam' }, { value: 'FEMALE', label: 'Nữ' }
      ] },
      { key: 'phone', label: 'Số điện thoại', type: 'text', required: false },
      { key: 'email', label: 'Email', type: 'text', required: false },
      { key: 'faculty', label: 'Khoa', type: 'text' },
      { key: 'studentStatus', label: 'Trạng thái', type: 'select', defaultValue: 'ACTIVE', options: [
        { value: 'ACTIVE', label: 'Đang học' }, { value: 'INACTIVE', label: 'Ngừng hoạt động' }
      ] }
    ]
  },
  stays: {
    title: 'Lượt ở', singular: 'lượt ở', description: 'Nhận phòng, chuyển giường và kết thúc lượt ở.',
    columns: [
      { key: 'studentCode', label: 'Mã SV' }, { key: 'studentName', label: 'Sinh viên' },
      { key: 'bedLabel', label: 'Vị trí' }, { key: 'startDate', label: 'Nhận phòng', format: 'date' },
      { key: 'endDate', label: 'Trả phòng', format: 'date' }, { key: 'stayStatus', label: 'Trạng thái', format: 'status' }
    ],
    fields: [
      { key: 'studentId', label: 'Sinh viên', type: 'lookup', lookup: 'students' },
      { key: 'bedId', label: 'Giường', type: 'lookup', lookup: 'beds' },
      { key: 'startDate', label: 'Ngày nhận phòng', type: 'date' },
      { key: 'endDate', label: 'Ngày trả phòng', type: 'date', required: false },
      { key: 'stayStatus', label: 'Trạng thái', type: 'select', defaultValue: 'ACTIVE', options: [
        { value: 'ACTIVE', label: 'Đang ở' }, { value: 'ENDED', label: 'Đã trả phòng' },
        { value: 'CANCELLED', label: 'Đã hủy' }
      ] }
    ]
  },
  invoices: {
    title: 'Hóa đơn', singular: 'hóa đơn', description: 'Tiền phòng theo tháng và công nợ còn lại.',
    columns: [
      { key: 'studentCode', label: 'Mã SV' }, { key: 'studentName', label: 'Sinh viên' },
      { key: 'billingMonth', label: 'Tháng', format: 'date' }, { key: 'amount', label: 'Phải thu', format: 'money' },
      { key: 'paidAmount', label: 'Đã thu', format: 'money' }, { key: 'balanceDue', label: 'Còn lại', format: 'money' },
      { key: 'paymentStatus', label: 'Trạng thái', format: 'status' }
    ],
    fields: [
      { key: 'stayId', label: 'Lượt ở', type: 'lookup', lookup: 'stays' },
      { key: 'billingMonth', label: 'Ngày đầu tháng thu tiền', type: 'date' },
      { key: 'amount', label: 'Số tiền (₫)', type: 'number', min: 1, step: '1000' },
      { key: 'dueDate', label: 'Hạn thanh toán', type: 'date' }
    ]
  },
  payments: {
    title: 'Thanh toán', singular: 'thanh toán', description: 'Ghi nhận các khoản tiền sinh viên đã nộp.',
    columns: [
      { key: 'invoiceLabel', label: 'Hóa đơn' }, { key: 'studentName', label: 'Sinh viên' },
      { key: 'amount', label: 'Số tiền', format: 'money' }, { key: 'paidAt', label: 'Ngày thanh toán', format: 'datetime' },
      { key: 'paymentMethod', label: 'Phương thức', format: 'status' }, { key: 'referenceCode', label: 'Mã tham chiếu' }
    ],
    fields: [
      { key: 'invoiceId', label: 'Hóa đơn', type: 'lookup', lookup: 'invoices' },
      { key: 'amount', label: 'Số tiền (₫)', type: 'number', min: 1, step: '1000' },
      { key: 'paidAt', label: 'Thời điểm thanh toán', type: 'datetime' },
      { key: 'paymentMethod', label: 'Phương thức', type: 'select', defaultValue: 'BANK_TRANSFER', options: [
        { value: 'BANK_TRANSFER', label: 'Chuyển khoản' }, { value: 'CASH', label: 'Tiền mặt' }
      ] },
      { key: 'referenceCode', label: 'Mã tham chiếu', type: 'text', required: false }
    ]
  },
  maintenance: {
    title: 'Bảo trì', singular: 'yêu cầu bảo trì', description: 'Tiếp nhận và theo dõi sửa chữa phòng ở.',
    columns: [
      { key: 'roomLabel', label: 'Phòng' }, { key: 'title', label: 'Nội dung' },
      { key: 'studentName', label: 'Người báo' }, { key: 'priority', label: 'Ưu tiên', format: 'status' },
      { key: 'requestStatus', label: 'Trạng thái', format: 'status' },
      { key: 'createdAt', label: 'Ngày tạo', format: 'datetime' }
    ],
    fields: [
      { key: 'roomId', label: 'Phòng', type: 'lookup', lookup: 'rooms' },
      { key: 'reportedByStudentId', label: 'Sinh viên báo (nếu có)', type: 'lookup', lookup: 'students', required: false },
      { key: 'title', label: 'Tiêu đề', type: 'text' },
      { key: 'description', label: 'Mô tả', type: 'textarea', required: false },
      { key: 'priority', label: 'Mức ưu tiên', type: 'select', defaultValue: 'MEDIUM', options: [
        { value: 'LOW', label: 'Thấp' }, { value: 'MEDIUM', label: 'Trung bình' }, { value: 'HIGH', label: 'Cao' }
      ] },
      { key: 'requestStatus', label: 'Trạng thái', type: 'select', defaultValue: 'OPEN', options: [
        { value: 'OPEN', label: 'Mới' }, { value: 'IN_PROGRESS', label: 'Đang xử lý' },
        { value: 'RESOLVED', label: 'Đã hoàn tất' }
      ] },
      { key: 'resolvedAt', label: 'Thời điểm hoàn tất', type: 'datetime', required: false }
    ]
  }
};

export const statusLabels: Record<string, string> = {
  MALE: 'Nam', FEMALE: 'Nữ', MIXED: 'Nam và nữ', AVAILABLE: 'Sẵn sàng',
  MAINTENANCE: 'Bảo trì', CLOSED: 'Đóng', ACTIVE: 'Đang hoạt động',
  INACTIVE: 'Ngừng hoạt động', ENDED: 'Đã trả phòng', CANCELLED: 'Đã hủy',
  PAID: 'Đã thanh toán', PARTIAL: 'Thanh toán một phần', UNPAID: 'Chưa thanh toán',
  CASH: 'Tiền mặt', BANK_TRANSFER: 'Chuyển khoản', LOW: 'Thấp', MEDIUM: 'Trung bình',
  HIGH: 'Cao', OPEN: 'Mới', IN_PROGRESS: 'Đang xử lý', RESOLVED: 'Đã hoàn tất'
};
