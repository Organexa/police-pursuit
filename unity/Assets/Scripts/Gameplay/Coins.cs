using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PolicePursuit.Gameplay
{
    // A few coins ahead of the player, reused when taken or passed.
    public class Coins : MonoBehaviour
    {
        const int Count = 6;
        const float Spacing = 35f;

        public event Action Collected;

        PlayerCar player;
        readonly Transform[] coins = new Transform[Count];

        public void Init(PlayerCar target)
        {
            player = target;
            for (int i = 0; i < Count; i++)
                coins[i] = Shapes.Box(transform, Vector3.zero, new Vector3(1, 0.1f, 1),
                    Shapes.Hex(0xFFD23F), PrimitiveType.Cylinder, 0.6f).transform;
            ResetCoins();
        }

        public void ResetCoins()
        {
            for (int i = 0; i < Count; i++) Place(coins[i], player.transform.position.z + Spacing * (i + 1));
        }

        public void Tick(float dt)
        {
            var p = player.transform.position;
            foreach (var coin in coins)
            {
                coin.Rotate(0, 0, 180 * dt, Space.World);
                var pos = coin.position;
                bool taken = Mathf.Abs(pos.x - p.x) < 1.5f && Mathf.Abs(pos.z - p.z) < 2f;
                if (taken) Collected?.Invoke();
                if (taken || pos.z < p.z - 10) Place(coin, pos.z + Spacing * Count);
            }
        }

        static void Place(Transform coin, float z)
        {
            coin.position = new Vector3(Random.Range(-1, 2) * 4f, 1, z);
            coin.rotation = Quaternion.Euler(90, 0, 0);
        }
    }
}
