# Báo cáo chuyển màn FE (21/10/2026)

Bỏ hẳn trang "đang phát triển" (`ScreenPlaceholder` đã xoá, không còn route nào dùng). Toàn bộ 43 màn trong `shared/screens.json` chạy trên mock API (`tools/mock-api`, cổng 5080). `npm run verify` xanh (i18n-check 0 lỗi, eslint, vue-tsc, build). Đã mở thử trên trình duyệt: Chi tiết dự án (4 tab), Sửa dự án, Chat AI.

## Màn đã chuyển

| Nhóm | Màn |
|---|---|
| Tài khoản | Đăng nhập, Quên mật khẩu, Đặt lại mật khẩu / nhận lời mời, Hồ sơ, Khảo sát CSS công khai |
| Công việc | Nhật ký công việc (ngày / tuần / báo cáo nhóm / duyệt), Nghỉ phép & OT, Chấm công vào/ra (+ import file máy chấm công), Công số |
| Dự án | Danh sách (+ **Import Excel** mới nối), **Tạo / Sửa dự án**, **Chi tiết dự án**: Tổng quan (KPI, cảnh báo nghỉ việc, bảng phân bổ + biểu đồ HĐ/KH/TT, phân bổ / bàn giao / bỏ gán), Báo cáo tháng, Metrics (Timeline, EE, PCV, CSS, Defect, Leakage), Quản trị dự án (Tài liệu, Thay đổi CR, Issue, Rủi ro), nút Report, Xuất dữ liệu, Xoá / Khôi phục |
| AI | **Chat AI toàn trang** (`/ai`), Insight theo màn ở cột phải |
| Quản trị | Nhân sự (+ chi tiết), Phòng ban & Team, Vai trò & quyền, Quy tắc, AI Insight, Prompt template, Tích hợp, Danh mục, Khách hàng, Cài đặt, Ngôn ngữ, Tiêu chí PCV, Biểu mẫu CSS |

## Chỗ không giữ y nguyên bản gốc (và lý do)

| Chỗ | Khác gì | Lý do |
|---|---|---|
| Biểu đồ phân bổ nhân sự, EE | Vẽ bằng SVG nhẹ, cuộn ngang; không zoom bằng con lăn / kéo pan | Bỏ phụ thuộc TradingView lightweight-charts / Recharts |
| Báo cáo tháng — tiếng Nhật | Chế độ 日本語 chỉ đổi nhãn cột / đầu trang; **không tự dịch nội dung** công việc | Dịch tự động cần gọi LLM theo từng ô, để sau khi có BE thật (D5) |
| Báo cáo tháng — xuất file | Xuất Excel dựng bằng code (1 sheet / nhân sự); bỏ xem trước / xuất PDF | File template 作業報告書 gốc là mẫu riêng của công ty (D12) |
| Báo cáo tháng — giờ / vào-ra | BE tính trực tiếp từ Nhật ký đã duyệt + Chấm công khi đọc; nút "Đồng bộ" chỉ trả ô sửa tay về dữ liệu nguồn | Bỏ RON (D4); không phải lưu bản sao giờ |
| Báo cáo tháng — insight riêng | Dùng Insight theo màn ở cột phải | Bộ quy tắc insight cục bộ của bản gốc gắn với RON |
| Quản trị dự án — tìm kiếm | Ô tìm lọc ngay danh sách của mục đang xem; bỏ popup gợi ý + nhảy tới dòng | Đơn giản hoá, cùng kết quả |
| Report (báo cáo tình trạng) | Bỏ "Nhập từ file"; AI điền qua BE (mock) | D5 — gọi LLM chỉ ở BE |
| Xuất dữ liệu chi tiết dự án | 3 sheet: thông tin chung, phân bổ, số liệu biểu đồ HĐ/KH/TT theo ngày; bỏ sheet nhật ký chi tiết | Số liệu TT đã nằm trong sheet biểu đồ |
| Import dự án / wizard dùng chung | Chỉ đọc sheet đầu (bỏ bước chọn sheet), không tạm dừng giữa chừng | Đơn giản hoá; wizard dùng lại được cho Import khách hàng sau này |
| Chat AI | Bỏ cảnh báo "chưa cấu hình API key" ở trình duyệt; lỗi cấu hình do BE trả về | D5 — khoá AI chỉ ở server |
| Milestone | Giải trình trễ hạn không bắt buộc khi lưu (giống bản gốc nhập riêng) | Giữ luồng cũ |
| Ngưỡng màu EE / PCV / CSS / Defect / Leakage | Không hard-code: 12 quy tắc `QUALITY_*` ở Quản trị → Quy tắc; công thức cố định, comment trong `tools/mock-api/lib/project-routes.mjs`, `project-extra-routes.mjs` | D12 |
| Đăng nhập OAuth / callback tích hợp | Không có route `/auth/callback`, `/api/integrations/:provider/callback` | Bỏ Keycloak (D4); bí mật connector chỉ ở server |

## Dữ liệu mẫu và API giả lập

- `generate.mjs` sinh thêm: tài liệu, rủi ro, CR, link issue, báo cáo tháng (09–10/2026), khảo sát CSS đang chờ, công số 06–09/2026 (để EE, Defect/Leakage có nhiều tháng), 12 quy tắc `QUALITY_*`. Vai trò hợp đồng giờ lưu theo **mã** danh mục (`DEV_BE`…), không theo tên.
- Endpoint mới ghi đủ trong `tools/mock-api/README.md` (tài khoản, tổ chức, quản trị, công việc, công số, dự án chi tiết, import).
- Form CSS giờ riêng từng dự án (khởi tạo từ biểu mẫu mặc định); trang khảo sát công khai đọc form của dự án.

## Còn lại (ngoài phạm vi lần này)

- Import khách hàng (wizard đã sẵn, chưa nối).
- BE .NET thật (chưa làm theo yêu cầu).
- Thư mục `DX` vẫn chưa có git — chưa commit gì.
