using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Editable data asset for a piece of equipment (slot, stat bonuses, who can equip it, resistance).</summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Equip", fileName = "NewEquip")]
    public class EquipAsset : ScriptableObject
    {
        public EquipDef Def = new EquipDef();
    }
}
