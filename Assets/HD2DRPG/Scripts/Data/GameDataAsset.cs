using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Registry of all game content. Lives at Resources/HD2D/GameData.asset (created by
    /// "HD-2D RPG ▸ Bake Data Assets"). When present, the game loads everything from here instead
    /// of the built-in defaults in Database.cs. Add a new asset to the matching list to use it.
    /// </summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Game Data Registry", fileName = "GameData")]
    public class GameDataAsset : ScriptableObject
    {
        [Header("New game")]
        public int StartLevel = 10;
        public int StartGold = 480;
        public List<ItemStack> StartItems = new List<ItemStack>();
        public List<ItemStack> StartEquipment = new List<ItemStack>();

        [Header("Content (party order = order of Characters)")]
        public List<CharacterAsset> Characters = new List<CharacterAsset>();
        public List<SkillAsset> Skills = new List<SkillAsset>();
        public List<EquipAsset> Equipment = new List<EquipAsset>();
        public List<ItemAsset> Items = new List<ItemAsset>();
        public List<EnemyAsset> Enemies = new List<EnemyAsset>();

        [Header("Encounters (enemy ids per battle; referenced by enemy symbols in the scene)")]
        public List<EncounterDef> Encounters = new List<EncounterDef>();
    }
}
