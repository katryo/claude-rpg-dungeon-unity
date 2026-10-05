using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Editable data asset for a consumable item.</summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Item", fileName = "NewItem")]
    public class ItemAsset : ScriptableObject
    {
        public ItemDef Def = new ItemDef();
    }
}
