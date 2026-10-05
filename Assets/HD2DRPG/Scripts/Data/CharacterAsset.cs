using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Editable data asset for a party member (base stats, growth, skills by level, starting gear).</summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Character", fileName = "NewCharacter")]
    public class CharacterAsset : ScriptableObject
    {
        public CharacterDef Def = new CharacterDef();
    }
}
