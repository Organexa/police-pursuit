using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // "Midnight Prison Break", played as either side, on top of the open-world systems.
    // Police: chase the escapee's car until it bails out, stop the suspect on foot (shots, then fists),
    //         arrest and deliver to the station.
    // Prisoner: shake off the police and reach the garage; the car dies there, so run past the guard to the safehouse.
    // shortcut: text intro and messages instead of cutscenes and dialogue; no civilian traffic yet.
    public class Mission
    {
        public enum Role { Police, Prisoner }
        public enum Outcome { None, Captured, Escaped, Busted, Incapacitated, TimeUp }

        public const string Id = "midnight_prison_break";
        public const float TimeLimit = 300, Pressure = 4, PressureRange = 25, BailAfter = 90, Reach = 8;
        public const int Reward = 500;
        public static readonly Vector3 FleeSpawn = new Vector3(0, 0.6f, 30); // just ahead of the player
        public static readonly Vector3 Garage = new Vector3(2 * OpenWorld.Pitch, 0, 0); // east edge
        public static readonly Vector3 GuardPost = new Vector3(2 * OpenWorld.Pitch, 0, OpenWorld.Pitch);
        public static readonly Vector3 Safehouse = new Vector3(2 * OpenWorld.Pitch, 0, 2 * OpenWorld.Pitch - 6);

        public const string Intro = "MIDNIGHT. An alarm tears through Blackwater Prison: one inmate is over the wall " +
            "and away in a stolen car.";

        public Role Side { get; }
        public int Step { get; private set; } // 0 = driving part, 1 = on-foot part
        public float Elapsed { get; private set; }
        public Outcome Result { get; private set; }
        public bool Won => Result == Outcome.Captured || Result == Outcome.Escaped;
        public string Message { get; private set; } // short story beat, shown for a few seconds
        public float MessageUntil { get; private set; }
        public float PressureTime { get; private set; }

        public Mission(Role side)
        {
            Side = side;
            Say(side == Role.Police ? "Dispatch: stolen car heading north. Stay on it!" : "You're out. Lose the police and reach the garage!");
        }

        public string Objective(OpenWorld w) =>
            Side == Role.Police
                ? (Step == 0 ? "Chase the escaped prisoner's car" : w.ArrestObjective)
                : (Step == 0 ? "Reach the garage (green)" : "Run to the safehouse (green) before dawn");

        void Say(string text)
        {
            Message = text;
            MessageUntil = Elapsed + 4;
        }

        public void End(Outcome outcome)
        {
            if (Result == Outcome.None) Result = outcome;
        }

        public static string Describe(Outcome o) =>
            o == Outcome.Captured ? "Suspect captured and booked."
            : o == Outcome.Escaped ? "You made it to the safehouse. Free... for now."
            : o == Outcome.Busted ? "The police caught you."
            : o == Outcome.Incapacitated ? "You were taken down."
            : o == Outcome.TimeUp ? "Dawn broke before you finished."
            : "";

        public void Tick(OpenWorld w, float dt)
        {
            if (Result != Outcome.None) return;
            Elapsed += dt;
            if (Elapsed > TimeLimit) { End(Outcome.TimeUp); return; }
            if (Side == Role.Police) TickPolice(w, dt);
            else TickPrisoner(w);
        }

        void TickPolice(OpenWorld w, float dt)
        {
            if (Step != 0) return; // the arrest loop reports the capture through OpenWorld
            bool close = Vector3.Distance(w.Player.position, w.Fleer.transform.position) < PressureRange;
            PressureTime = close ? PressureTime + dt : Mathf.Max(0, PressureTime - dt / 2);
            if (PressureTime < Pressure && Elapsed < BailAfter) return;
            // Scripted turn: the escapee panics, abandons the car and stands to fight.
            Step = 1;
            var car = w.Fleer.transform;
            w.Fleer.Park();
            w.PlaceSuspect(car.position - car.right * 2.2f);
            Say("The prisoner bails out! Get out and go after them on foot.");
        }

        void TickPrisoner(OpenWorld w)
        {
            if (Step == 0 && !w.OnFoot && Flat(w.Car.transform.position - Garage) < Reach + 2)
            {
                // Scripted turn: the engine gives out, so the run continues on foot.
                Step = 1;
                w.BreakDown();
                Say("The engine dies. Run for the safehouse!");
            }
            else if (Step == 1 && w.OnFoot && Flat(w.Walker.transform.position - Safehouse) < Reach)
                End(Outcome.Escaped);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
