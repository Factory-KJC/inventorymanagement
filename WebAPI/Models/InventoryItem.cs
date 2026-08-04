using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Serialization;

namespace InventoryAPI.Models
{
    /// <summary>
    /// 在庫アイテムのモデル
    /// </summary>
    [Table("inventory_items", Schema = "inventorymanagement")]
    public class InventoryItems
    {
        public int Id { get; set; }

        /// <summary>
        /// アイテム名
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// カテゴリ
        /// </summary>
        public string? Category { get; set; } = "その他";

        /// <summary>
        /// 数量
        /// </summary>
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        /// <summary>
        /// 価格
        /// </summary>
        [Range(0, double.MaxValue)]
        public decimal? Price { get; set; }

        /// <summary>
        ///  バーコード
        /// </summary>
        public string? Barcode { get; set; }
        /// <summary>
        /// 購入元
        /// </summary>
        public string? Supplier { get; set; }
        /// <summary>
        /// 保管場所
        /// </summary>
        public string? Storage_Location { get; set; }
        /// <summary>
        /// 入庫日
        /// </summary>
        public DateTime Entry_Date { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// 消費・賞味・使用期限
        /// </summary>
        public DateTime? Expiration_Date { get; set; }
        /// <summary>
        /// 備考
        /// </summary>
        public string? Notes { get; set; }
    }
}
