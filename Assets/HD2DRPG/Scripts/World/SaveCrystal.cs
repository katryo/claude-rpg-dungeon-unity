using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    public class SaveCrystal : MonoBehaviour
    {
        public Vector2Int Tile;
        public Vector3 GroundPos => new Vector3(transform.position.x, 0, transform.position.z);
    }
}
