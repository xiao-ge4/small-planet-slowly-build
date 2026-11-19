using System;

namespace DawnGarden
{
    public enum GuardianBattleAction { Attack, Guard, Resonance, Retreat }
    public enum GuardianBattlePhase { AwaitingAction, PlayerAction, GuardianAction, Complete }
    public enum GuardianBattleIntent { Probe, Windup, HeavyStrike, Exposed }
    public enum GuardianBattleResult { None, Victory, Defeat, Retreat }

    /// <summary>A deterministic, in-memory encounter. This type never reads or writes garden progress.</summary>
    public sealed class GuardianBattleModel
    {
        public const int PlayerMaxHealth = 36, GuardianMaxHealth = 72, MaxEnergy = 3;
        public const int AttackDamage = 8, ResonanceCost = 2, ResonanceDamage = 16, WeakPointDamage = 28;
        public int PlayerHealth { get; private set; } = PlayerMaxHealth;
        public int GuardianHealth { get; private set; } = GuardianMaxHealth;
        public int Energy { get; private set; }
        public int Turn { get; private set; } = 1;
        public GuardianBattlePhase Phase { get; private set; } = GuardianBattlePhase.AwaitingAction;
        public GuardianBattleIntent Intent { get; private set; } = GuardianBattleIntent.Probe;
        public GuardianBattleResult Result { get; private set; }
        public GuardianBattleAction LastAction { get; private set; }
        public int PlayerDamageDealt { get; private set; }
        public int GuardianDamageDealt { get; private set; }
        public bool Interrupted { get; private set; }
        public bool HitWeakPoint { get; private set; }
        public bool ImpactResolved { get; private set; }
        public bool PhaseTwo => GuardianHealth <= GuardianMaxHealth / 2;
        public string Message { get; private set; } = "先看守卫的意图，再选择行动。普攻可以积攒能量。";
        public bool CanAct => Phase == GuardianBattlePhase.AwaitingAction && Result == GuardianBattleResult.None;
        public bool CanUseResonance => CanAct && Energy >= ResonanceCost;
        public bool CanInterrupt => Intent == GuardianBattleIntent.Windup || Intent == GuardianBattleIntent.HeavyStrike;
        public int IntentDamage => Intent == GuardianBattleIntent.Probe ? 8 : Intent == GuardianBattleIntent.HeavyStrike ? 24 : 0;
        public string IntentTitle => Intent == GuardianBattleIntent.Probe ? "试探挥击" : Intent == GuardianBattleIntent.Windup ? (PhaseTwo ? "急促蓄力" : "正在蓄力") : Intent == GuardianBattleIntent.HeavyStrike ? "蓄力重击" : "弱点显露";
        public string IntentDescription => Intent == GuardianBattleIntent.Probe ? "本回合挥击，造成 8 点伤害。"
            : Intent == GuardianBattleIntent.Windup ? (PhaseTwo ? "石甲松动，守卫加快节奏。本回合不攻击；下回合重击 24 点。" : "本回合不攻击；下回合重击 24 点。")
            : Intent == GuardianBattleIntent.HeavyStrike ? "本回合重击，造成 24 点伤害。"
            : "本回合不攻击，石甲裂缝中的核心显露。";

        /// <summary>expectedTurn rejects delayed button events belonging to an older turn.</summary>
        public bool TryAct(GuardianBattleAction action, int expectedTurn)
        {
            if (expectedTurn != Turn || !Enum.IsDefined(typeof(GuardianBattleAction), action)) return false;
            if (action == GuardianBattleAction.Retreat) return Retreat();
            if (!CanAct) return false;
            if (action == GuardianBattleAction.Resonance && Energy < ResonanceCost) return false;
            LastAction = action;
            PlayerDamageDealt = GuardianDamageDealt = 0;
            Interrupted = HitWeakPoint = false;
            ImpactResolved = false;
            if (action == GuardianBattleAction.Attack)
            {
                PlayerDamageDealt = Math.Min(AttackDamage, GuardianHealth);
                Message = "挥出普攻……";
            }
            else if (action == GuardianBattleAction.Guard)
                Message = "展开守护：本回合受到的伤害减少 75%。";
            else
            {
                Energy -= ResonanceCost;
                HitWeakPoint = Intent == GuardianBattleIntent.Exposed;
                Interrupted = CanInterrupt;
                PlayerDamageDealt = Math.Min(HitWeakPoint ? WeakPointDamage : ResonanceDamage, GuardianHealth);
                Message = "汇聚共鸣……";
            }
            Phase = GuardianBattlePhase.PlayerAction;
            return true;
        }

        /// <summary>Apply one phase's pending effects at visible contact, never at input or phase entry.</summary>
        public bool ResolveImpact()
        {
            if (Result != GuardianBattleResult.None || ImpactResolved) return false;
            if (Phase == GuardianBattlePhase.PlayerAction)
            {
                GuardianHealth = Math.Max(0, GuardianHealth - PlayerDamageDealt);
                if (LastAction == GuardianBattleAction.Attack)
                {
                    bool energyFull = Energy == MaxEnergy;
                    Energy = Math.Min(MaxEnergy, Energy + 1);
                    Message = "普攻命中，造成 " + PlayerDamageDealt + " 点伤害，" + (energyFull ? "能量已满。" : "能量 +1。");
                }
                else if (LastAction == GuardianBattleAction.Resonance)
                    Message = HitWeakPoint ? "击中弱点！共鸣造成 " + PlayerDamageDealt + " 点伤害。"
                        : Interrupted ? "共鸣造成 " + PlayerDamageDealt + " 点伤害，打断了蓄力！"
                        : "共鸣造成 " + PlayerDamageDealt + " 点伤害。";
            }
            else if (Phase == GuardianBattlePhase.GuardianAction)
            {
                PlayerHealth = Math.Max(0, PlayerHealth - GuardianDamageDealt);
                Message = Interrupted ? "蓄力被打断，守卫失去平衡，下一回合露出弱点。"
                    : Intent == GuardianBattleIntent.Windup ? "守卫正在蓄力。下一回合将发动重击！"
                    : Intent == GuardianBattleIntent.Exposed ? (PhaseTwo ? "石甲松动，守卫调整姿态，下一回合急促蓄力。" : "守卫调整姿态，即将再次试探。")
                    : LastAction == GuardianBattleAction.Guard ? "守护挡下大部分冲击，只受到 " + GuardianDamageDealt + " 点伤害。"
                    : "受到 " + GuardianDamageDealt + " 点伤害。";
            }
            else return false;
            ImpactResolved = true;
            return true;
        }

        /// <summary>One discrete animation boundary; elapsed frame time never changes combat arithmetic.</summary>
        public bool Advance()
        {
            if (Result != GuardianBattleResult.None || !ImpactResolved) return false;
            if (Phase == GuardianBattlePhase.PlayerAction)
            {
                if (GuardianHealth == 0) { Finish(GuardianBattleResult.Victory, "守卫平静下来。你掌握了观察意图与共鸣的节奏。"); return true; }
                GuardianDamageDealt = Interrupted ? 0 : IntentDamage;
                if (LastAction == GuardianBattleAction.Guard) GuardianDamageDealt = (GuardianDamageDealt + 3) / 4;
                GuardianDamageDealt = Math.Min(GuardianDamageDealt, PlayerHealth);
                Message = Interrupted ? "守卫踉跄失衡……" : GuardianDamageDealt > 0 ? "守卫挥出攻击……" : "守卫调整姿态……";
                Phase = GuardianBattlePhase.GuardianAction;
                ImpactResolved = false;
                return true;
            }
            if (Phase != GuardianBattlePhase.GuardianAction) return false;
            if (PlayerHealth == 0) { Finish(GuardianBattleResult.Defeat, "这次先休息一下。家园、许可和已获得的进度都不会损失。"); return true; }
            Intent = Interrupted ? GuardianBattleIntent.Exposed
                : Intent == GuardianBattleIntent.Exposed && PhaseTwo ? GuardianBattleIntent.Windup
                : (GuardianBattleIntent)(((int)Intent + 1) % 4);
            Turn++;
            Phase = GuardianBattlePhase.AwaitingAction;
            ImpactResolved = false;
            Message = "第 " + Turn + " 回合 · " + IntentDescription;
            return true;
        }

        public bool Retreat()
        {
            if (Result != GuardianBattleResult.None) return false;
            Finish(GuardianBattleResult.Retreat, "已经安全撤退。家园、许可和已获得的进度都不会损失。");
            return true;
        }

        void Finish(GuardianBattleResult result, string message)
        {
            Result = result;
            Phase = GuardianBattlePhase.Complete;
            ImpactResolved = false;
            Message = message;
        }
    }
}
