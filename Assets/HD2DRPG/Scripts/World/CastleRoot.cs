using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Root of the castle diorama. When a baked castle exists in the scene the game uses it
    /// instead of generating one, so everything below can be moved, duplicated or deleted in the editor.
    /// Chests, crystals and enemy symbols are registered here (use "Refresh Lists" from the context
    /// menu after adding or removing some).
    /// </summary>
    [DisallowMultipleComponent]
    public class CastleRoot : MonoBehaviour
    {
        public Transform PartyStart;
        public Transform Throne;
        public Light Moon;
        public List<Chest> Chests = new List<Chest>();
        public List<SaveCrystal> Crystals = new List<SaveCrystal>();
        public List<EnemySymbol> Symbols = new List<EnemySymbol>();
        public EnemySymbol Boss;

        [ContextMenu("Refresh Lists")]
        public void RefreshLists()
        {
            Chests = new List<Chest>(GetComponentsInChildren<Chest>(true));
            Crystals = new List<SaveCrystal>(GetComponentsInChildren<SaveCrystal>(true));
            Symbols = new List<EnemySymbol>();
            Boss = null;
            foreach (var s in GetComponentsInChildren<EnemySymbol>(true))
            {
                if (s.IsBoss) Boss = s;
                else Symbols.Add(s);
            }
        }

        public CastleBuilder.Result ToResult()
        {
            RefreshLists();
            var map = new CastleMap();
            return new CastleBuilder.Result
            {
                Root = transform,
                Map = map,
                Chests = Chests,
                Crystals = Crystals,
                Symbols = Symbols,
                Boss = Boss,
                Start = PartyStart != null ? PartyStart.position : map.TileToWorld(map.Find('@')),
                ThronePos = Throne != null ? Throne.position : map.TileToWorld(map.Find('T')),
            };
        }

        void OnDrawGizmos()
        {
            if (PartyStart == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(PartyStart.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(PartyStart.position, PartyStart.position + Vector3.forward);
        }
    }
}
