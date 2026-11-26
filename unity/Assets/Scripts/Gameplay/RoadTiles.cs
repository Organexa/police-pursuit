using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Endless road: a ring of tiles, the one behind the player jumps to the front.
    public class RoadTiles : MonoBehaviour
    {
        const int Count = 8;
        const float Length = 40f;

        readonly Transform[] tiles = new Transform[Count];

        void Awake()
        {
            for (int i = 0; i < Count; i++) tiles[i] = BuildTile(i);
            ResetRoad();
        }

        public void ResetRoad()
        {
            for (int i = 0; i < Count; i++) tiles[i].localPosition = new Vector3(0, 0, (i - 1) * Length);
        }

        public void Recycle(float playerZ)
        {
            foreach (var t in tiles)
                if (t.localPosition.z + Length < playerZ)
                    t.localPosition += new Vector3(0, 0, Count * Length);
        }

        Transform BuildTile(int index)
        {
            var tile = new GameObject("Tile" + index).transform;
            tile.SetParent(transform, false);
            float width = PlayerCar.RoadHalfWidth * 2 + 2;

            Shapes.Box(tile, new Vector3(0, -0.1f, 0), new Vector3(width, 0.2f, Length), Shapes.Navy);
            for (float z = -15; z <= 15; z += 10)
            {
                Shapes.Box(tile, new Vector3(-2, 0.01f, z), new Vector3(0.15f, 0.02f, 4), Color.white);
                Shapes.Box(tile, new Vector3(2, 0.01f, z), new Vector3(0.15f, 0.02f, 4), Color.white);
            }
            foreach (int side in new[] { -1, 1 })
            {
                var neon = side < 0 ? Shapes.Cyan : Shapes.Magenta;
                Shapes.Box(tile, new Vector3(side * width / 2, 0.2f, 0), new Vector3(0.3f, 0.4f, Length), neon);
                float height = 6 + (index * 5 + side + 1) % 11;
                Shapes.Box(tile, new Vector3(side * 12, height / 2, 0), new Vector3(8, height, 20), Shapes.Dark);
                Shapes.Box(tile, new Vector3(side * 12, height, 0), new Vector3(8.2f, 0.4f, 20.2f), neon);
            }
            return tile;
        }
    }
}
