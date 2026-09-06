namespace InventoryAPI.Domain.Inventory;


/// <summary>
/// 在庫移動タイプを表す列挙型
/// </summary>
public enum StockMovementType
{
    Receive = 1,
    Consume = 2,
    Discard = 3,
    Adjust = 4,
    Transfer = 5,
    Reverse = 6
}
