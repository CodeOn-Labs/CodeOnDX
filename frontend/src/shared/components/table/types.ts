/** Cột của CommonTable. Nội dung ô render qua slot `cell-<id>`, tiêu đề phức tạp qua slot `header-<id>`. */
export interface TableColumn {
  id: string;
  /** Tiêu đề dạng chữ (đã dịch). Bỏ trống nếu dùng slot `header-<id>`. */
  header?: string;
  headerClass?: string;
  cellClass?: string;
}
