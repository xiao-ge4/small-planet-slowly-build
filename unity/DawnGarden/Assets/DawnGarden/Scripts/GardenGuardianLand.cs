using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DawnGarden
{
    // Pure save-state rules. Combat decides a result; this class owns its one-time reward.
    public static class GardenGuardianLand
    {
        public const string Guarded = "guarded", Challenging = "challenging", Occupied = "occupied";
        public enum Resolution { Rejected, Defeat, ReplayVictory, Occupied, AlreadyResolved }

        public static bool CanCreate(GardenProgress state) => state != null && state.firstExpansionUnlocked
            && (state.guardianLandVersion == 0 || (state.guardianLandVersion == 1 && state.guardianLandState == Occupied));
        public static bool IsChallenging(GardenProgress state) => state != null && state.guardianLandVersion == 1
            && state.guardianLandState == Challenging;

        public static void Restore(GardenProgress state)
        {
            Upgrade(state);
            Validate(state);
            if (!IsChallenging(state)) return;
            // A process interruption is a retreat, never an implicit victory or debit.
            state.guardianLastEncounterId = state.guardianEncounterId;
            state.guardianLastOutcome = "interrupted";
            state.guardianLandState = state.firstExpansionUnlocked ? Occupied : Guarded;
            state.guardianEncounterId = ""; state.guardianReplay = false;
        }

        public static void Upgrade(GardenProgress state)
        {
            if (state == null) throw new InvalidDataException("守卫土地存档缺失。");
            if (state.guardianLandVersion != 0) return;
            state.guardianLandVersion = 1;
            state.guardianLandState = state.firstExpansionUnlocked ? Occupied : Guarded;
            state.guardianLegacyOwnership = state.firstExpansionUnlocked;
            state.guardianOccupationId = state.firstExpansionUnlocked ? LegacyReceipt(state.homeId) : "";
            state.guardianEncounterId = state.guardianLastEncounterId = state.guardianLastOutcome = "";
            state.guardianReplay = false;
        }

        public static void Validate(GardenProgress state)
        {
            if (state.guardianLandVersion != 1 || state.expansionPermits < 0)
                throw new InvalidDataException("守卫土地存档版本或许可无效。");
            bool occupied = state.firstExpansionUnlocked, active = state.guardianLandState == Challenging;
            if (state.guardianLandState != Guarded && state.guardianLandState != Challenging && state.guardianLandState != Occupied
                || (state.guardianLandState == Occupied && !occupied) || (state.guardianLandState == Guarded && occupied)
                || occupied != Id(state.guardianOccupationId) || (!occupied && !string.IsNullOrEmpty(state.guardianOccupationId))
                || (state.guardianLegacyOwnership && !occupied)
                || active != Id(state.guardianEncounterId) || (!active && !string.IsNullOrEmpty(state.guardianEncounterId))
                || state.guardianReplay != (active && occupied)
                || (active && !occupied && state.expansionPermits < 1))
                throw new InvalidDataException("守卫土地状态不一致，原存档已保留。");
            var outcome = state.guardianLastOutcome ?? "";
            if (outcome != "" && outcome != "victory" && outcome != "defeat" && outcome != "interrupted"
                || (outcome != "") != Id(state.guardianLastEncounterId)
                || (outcome == "" && !string.IsNullOrEmpty(state.guardianLastEncounterId))
                || (occupied && !state.guardianLegacyOwnership && outcome == "")
                || (occupied && !state.guardianLegacyOwnership && outcome != "victory" && !active && state.guardianOccupationId == state.guardianLastEncounterId))
                throw new InvalidDataException("守卫挑战结果无效，原存档已保留。");
        }

        public static bool Begin(GardenProgress state, bool replay, out string encounterId)
        {
            encounterId = null; Upgrade(state); Validate(state);
            if (IsChallenging(state) || replay != state.firstExpansionUnlocked || (replay && !CanCreate(state)) || (!replay && state.expansionPermits < 1)) return false;
            encounterId = Guid.NewGuid().ToString("N");
            state.guardianLandState = Challenging; state.guardianEncounterId = encounterId; state.guardianReplay = replay;
            return true;
        }

        public static Resolution Resolve(GardenProgress state, string encounterId, bool victory)
        {
            Upgrade(state); Validate(state);
            if (!Id(encounterId)) return Resolution.Rejected;
            if (!IsChallenging(state))
                return state.guardianLastEncounterId == encounterId && state.guardianLastOutcome == (victory ? "victory" : "defeat")
                    ? Resolution.AlreadyResolved : Resolution.Rejected;
            if (state.guardianEncounterId != encounterId) return Resolution.Rejected;
            bool replay = state.guardianReplay;
            if (victory && !replay)
            {
                if (state.expansionPermits < 1 || state.firstExpansionUnlocked) return Resolution.Rejected;
                state.expansionPermits--; state.firstExpansionUnlocked = true; state.expansionOfferPending = false;
                state.guardianOccupationId = encounterId;
            }
            state.guardianLastEncounterId = encounterId; state.guardianLastOutcome = victory ? "victory" : "defeat";
            state.guardianLandState = state.firstExpansionUnlocked ? Occupied : Guarded;
            state.guardianEncounterId = ""; state.guardianReplay = false;
            return !victory ? Resolution.Defeat : replay ? Resolution.ReplayVictory : Resolution.Occupied;
        }

        static bool Id(string value) => value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out _);
        static string LegacyReceipt(string homeId)
        {
            // Stable until the migrated save is written, including a crash on first load.
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("guardian-legacy:" + homeId)), 0, 16).Replace("-", "").ToLowerInvariant();
        }

        // Roll back only progression fields when an atomic file replacement fails.
        public sealed class Checkpoint
        {
            readonly int version, permits;
            readonly bool unlocked, pending, legacy, replay;
            readonly string state, encounter, occupation, lastEncounter, outcome;
            public Checkpoint(GardenProgress p)
            {
                version=p.guardianLandVersion; permits=p.expansionPermits; unlocked=p.firstExpansionUnlocked; pending=p.expansionOfferPending;
                legacy=p.guardianLegacyOwnership; replay=p.guardianReplay; state=p.guardianLandState; encounter=p.guardianEncounterId;
                occupation=p.guardianOccupationId; lastEncounter=p.guardianLastEncounterId; outcome=p.guardianLastOutcome;
            }
            public void Apply(GardenProgress p)
            {
                p.guardianLandVersion=version; p.expansionPermits=permits; p.firstExpansionUnlocked=unlocked; p.expansionOfferPending=pending;
                p.guardianLegacyOwnership=legacy; p.guardianReplay=replay; p.guardianLandState=state; p.guardianEncounterId=encounter;
                p.guardianOccupationId=occupation; p.guardianLastEncounterId=lastEncounter; p.guardianLastOutcome=outcome;
            }
        }
    }
}
