namespace InticooInspection.Client.Shared
{
    /// <summary>Inner/Outer packing của 1 thùng carton trong Job Request (B. Packaging).</summary>
    public class CartonPackingRow
    {
        public int     CartonIndex     { get; set; }
        public string  CartonVariant   { get; set; } = "";
        public string  Description     { get; set; } = "";
        public string  PackingQuantity { get; set; } = "";
        public int     InnerPackingQty { get; set; }
        public double  InnerL          { get; set; }
        public double  InnerW          { get; set; }
        public double  InnerH          { get; set; }
        public double  InnerWeight     { get; set; }
        public double  OuterL          { get; set; }
        public double  OuterW          { get; set; }
        public double  OuterH          { get; set; }
        public double  OuterWeight     { get; set; }
    }

    /// <summary>Carton khai báo trong Product profile — dùng để auto-fill.</summary>
    public class ProductCartonOption
    {
        public string  CartonVariant     { get; set; } = "";
        public string  Description       { get; set; } = "";
        public string  PackagingQuantity { get; set; } = "";
        public double? SizeL             { get; set; }
        public double? SizeW             { get; set; }
        public double? SizeH             { get; set; }
        public double? Weight            { get; set; }
    }

    public static class CartonPacking
    {
        /// <summary>Các mức Bursting Test (lbs) cho phép chọn.</summary>
        public static readonly int[] BurstingTestOptions = { 125, 150, 175, 200, 275, 350, 400, 500, 600 };

        /// <summary>Đồng bộ số dòng carton theo Carton Number (giữ dữ liệu đã nhập, tối đa 50).</summary>
        public static void Resize(List<CartonPackingRow> rows, string? cartonNumber, List<ProductCartonOption>? options = null)
        {
            var n = int.TryParse(cartonNumber?.Trim(), out var v) && v > 0 ? Math.Min(v, 50) : 1;
            while (rows.Count > n) rows.RemoveAt(rows.Count - 1);
            while (rows.Count < n)
            {
                var row = new CartonPackingRow { CartonIndex = rows.Count + 1 };
                // Thùng thứ i lấy carton variant thứ i trong product profile (nếu có)
                var opt = options != null && options.Count >= row.CartonIndex ? options[row.CartonIndex - 1] : null;
                if (opt != null) ApplyOption(row, opt);
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++) rows[i].CartonIndex = i + 1;
        }

        public static void ApplyOption(CartonPackingRow row, ProductCartonOption opt)
        {
            row.CartonVariant   = opt.CartonVariant;
            row.Description     = opt.Description;
            row.PackingQuantity = opt.PackagingQuantity;
            row.OuterL          = opt.SizeL  ?? 0;
            row.OuterW          = opt.SizeW  ?? 0;
            row.OuterH          = opt.SizeH  ?? 0;
            row.OuterWeight     = opt.Weight ?? 0;
        }
    }
}
