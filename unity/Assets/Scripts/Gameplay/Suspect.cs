using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // An armed suspect on foot. Holds its post and fires at the player when in range and sight.
    // When its health runs out it is incapacitated: it lies down and goes grey (no injury effects).
    // Low on health it surrenders (hands up, stops fighting, can no longer be hurt). A surrendered or
    // incapacitated suspect can be handcuffed, then follows the player and rides in the car to the station.
    // shortcut: no movement, cover or fleeing on foot yet.
    public class Suspect : MonoBehaviour
    {
        public enum State { Armed, Surrendered, Down, Cuffed, InCar, Delivered }
        public const float SurrenderAt = 0.35f; // share of health left
        public const float EscortGap = 1.5f, EscortSpeed = 5;
        public State Now { get; private set; }
        public bool CanBeCuffed => Now == State.Surrendered || Now == State.Down;
        public Transform Escort { get; set; } // who a cuffed suspect follows
        public Color Colour { get; set; } = Shapes.Hex(0xFF8A00); // orange suspect; the prisoner mission's guard is blue
        public void SetPost(Vector3 feet) => post = feet;
        GameObject hands, cuffs;
        Collider hitbox;
        public Transform Target { get; set; } // the player on foot, or null
        public Health Health { get; private set; }
        public WeaponController Weapon { get; private set; }
        public MeleeController Melee { get; private set; }
        public MeleeController TargetMelee { get; set; } // to see the player's strikes coming
        public const float CloseRange = 2.2f, RangedFrom = 4, BlockChance = 0.35f, StrikeEvery = 1.4f;
        bool playerWasBusy, guarding;
        float strikeWait;
        Renderer look;
        Vector3 post;

        public static Suspect Create(Transform parent, Vector3 feet)
        {
            var go = new GameObject("Suspect");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = feet;
            var body = Shapes.Box(go.transform, new Vector3(0, Walker.Height / 2, 0),
                new Vector3(Walker.Radius * 2, Walker.Height / 2, Walker.Radius * 2), Shapes.Hex(0xFF8A00), PrimitiveType.Capsule);
            var hitbox = go.AddComponent<CapsuleCollider>();
            go.AddComponent<Suspect>().hitbox = hitbox;
            hitbox.height = Walker.Height;
            hitbox.radius = Walker.Radius;
            hitbox.center = new Vector3(0, Walker.Height / 2, 0);
            var s = go.GetComponent<Suspect>();
            s.hands = Shapes.Box(go.transform, new Vector3(0, 2.05f, 0), new Vector3(0.9f, 0.15f, 0.15f), Shapes.Hex(0xFF8A00));
            s.cuffs = Shapes.Box(go.transform, new Vector3(0, 1f, -0.4f), new Vector3(0.35f, 0.12f, 0.12f), Color.white, glow: 1);
            s.look = body.GetComponent<Renderer>();
            s.post = feet;
            s.Health = go.AddComponent<Health>();
            s.Weapon = go.AddComponent<WeaponController>();
            s.Weapon.Init(WeaponData.SuspectPistol());
            s.Melee = go.AddComponent<MeleeController>();
            s.Health.Downed += s.Incapacitate;
            s.Health.Hurt += s.MaybeSurrender;
            s.ResetSuspect();
            return s;
        }

        void MaybeSurrender()
        {
            if (Now != State.Armed || Health.Down || Health.Current > Health.Max * SurrenderAt) return;
            Now = State.Surrendered;
            Protect();
            hands.SetActive(true);
        }

        // Surrendered and arrested suspects can no longer be hurt; the melee guard must not undo this.
        void Protect()
        {
            Melee.ResetMelee();
            Health.DamageScale = 0;
        }

        // Handcuffed: stands up, hands behind the back, follows `escort`.
        public bool Cuff(Transform escort)
        {
            if (!CanBeCuffed) return false;
            Now = State.Cuffed;
            Escort = escort;
            Protect();
            hands.SetActive(false);
            cuffs.SetActive(true);
            hitbox.enabled = false; // walks with the player, never blocks them
            transform.localRotation = Quaternion.Euler(0, transform.localEulerAngles.y, 0);
            transform.position = new Vector3(transform.position.x, transform.parent.position.y, transform.position.z);
            Shapes.Paint(look, Shapes.Hex(0xB36100));
            return true;
        }

        public void PutInCar()
        {
            if (Now != State.Cuffed) return;
            Now = State.InCar;
            gameObject.SetActive(false); // on the back seat
        }

        public void Deliver()
        {
            if (Now == State.InCar) Now = State.Delivered;
        }

        void Incapacitate()
        {
            Now = State.Down;
            hands.SetActive(false);
            transform.localRotation = Quaternion.Euler(0, transform.localEulerAngles.y, 90); // lies on its side
            transform.localPosition = post + Vector3.up * Walker.Radius;
            Shapes.Paint(look, Color.grey);
        }

        public void ResetSuspect()
        {
            gameObject.SetActive(true);
            Now = State.Armed;
            Escort = null;
            hands.SetActive(false);
            cuffs.SetActive(false);
            hitbox.enabled = true;
            Health.Restore();
            Weapon.Restock();
            Melee.ResetMelee(); // also puts the damage scale back to normal
            strikeWait = 0;
            transform.localPosition = post;
            transform.localRotation = Quaternion.Euler(0, 180, 0);
            Shapes.Paint(look, Colour);
        }

        void Update()
        {
            if (Now == State.Cuffed) { Follow(Time.deltaTime); return; }
            if (Now != State.Armed) return;
            if (Target == null) { Melee.SetBlock(false); return; }
            var to = Target.position - transform.position;
            if (to.magnitude < CloseRange) { Brawl(Time.deltaTime); return; }
            Melee.SetBlock(false);
            if (to.magnitude < RangedFrom || to.magnitude > Weapon.Data.range) return;
            var eye = transform.position + Vector3.up * WeaponController.ChestHeight;
            var chest = Target.position + Vector3.up * 1.1f;
            if (Physics.Linecast(eye, chest, out var hit) && hit.transform != Target) return;
            Weapon.Fire(chest - eye); // the weapon's own interval and hit chance keep it fair
        }

        void Follow(float dt)
        {
            if (Escort == null) return;
            var goal = Escort.position - Escort.forward * EscortGap;
            goal.y = transform.position.y;
            var to = goal - transform.position;
            if (to.magnitude > 0.1f) transform.position = Vector3.MoveTowards(transform.position, goal, EscortSpeed * dt);
            var face = Vector3.ProjectOnPlane(Escort.position - transform.position, Vector3.up);
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
        }

        // Close up: guard against some of the player's strikes (one roll per strike), otherwise strike back.
        void Brawl(float dt)
        {
            bool playerBusy = TargetMelee != null && TargetMelee.Busy && !TargetMelee.Dodging;
            if (playerBusy && !playerWasBusy) guarding = Random.value < BlockChance;
            playerWasBusy = playerBusy;
            Melee.SetBlock(playerBusy && guarding);
            if (Melee.Blocking || (strikeWait -= dt) > 0) return;
            if (Melee.LightAttack()) strikeWait = StrikeEvery;
        }
    }
}
