using System;
using System.Collections.Generic;

namespace PolicePursuit.Gameplay
{
    // Every state the open-world player can be in (master prompt section 6), and which changes are allowed.
    // OpenWorld is the only writer. Getting in and out of the car passes through EnteringVehicle / ExitingVehicle;
    // both are instant until there are animations, but Changed fires for them so animations can hook in later.
    public enum PlayerState
    {
        Driving, ExitingVehicle, OnFoot, EnteringVehicle,
        MeleeCombat, RangedCombat, Arresting,
        Captured, Incapacitated, MissionComplete,
    }

    public class PlayerStateMachine
    {
        static readonly Dictionary<PlayerState, PlayerState[]> Allowed = new Dictionary<PlayerState, PlayerState[]>
        {
            [PlayerState.Driving] = new[] { PlayerState.ExitingVehicle, PlayerState.Captured, PlayerState.Incapacitated, PlayerState.MissionComplete },
            [PlayerState.ExitingVehicle] = new[] { PlayerState.OnFoot },
            [PlayerState.OnFoot] = new[]
            {
                PlayerState.EnteringVehicle, PlayerState.MeleeCombat, PlayerState.RangedCombat, PlayerState.Arresting,
                PlayerState.Captured, PlayerState.Incapacitated, PlayerState.MissionComplete,
            },
            [PlayerState.EnteringVehicle] = new[] { PlayerState.Driving },
            // No shooting mid-strike and no strike mid-arrest: combat and the arrest only change back through OnFoot.
            [PlayerState.MeleeCombat] = new[] { PlayerState.OnFoot, PlayerState.Captured, PlayerState.Incapacitated, PlayerState.MissionComplete },
            [PlayerState.RangedCombat] = new[] { PlayerState.OnFoot, PlayerState.Captured, PlayerState.Incapacitated, PlayerState.MissionComplete },
            [PlayerState.Arresting] = new[] { PlayerState.OnFoot, PlayerState.Captured, PlayerState.Incapacitated, PlayerState.MissionComplete },
            // Ends: nothing moves on until Reset (a fresh start or a retry).
            [PlayerState.Captured] = new PlayerState[0],
            [PlayerState.Incapacitated] = new PlayerState[0],
            [PlayerState.MissionComplete] = new PlayerState[0],
        };

        public PlayerState State { get; private set; } = PlayerState.Driving;
        public event Action<PlayerState, PlayerState> Changed; // from, to

        public bool InCar => State == PlayerState.Driving;
        public bool OnFoot => State == PlayerState.OnFoot || State == PlayerState.MeleeCombat
            || State == PlayerState.RangedCombat || State == PlayerState.Arresting;
        public bool Ended => State == PlayerState.Captured || State == PlayerState.Incapacitated || State == PlayerState.MissionComplete;

        public bool CanGo(PlayerState to) => to == State || Array.IndexOf(Allowed[State], to) >= 0;

        // False, and nothing changes, when the move is not allowed from here.
        public bool Go(PlayerState to)
        {
            if (to == State) return true;
            if (!CanGo(to)) return false;
            var from = State;
            State = to;
            Changed?.Invoke(from, to);
            return true;
        }

        // A fresh start or a retry: back to `to` whatever the state was.
        public void Reset(PlayerState to = PlayerState.Driving)
        {
            if (to == State) return;
            var from = State;
            State = to;
            Changed?.Invoke(from, to);
        }

        // The on-foot state the moment calls for: combat and the arrest show while they last, else plain OnFoot.
        // A change between two of them passes through OnFoot, so the rules above still hold.
        public void SettleOnFoot(bool striking, bool shooting, bool arresting)
        {
            if (!OnFoot) return;
            var want = striking ? PlayerState.MeleeCombat : arresting ? PlayerState.Arresting
                : shooting ? PlayerState.RangedCombat : PlayerState.OnFoot;
            if (!Go(want)) { Go(PlayerState.OnFoot); Go(want); }
        }
    }
}
