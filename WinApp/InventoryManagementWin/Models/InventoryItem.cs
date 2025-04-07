using System;

namespace InventoryManagementWin.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Category { get; set; } = "その他";

        public int Quantity { get; set; }

        public decimal? Price { get; set; }

        public string Barcode { get; set; }

        public string Supplier { get; set; }
        public string Storage_Location { get; set; }
        public DateTime Entry_Date { get; set; } = DateTime.UtcNow;
        public DateTime? Expiration_Date { get; set; }
        public string Notes { get; set; }
    }
}
