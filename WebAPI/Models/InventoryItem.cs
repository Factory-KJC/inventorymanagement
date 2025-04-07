using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Serialization;

namespace InventoryAPI.Models
{
    [Table("inventory_items", Schema = "inventorymanagement")]
    public class InventoryItems
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? Category { get; set; } = "その他";

        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Price { get; set; }

        public string? Barcode { get; set; }

        public string? Supplier { get; set; }
        public string? Storage_Location { get; set; }
        public DateTime Entry_Date { get; set; } = DateTime.UtcNow;
        public DateTime? Expiration_Date { get; set; }
        public string? Notes { get; set; }
    }
}