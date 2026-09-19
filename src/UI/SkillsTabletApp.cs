using System;
using System.Collections.Generic;
using System.Drawing;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class SkillsTabletApp : ITabletApp
    {
        private static readonly Color LiveIdle = Color.FromArgb(180, 46, 60, 48);
        private static readonly Color LiveActive = Color.FromArgb(224, 104, 140, 114);
        private static readonly Color PendingIdle = Color.FromArgb(176, 40, 44, 48);
        private static readonly Color PendingActive = Color.FromArgb(220, 92, 104, 112);

        private readonly PlayerSkillSystem _skillSystem;

        public SkillsTabletApp(PlayerSkillSystem skillSystem)
        {
            _skillSystem = skillSystem;
        }

        public string AppId
        {
            get { return TabletAppIds.Skills; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var pageId = (route != null ? route.PageId : string.Empty) ?? string.Empty;
            var skillId = TryParseSkillId(pageId);
            if (skillId.HasValue)
            {
                return BuildSkillPage(context, skillId.Value);
            }

            return BuildRootPage(context);
        }

        private TabletShellPage BuildRootPage(TabletShellContext context)
        {
            var statuses = _skillSystem != null
                ? _skillSystem.GetStatuses()
                : Array.Empty<PlayerSkillStatus>();
            var items = new List<MenuItem>();

            for (int i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null)
                {
                    continue;
                }

                var captured = status;
                items.Add(TabletUiHelpers.CreateActionItem(
                    GetSkillDisplayName(captured.SkillId, captured.Name),
                    LocalizedText.FormatOrDefault(
                        "tablet.skills.rootDetail",
                        "Level {0} | +{1:0.#}% payout | {2:N0} / {3:N0} XP",
                        captured.Level,
                        captured.BonusPercent,
                        captured.XpIntoLevel,
                        captured.XpForNextLevel),
                    () =>
                    {
                        if (context != null)
                        {
                            context.Push(TabletAppIds.Skills, captured.SkillId.ToString(), captured.SkillId);
                        }
                    },
                    captured.HasLiveXpSource ? LiveIdle : PendingIdle,
                    captured.HasLiveXpSource ? LiveActive : PendingActive,
                    captured.ProgressRatio,
                    captured.HasLiveXpSource ? "LIVE" : "SOON"));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(
                LocalizedText.GetOrDefault("tablet.common.back", "Back"),
                LocalizedText.GetOrDefault("tablet.skills.backHub", "Return to the company hub."),
                () =>
                {
                    if (context != null)
                    {
                        context.GoBack();
                    }
                },
                "BACK"));

            return new TabletShellPage
            {
                Title = LocalizedText.GetOrDefault("tablet.skills.title", "Skills"),
                Subtitle = LocalizedText.GetOrDefault("tablet.skills.subtitle", "Player job skills and payout bonuses"),
                HeaderRightText = string.Format("{0}", statuses.Count),
                FooterText = LocalizedText.GetOrDefault("tablet.skills.footer", "Arrow Keys Navigate | Enter Select | Backspace/Esc Back"),
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 7,
                Layout = SimpleMenuTabletLayout.List,
                Items = items,
            };
        }

        private TabletShellPage BuildSkillPage(TabletShellContext context, PlayerSkillId skillId)
        {
            var status = ResolveStatus(skillId);
            var items = new List<MenuItem>();

            if (status == null)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.skills.status", "Status"),
                    LocalizedText.GetOrDefault("tablet.skills.unavailable", "Skill state unavailable.")));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.skills.level", "Level"),
                    LocalizedText.FormatOrDefault("tablet.skills.levelDetail", "Level {0}", status.Level),
                    status.ProgressRatio));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.skills.experience", "Experience"),
                    LocalizedText.FormatOrDefault("tablet.skills.xpDetail", "{0:N0} / {1:N0} XP", status.XpIntoLevel, status.XpForNextLevel),
                    status.ProgressRatio));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.skills.bonus", "Payout Bonus"),
                    status.HasLiveXpSource
                        ? LocalizedText.FormatOrDefault("tablet.skills.bonusLive", "+{0:0.#}% on player deliveries", status.BonusPercent)
                        : LocalizedText.GetOrDefault("tablet.skills.bonusPending", "Unlocks when this job type is added."),
                    status.HasLiveXpSource ? (float?)Math.Min(1f, status.BonusPercent / 25f) : null));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(
                LocalizedText.GetOrDefault("tablet.common.back", "Back"),
                LocalizedText.GetOrDefault("tablet.skills.backOverview", "Return to the Skills overview."),
                () =>
                {
                    if (context != null)
                    {
                        context.GoBack();
                    }
                },
                "BACK"));

            return new TabletShellPage
            {
                Title = status != null ? GetSkillDisplayName(skillId, status.Name) : LocalizedText.GetOrDefault("tablet.skills.title", "Skills"),
                Subtitle = status != null
                    ? (status.HasLiveXpSource
                        ? LocalizedText.GetOrDefault("tablet.skills.liveSource", "Live")
                        : LocalizedText.GetOrDefault("tablet.skills.pendingSource", "Coming soon"))
                    : LocalizedText.GetOrDefault("tablet.skills.unavailable", "Skill state unavailable"),
                HeaderRightText = status != null ? string.Format("Lv {0}", status.Level) : string.Empty,
                FooterText = LocalizedText.GetOrDefault("tablet.skills.footer", "Arrow Keys Navigate | Enter Select | Backspace/Esc Back"),
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 6,
                Layout = SimpleMenuTabletLayout.List,
                Items = items,
            };
        }

        private PlayerSkillStatus ResolveStatus(PlayerSkillId skillId)
        {
            if (_skillSystem == null)
            {
                return null;
            }

            var statuses = _skillSystem.GetStatuses();
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i] != null && statuses[i].SkillId == skillId)
                {
                    return statuses[i];
                }
            }

            return null;
        }

        private static PlayerSkillId? TryParseSkillId(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return null;
            }

            var skillIds = PlayerSkillSystem.GetAllSkillIds();
            for (int i = 0; i < skillIds.Count; i++)
            {
                if (string.Equals(skillIds[i].ToString(), pageId, StringComparison.OrdinalIgnoreCase))
                {
                    return skillIds[i];
                }
            }

            return null;
        }

        private static string GetSkillDisplayName(PlayerSkillId skillId, string englishFallback)
        {
            var fallback = string.IsNullOrWhiteSpace(englishFallback) ? skillId.ToString() : englishFallback;
            var key = BuildSkillNameKey(skillId);
            return string.IsNullOrWhiteSpace(key) ? fallback : LocalizedText.GetOrDefault(key, fallback);
        }

        private static string BuildSkillNameKey(PlayerSkillId skillId)
        {
            switch (skillId)
            {
                case PlayerSkillId.Trucking:
                    return "tablet.skills.name.trucking";
                case PlayerSkillId.Towing:
                    return "tablet.skills.name.towing";
                case PlayerSkillId.FoodDelivery:
                    return "tablet.skills.name.foodDelivery";
                case PlayerSkillId.Garbage:
                    return "tablet.skills.name.garbage";
                case PlayerSkillId.Taxi:
                    return "tablet.skills.name.taxi";
                case PlayerSkillId.Bus:
                    return "tablet.skills.name.bus";
                default:
                    return null;
            }
        }
    }
}
