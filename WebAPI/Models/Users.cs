using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    /// <summary>
    /// ユーザーモデル
    /// </summary>
    [Table("users", Schema ="inventorymanagement")]
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password_Hash { get; set; } = string.Empty;
    }
}
