using System;
using System.Collections.Generic;

namespace DawnGarden
{
    /// <summary>Pure logic checks; suitable for a standalone C# runner or Unity's integration verifier.</summary>
    public static class GuardianBattleVerification
    {
        public static string[] RunAll()
        {
            var checks = new List<string>();
            CheckInputLock(checks);
            CheckImpactTiming(checks);
            CheckInterrupt(checks);
            CheckGuardAndWeakPoint(checks);
            CheckOutcomes(checks);
            ExploreReachableStates(checks);
            return checks.ToArray();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Guardian battle verification failed: " + message);
        }

        static void Resolve(GuardianBattleModel model, GuardianBattleAction action)
        {
            int originalHealth = model.PlayerHealth, originalEnemyHealth = model.GuardianHealth;
            Require(model.TryAct(action, model.Turn), "scripted action is accepted: " + action);
            Require(model.PlayerHealth == originalHealth && model.GuardianHealth == originalEnemyHealth, "input never applies pending damage");
            while (model.Phase == GuardianBattlePhase.PlayerAction || model.Phase == GuardianBattlePhase.GuardianAction)
            {
                Require(!model.Advance(), "a phase cannot end before its impact");
                Require(model.ResolveImpact(), "animation contact resolves");
                int health = model.PlayerHealth, enemyHealth = model.GuardianHealth, energy = model.Energy;
                Require(!model.ResolveImpact(), "duplicate contact is ignored");
                Require(model.PlayerHealth == health && model.GuardianHealth == enemyHealth && model.Energy == energy, "duplicate contact has no effects");
                Require(model.Advance(), "animation boundary resolves");
            }
        }

        static void CheckInputLock(List<string> checks)
        {
            var model = new GuardianBattleModel();
            Require(!model.Advance(), "idle cannot skip a player decision");
            Require(!model.TryAct(GuardianBattleAction.Resonance, 1), "insufficient energy is rejected");
            Require(!model.TryAct((GuardianBattleAction)99, 1), "unknown commands are rejected");
            Require(model.Turn == 1 && model.Energy == 0 && model.GuardianHealth == 72, "rejected actions do not consume a turn or resources");
            Require(model.TryAct(GuardianBattleAction.Attack, 1), "first input accepted");
            Require(!model.TryAct(GuardianBattleAction.Attack, 1), "double click cannot attack twice");
            model.ResolveImpact();
            model.Advance();
            Require(!model.TryAct(GuardianBattleAction.Guard, 1), "guardian animation blocks player input");
            model.ResolveImpact();
            model.Advance();
            Require(!model.TryAct(GuardianBattleAction.Attack, 1), "stale input is rejected after the next turn begins");
            Require(model.PlayerHealth == 28 && model.GuardianHealth == 64 && model.Energy == 1 && model.Turn == 2, "exactly one exchange applied");
            checks.Add("input: insufficient energy, invalid commands, double click, stale turns and animation locks pass");
        }

        static void CheckImpactTiming(List<string> checks)
        {
            var model = new GuardianBattleModel();
            Require(!model.ResolveImpact() && !model.ImpactResolved, "idle has no impact");
            Require(model.TryAct(GuardianBattleAction.Attack, 1), "attack starts");
            Require(model.GuardianHealth == 72 && model.PlayerHealth == 36 && model.Energy == 0 && model.PlayerDamageDealt == 8, "selection only plans attack damage");
            Require(!model.Advance() && model.Phase == GuardianBattlePhase.PlayerAction, "early player boundary cannot skip contact");
            Require(model.ResolveImpact() && model.ImpactResolved && model.GuardianHealth == 64 && model.Energy == 1, "player contact applies damage and energy");
            Require(!model.ResolveImpact() && model.GuardianHealth == 64 && model.Energy == 1, "player contact applies at most once");
            Require(model.Advance() && !model.ImpactResolved, "guardian phase begins with its own unresolved contact");
            Require(model.PlayerHealth == 36 && model.GuardianDamageDealt == 8, "guardian phase entry only plans damage");
            Require(!model.Advance() && model.PlayerHealth == 36, "early guardian boundary cannot skip contact");
            Require(model.ResolveImpact() && model.PlayerHealth == 28, "guardian contact applies damage");
            Require(!model.ResolveImpact() && model.PlayerHealth == 28, "guardian contact applies at most once");
            Require(model.Advance() && !model.ResolveImpact() && !model.ImpactResolved, "next decision rejects old contact");

            Resolve(model, GuardianBattleAction.Attack);
            Require(model.TryAct(GuardianBattleAction.Resonance, model.Turn), "resonance starts");
            Require(model.Energy == 0 && model.GuardianHealth == 56 && model.Interrupted && !model.ImpactResolved, "resonance spends energy but leaves health intact before impact");
            Require(model.ResolveImpact() && model.GuardianHealth == 40 && model.PlayerHealth == 28, "resonance damage applies only on contact");
            Require(model.Advance() && model.GuardianDamageDealt == 0 && model.ResolveImpact() && model.Advance(), "zero damage interrupted reaction still resolves once");

            foreach (var action in new[] { GuardianBattleAction.Attack, GuardianBattleAction.Attack, GuardianBattleAction.Resonance }) Resolve(model, action);
            Require(model.GuardianHealth == 8 && model.TryAct(GuardianBattleAction.Attack, model.Turn), "final attack starts");
            Require(model.Result == GuardianBattleResult.None && model.GuardianHealth == 8 && !model.Advance(), "victory cannot occur before final contact");
            Require(model.ResolveImpact() && model.GuardianHealth == 0 && model.Result == GuardianBattleResult.None, "lethal contact waits for animation boundary");
            Require(model.Advance() && model.Result == GuardianBattleResult.Victory && !model.ResolveImpact(), "victory finalizes at boundary");

            var defeat = new GuardianBattleModel();
            for (int i = 0; i < 4; i++) Resolve(defeat, GuardianBattleAction.Attack);
            Require(defeat.PlayerHealth == 4 && defeat.TryAct(GuardianBattleAction.Attack, defeat.Turn), "fatal probe is still telegraphed");
            Require(defeat.ResolveImpact() && defeat.Advance() && defeat.PlayerHealth == 4 && defeat.Result == GuardianBattleResult.None, "fatal counter does not damage on entry");
            Require(defeat.ResolveImpact() && defeat.PlayerHealth == 0 && defeat.Result == GuardianBattleResult.None, "fatal counter waits for boundary");
            Require(defeat.Advance() && defeat.Result == GuardianBattleResult.Defeat, "defeat finalizes at boundary");
            checks.Add("timing: selection and counter entry preserve HP; both contacts are idempotent; premature boundaries and old idle contacts rejected; lethal outcomes wait for boundaries");
        }

        static void CheckInterrupt(List<string> checks)
        {
            var model = new GuardianBattleModel();
            Resolve(model, GuardianBattleAction.Attack);
            Resolve(model, GuardianBattleAction.Attack);
            Require(model.Intent == GuardianBattleIntent.HeavyStrike && model.Energy == 2, "heavy strike is advertised a full turn ahead");
            Resolve(model, GuardianBattleAction.Resonance);
            Require(model.PlayerHealth == 28 && model.GuardianHealth == 40 && model.Energy == 0, "interrupt spends two energy and prevents 24 damage");
            Require(model.Intent == GuardianBattleIntent.Exposed && model.Interrupted, "interrupt exposes the weak point");
            Resolve(model, GuardianBattleAction.Attack);
            Require(model.PhaseTwo && model.Intent == GuardianBattleIntent.Windup && model.Energy == 1, "loosened armor skips probe and explicitly telegraphs another windup");
            Resolve(model, GuardianBattleAction.Attack);
            Require(model.Intent == GuardianBattleIntent.HeavyStrike && model.Energy == 2, "second-phase heavy strike follows a full windup turn");
            Resolve(model, GuardianBattleAction.Resonance);
            Require(model.Intent == GuardianBattleIntent.Exposed && model.PlayerHealth == 28, "second-phase interrupt still exposes the weak point");
            var early = new GuardianBattleModel();
            foreach (var action in new[] { GuardianBattleAction.Attack, GuardianBattleAction.Attack, GuardianBattleAction.Guard,
                GuardianBattleAction.Attack, GuardianBattleAction.Guard }) Resolve(early, action);
            Require(!early.PhaseTwo && early.Intent == GuardianBattleIntent.Windup && early.Energy == 3, "first-phase exposed recovery retains probe before windup");
            Resolve(early, GuardianBattleAction.Resonance);
            Require(early.Intent == GuardianBattleIntent.Exposed && early.PhaseTwo && early.PlayerHealth == 20, "early interrupt skips the future heavy strike even while entering phase two");
            checks.Add("intent: both early windup and imminent heavy strike can be interrupted; next intent becomes exposed");
        }

        static void CheckGuardAndWeakPoint(List<string> checks)
        {
            var model = new GuardianBattleModel();
            Resolve(model, GuardianBattleAction.Attack);
            Resolve(model, GuardianBattleAction.Attack);
            Resolve(model, GuardianBattleAction.Guard);
            Require(model.PlayerHealth == 22 && model.Energy == 2 && model.GuardianDamageDealt == 6, "guard reduces heavy strike from 24 to 6");
            Resolve(model, GuardianBattleAction.Resonance);
            Require(model.GuardianHealth == 28 && model.Energy == 0 && model.HitWeakPoint, "exposed resonance inflicts 28 and consumes energy");
            Require(model.PhaseTwo && model.Intent == GuardianBattleIntent.Windup, "weak-point strike enters the faster second phase");
            Resolve(model, GuardianBattleAction.Attack);
            Resolve(model, GuardianBattleAction.Guard);
            Require(model.PlayerHealth == 16 && model.Energy == 1, "second-phase heavy requires another defense decision");
            Resolve(model, GuardianBattleAction.Attack);
            Resolve(model, GuardianBattleAction.Resonance);
            Require(model.Result == GuardianBattleResult.Victory && model.PlayerHealth == 16, "guard plus weak point strategy wins");
            var expired = new GuardianBattleModel();
            Resolve(expired, GuardianBattleAction.Guard);
            Resolve(expired, GuardianBattleAction.Attack);
            Resolve(expired, GuardianBattleAction.Attack);
            Require(expired.PlayerHealth == 10 && expired.GuardianDamageDealt == 24, "guard expires after its own turn");
            checks.Add("defense: 75 percent reduction, one-turn guard expiry and weak-point bonus lead to a valid victory");
        }

        static void CheckOutcomes(List<string> checks)
        {
            var attackOnly = new GuardianBattleModel();
            int actions = 0;
            while (attackOnly.CanAct && actions++ < 20) Resolve(attackOnly, GuardianBattleAction.Attack);
            Require(attackOnly.Result == GuardianBattleResult.Defeat && attackOnly.PlayerHealth == 0, "ignoring intent has a real defeat outcome");
            Require(!attackOnly.Retreat() && !attackOnly.Advance() && !attackOnly.TryAct(GuardianBattleAction.Attack, attackOnly.Turn), "defeat is terminal");
            var interrupted = new GuardianBattleModel();
            foreach (var action in new[] { GuardianBattleAction.Attack, GuardianBattleAction.Attack, GuardianBattleAction.Resonance,
                GuardianBattleAction.Attack, GuardianBattleAction.Attack, GuardianBattleAction.Resonance, GuardianBattleAction.Attack }) Resolve(interrupted, action);
            Require(interrupted.Result == GuardianBattleResult.Victory && interrupted.PlayerHealth == 28, "interrupt strategy wins with expected remaining health");
            Require(!interrupted.Retreat() && !interrupted.Advance(), "victory is terminal");
            foreach (int boundary in new[] { 0, 1, 2, 3, 4 })
            {
                var retreat = new GuardianBattleModel();
                if (boundary > 0) retreat.TryAct(GuardianBattleAction.Attack, 1);
                if (boundary > 1) retreat.ResolveImpact();
                if (boundary > 2) retreat.Advance();
                if (boundary > 3) retreat.ResolveImpact();
                int health = retreat.PlayerHealth, enemyHealth = retreat.GuardianHealth, energy = retreat.Energy;
                Require(retreat.TryAct(GuardianBattleAction.Retreat, retreat.Turn) && retreat.Result == GuardianBattleResult.Retreat, "can retreat while awaiting or resolving before/after either contact");
                Require(!retreat.Retreat() && !retreat.Advance() && !retreat.ResolveImpact(), "retreat cancels unresolved contact and is terminal");
                Require(retreat.PlayerHealth == health && retreat.GuardianHealth == enemyHealth && retreat.Energy == energy, "retreat has no hidden resource penalty");
            }
            var cancelledResonance = new GuardianBattleModel();
            Resolve(cancelledResonance, GuardianBattleAction.Attack);
            Resolve(cancelledResonance, GuardianBattleAction.Attack);
            Require(cancelledResonance.TryAct(GuardianBattleAction.Resonance, cancelledResonance.Turn) && cancelledResonance.Retreat(), "resonance can be cancelled before contact");
            Require(!cancelledResonance.ResolveImpact() && cancelledResonance.GuardianHealth == 56 && cancelledResonance.PlayerHealth == 28 && cancelledResonance.Energy == 0, "cancelled resonance keeps only its already-spent energy cost");
            var fresh = new GuardianBattleModel();
            Require(fresh.PlayerHealth == 36 && fresh.GuardianHealth == 72 && fresh.Energy == 0, "retry starts a clean, separate encounter");
            checks.Add("outcomes: interrupt victory, defense victory, attack-only defeat, retreat at every phase and clean retry pass");
        }

        static GuardianBattleModel Replay(GuardianBattleAction[] path)
        {
            var model = new GuardianBattleModel();
            foreach (var action in path) Resolve(model, action);
            return model;
        }

        static string Key(GuardianBattleModel model) => model.PlayerHealth + ":" + model.GuardianHealth + ":" + model.Energy + ":" + model.Intent + ":" + model.Result;

        static void ExploreReachableStates(List<string> checks)
        {
            var frontier = new Queue<GuardianBattleAction[]>();
            var visited = new HashSet<string>();
            var successors = new Dictionary<string, List<string>>();
            frontier.Enqueue(new GuardianBattleAction[0]);
            int transitions = 0, victories = 0, defeats = 0;
            while (frontier.Count > 0)
            {
                var path = frontier.Dequeue(); var model = Replay(path);
                Require(model.PlayerHealth >= 0 && model.PlayerHealth <= 36 && model.GuardianHealth >= 0 && model.GuardianHealth <= 72
                    && model.Energy >= 0 && model.Energy <= 3, "reachable health and energy remain bounded");
                Require(Key(model) == Key(Replay(path)), "the same choices deterministically replay to the same state");
                string key = Key(model);
                if (!visited.Add(key)) continue;
                successors[key] = new List<string>();
                if (model.Result == GuardianBattleResult.Victory) { victories++; continue; }
                if (model.Result == GuardianBattleResult.Defeat) { defeats++; continue; }
                Require(path.Length < 40, "the encounter cannot continue indefinitely without a terminal result");
                foreach (var action in new[] { GuardianBattleAction.Attack, GuardianBattleAction.Guard, GuardianBattleAction.Resonance })
                {
                    if (action == GuardianBattleAction.Resonance && !model.CanUseResonance) continue;
                    var child = new GuardianBattleAction[path.Length + 1];
                    Array.Copy(path, child, path.Length); child[path.Length] = action;
                    successors[key].Add(Key(Replay(child)));
                    frontier.Enqueue(child); transitions++;
                }
            }
            Require(victories > 0 && defeats > 0 && visited.Count > 100, "reachability covers both outcomes and nontrivial choices");
            // Remove terminal states, then their predecessors. Any remaining state belongs to,
            // or can reach, a cycle that could keep combat alive forever.
            var parents = new Dictionary<string, List<string>>();
            var remaining = new Dictionary<string, int>();
            var longest = new Dictionary<string, int>();
            var terminating = new Queue<string>();
            foreach (string key in visited) { parents[key] = new List<string>(); longest[key] = 0; }
            foreach (var pair in successors)
            {
                remaining[pair.Key] = pair.Value.Count;
                if (pair.Value.Count == 0) terminating.Enqueue(pair.Key);
                foreach (string child in pair.Value) parents[child].Add(pair.Key);
            }
            int proven = 0, longestEncounter = 0;
            while (terminating.Count > 0)
            {
                string child = terminating.Dequeue(); proven++;
                longestEncounter = Math.Max(longestEncounter, longest[child]);
                foreach (string parent in parents[child])
                {
                    longest[parent] = Math.Max(longest[parent], longest[child] + 1);
                    if (--remaining[parent] == 0) terminating.Enqueue(parent);
                }
            }
            Require(proven == visited.Count, "all reachable choices terminate; no reachable cycles or stalled states");
            checks.Add("state space: " + visited.Count + " unique states / " + transitions + " legal transitions; deterministic, bounded and all paths terminate within " + longestEncounter + " actions");
        }
    }
}
