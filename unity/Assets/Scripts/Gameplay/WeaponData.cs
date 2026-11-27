using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A fictional weapon's game numbers. Not modelled on any real firearm.
    [CreateAssetMenu(menuName = "Police Pursuit/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Pulse Pistol";
        public float damage = 25;
        public float fireInterval = 0.3f; // seconds between shots
        public int magazine = 12;
        public float reloadTime = 1.2f;
        public float range = 40;
        public float hitChance = 1;   // 0..1, the game's own accuracy roll
        public float assistAngle = 15; // degrees: the nearest target inside this cone is aimed at

        // shortcut: weapons are made in code; move them to .asset files when there is more than one per side.
        public static WeaponData Make(string name, float damage, float interval, int magazine, float reload, float range,
            float hitChance, float assist)
        {
            var w = CreateInstance<WeaponData>();
            w.displayName = name;
            w.damage = damage;
            w.fireInterval = interval;
            w.magazine = magazine;
            w.reloadTime = reload;
            w.range = range;
            w.hitChance = hitChance;
            w.assistAngle = assist;
            return w;
        }

        public static WeaponData PlayerPistol() => Make("Pulse Pistol", 25, 0.3f, 12, 1.2f, 40, 1, 15);
        public static WeaponData SuspectPistol() => Make("Scrap Blaster", 8, 0.9f, 6, 2f, 25, 0.5f, 0);
    }
}
