using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Systems
{
    public enum PlayerSkillId
    {
        Trucking = 0,
        Towing = 1,
        FoodDelivery = 2,
        Garbage = 3,
        Bus = 5,
    }

    public enum PlayerSkillXpSource
    {
        Player = 0,
        Npc = 1,
    }

    public sealed class PlayerSkillXpSnapshot
    {
        public int SkillId { get; set; }

        public float Xp { get; set; }
    }

    public sealed class PlayerSkillPersistenceSnapshot
    {
        public PlayerSkillPersistenceSnapshot()
        {
            SkillXp = new List<PlayerSkillXpSnapshot>();
        }

        public List<PlayerSkillXpSnapshot> SkillXp { get; }

        public bool HasData
        {
            get
            {
                return SkillXp != null
                    && SkillXp.Any(entry => entry != null && entry.Xp > 0.001f);
            }
        }
    }

    public sealed class PlayerSkillStatus
    {
        public PlayerSkillId SkillId { get; set; }

        public string Name { get; set; }

        public int Level { get; set; }

        public float TotalXp { get; set; }

        public float XpIntoLevel { get; set; }

        public float XpForNextLevel { get; set; }

        public float ProgressRatio { get; set; }

        public float BonusPercent { get; set; }

        public bool HasLiveXpSource { get; set; }
    }

    internal sealed class PlayerSkillSystem
    {
        public const float TruckingXpPerCompletedDelivery = 100f;
        public const float TruckingXpPerTon = 10f;

        private const double DefaultXpGrowthExponent = 1.5;
        private const float DefaultBonusCap = 0.25f;
        private const double DefaultBonusCurveK = 0.06;

        private sealed class SkillDefinition
        {
            public PlayerSkillId Id { get; set; }

            public string Name { get; set; }

            public float BaseXp { get; set; }

            public double XpGrowthExponent { get; set; }

            public float BonusCap { get; set; }

            public double BonusCurveK { get; set; }

            public bool HasLiveXpSource { get; set; }
        }

        private static readonly IReadOnlyDictionary<PlayerSkillId, SkillDefinition> Definitions = BuildDefinitions();

        private readonly Dictionary<PlayerSkillId, float> _xpBySkill;

        public PlayerSkillSystem()
        {
            _xpBySkill = new Dictionary<PlayerSkillId, float>();
            foreach (var id in GetAllSkillIds())
            {
                _xpBySkill[id] = 0f;
            }
        }

        public static IReadOnlyList<PlayerSkillId> GetAllSkillIds()
        {
            return new[]
            {
                PlayerSkillId.Trucking,
                PlayerSkillId.Towing,
                PlayerSkillId.FoodDelivery,
                PlayerSkillId.Garbage,
                PlayerSkillId.Bus,
            };
        }

        public static string GetSkillName(PlayerSkillId id)
        {
            var definition = ResolveDefinition(id);
            return definition != null ? definition.Name : id.ToString();
        }

        public void ResetForNewSave()
        {
            foreach (var id in GetAllSkillIds())
            {
                _xpBySkill[id] = 0f;
            }
        }

        public void ApplyPersistenceSnapshot(PlayerSkillPersistenceSnapshot snapshot)
        {
            ResetForNewSave();
            if (snapshot == null || snapshot.SkillXp == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.SkillXp.Count; i++)
            {
                var entry = snapshot.SkillXp[i];
                if (entry == null || entry.Xp <= 0.001f)
                {
                    continue;
                }

                var id = ResolveSkillId(entry.SkillId);
                if (!id.HasValue)
                {
                    continue;
                }

                _xpBySkill[id.Value] = Math.Max(0f, entry.Xp);
            }
        }

        public PlayerSkillPersistenceSnapshot CreatePersistenceSnapshot()
        {
            var snapshot = new PlayerSkillPersistenceSnapshot();
            foreach (var id in GetAllSkillIds())
            {
                snapshot.SkillXp.Add(new PlayerSkillXpSnapshot
                {
                    SkillId = (int)id,
                    Xp = Math.Max(0f, _xpBySkill[id]),
                });
            }

            return snapshot;
        }

        public void AddXp(PlayerSkillId id, float amount, PlayerSkillXpSource source = PlayerSkillXpSource.Player)
        {
            if (source != PlayerSkillXpSource.Player || amount <= 0.001f)
            {
                return;
            }

            if (!_xpBySkill.ContainsKey(id))
            {
                _xpBySkill[id] = 0f;
            }

            _xpBySkill[id] = _xpBySkill[id] + amount;
        }

        public float GetTruckingDeliveryXp(float deliveredTons, bool completedDelivery)
        {
            var xp = TruckingXpPerTon * Math.Max(0f, deliveredTons);
            if (completedDelivery)
            {
                xp += TruckingXpPerCompletedDelivery;
            }

            return xp;
        }

        public float GetTotalXp(PlayerSkillId id)
        {
            float xp;
            return _xpBySkill.TryGetValue(id, out xp) ? Math.Max(0f, xp) : 0f;
        }

        public int GetLevel(PlayerSkillId id)
        {
            int level;
            float cumulative;
            ResolveLevel(id, GetTotalXp(id), out level, out cumulative);
            return level;
        }

        public float GetXpIntoLevel(PlayerSkillId id)
        {
            int level;
            float cumulative;
            ResolveLevel(id, GetTotalXp(id), out level, out cumulative);
            return Math.Max(0f, GetTotalXp(id) - cumulative);
        }

        public float GetXpForNextLevel(PlayerSkillId id)
        {
            return XpForLevel(id, GetLevel(id) + 1);
        }

        public float GetProgressRatio(PlayerSkillId id)
        {
            var xpForNext = GetXpForNextLevel(id);
            if (xpForNext <= 0.001f)
            {
                return 1f;
            }

            return ModMath.Clamp01(GetXpIntoLevel(id) / xpForNext);
        }

        public float GetBonusMultiplier(PlayerSkillId id)
        {
            var definition = ResolveDefinition(id);
            if (definition == null)
            {
                return 1f;
            }

            var level = GetLevel(id);
            var multiplier = 1f + definition.BonusCap * (float)(1.0 - Math.Exp(-definition.BonusCurveK * level));
            return Math.Max(1f, multiplier);
        }

        public float GetBonusPercent(PlayerSkillId id)
        {
            return (GetBonusMultiplier(id) - 1f) * 100f;
        }

        public float GetXpRequiredToReachLevel(PlayerSkillId id, int targetLevel)
        {
            targetLevel = Math.Max(0, targetLevel);
            var targetCumulative = CumulativeXpForLevel(id, targetLevel);
            return Math.Max(0f, targetCumulative - GetTotalXp(id));
        }

        public float AdvanceLevel(PlayerSkillId id, int levelCount)
        {
            var targetLevel = Math.Max(0, GetLevel(id) + Math.Max(0, levelCount));
            var required = GetXpRequiredToReachLevel(id, targetLevel);
            if (required <= 0.001f)
            {
                return 0f;
            }

            AddXp(id, required, PlayerSkillXpSource.Player);
            return required;
        }

        public IReadOnlyList<PlayerSkillStatus> GetStatuses()
        {
            return GetAllSkillIds()
                .Select(BuildStatus)
                .ToArray();
        }

        private PlayerSkillStatus BuildStatus(PlayerSkillId id)
        {
            var definition = ResolveDefinition(id);
            var xp = GetTotalXp(id);
            var level = GetLevel(id);
            var xpIntoLevel = GetXpIntoLevel(id);
            var xpForNextLevel = GetXpForNextLevel(id);

            return new PlayerSkillStatus
            {
                SkillId = id,
                Name = definition != null ? definition.Name : id.ToString(),
                Level = level,
                TotalXp = xp,
                XpIntoLevel = xpIntoLevel,
                XpForNextLevel = xpForNextLevel,
                ProgressRatio = xpForNextLevel > 0.001f ? ModMath.Clamp01(xpIntoLevel / xpForNextLevel) : 1f,
                BonusPercent = GetBonusPercent(id),
                HasLiveXpSource = definition != null && definition.HasLiveXpSource,
            };
        }

        private static void ResolveLevel(PlayerSkillId id, float xp, out int level, out float cumulativeXp)
        {
            level = 0;
            cumulativeXp = 0f;
            while (true)
            {
                var nextLevelCost = XpForLevel(id, level + 1);
                if (nextLevelCost <= 0.001f || xp < cumulativeXp + nextLevelCost)
                {
                    return;
                }

                cumulativeXp += nextLevelCost;
                level += 1;
            }
        }

        private static float XpForLevel(PlayerSkillId id, int level)
        {
            var definition = ResolveDefinition(id);
            if (definition == null || level <= 0)
            {
                return 0f;
            }

            return (float)(definition.BaseXp * Math.Pow(level, definition.XpGrowthExponent));
        }

        private static float CumulativeXpForLevel(PlayerSkillId id, int level)
        {
            var total = 0f;
            for (int n = 1; n <= level; n++)
            {
                total += XpForLevel(id, n);
            }

            return total;
        }

        private static SkillDefinition ResolveDefinition(PlayerSkillId id)
        {
            SkillDefinition definition;
            return Definitions.TryGetValue(id, out definition) ? definition : null;
        }

        private static PlayerSkillId? ResolveSkillId(int rawId)
        {
            var ids = GetAllSkillIds();
            for (int i = 0; i < ids.Count; i++)
            {
                if ((int)ids[i] == rawId)
                {
                    return ids[i];
                }
            }

            return null;
        }

        private static IReadOnlyDictionary<PlayerSkillId, SkillDefinition> BuildDefinitions()
        {
            return new Dictionary<PlayerSkillId, SkillDefinition>
            {
                { PlayerSkillId.Trucking, new SkillDefinition { Id = PlayerSkillId.Trucking, Name = "Trucking", BaseXp = 100f, XpGrowthExponent = DefaultXpGrowthExponent, BonusCap = DefaultBonusCap, BonusCurveK = DefaultBonusCurveK, HasLiveXpSource = true } },
                { PlayerSkillId.Towing, new SkillDefinition { Id = PlayerSkillId.Towing, Name = "Towing", BaseXp = 100f, XpGrowthExponent = DefaultXpGrowthExponent, BonusCap = DefaultBonusCap, BonusCurveK = DefaultBonusCurveK, HasLiveXpSource = false } },
                { PlayerSkillId.FoodDelivery, new SkillDefinition { Id = PlayerSkillId.FoodDelivery, Name = "Food Delivery", BaseXp = 100f, XpGrowthExponent = DefaultXpGrowthExponent, BonusCap = DefaultBonusCap, BonusCurveK = DefaultBonusCurveK, HasLiveXpSource = true } },
                { PlayerSkillId.Garbage, new SkillDefinition { Id = PlayerSkillId.Garbage, Name = "Garbage", BaseXp = 100f, XpGrowthExponent = DefaultXpGrowthExponent, BonusCap = DefaultBonusCap, BonusCurveK = DefaultBonusCurveK, HasLiveXpSource = false } },
                { PlayerSkillId.Bus, new SkillDefinition { Id = PlayerSkillId.Bus, Name = "Bus", BaseXp = 100f, XpGrowthExponent = DefaultXpGrowthExponent, BonusCap = DefaultBonusCap, BonusCurveK = DefaultBonusCurveK, HasLiveXpSource = false } },
            };
        }
    }
}
