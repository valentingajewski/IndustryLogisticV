using System;
using System.Collections.Generic;
using System.Drawing;
using LSOL.Systems;

namespace LSOL.UI
{
    internal sealed class SuccessesTabletApp : ITabletApp
    {
        private static readonly Color LockedIdle = Color.FromArgb(176, 40, 44, 48);
        private static readonly Color LockedActive = Color.FromArgb(220, 92, 104, 112);
        private static readonly Color UnlockedIdle = Color.FromArgb(180, 46, 60, 48);
        private static readonly Color UnlockedActive = Color.FromArgb(224, 104, 140, 114);

        private readonly PlayerSuccessTracker _tracker;

        public SuccessesTabletApp(PlayerSuccessTracker tracker)
        {
            _tracker = tracker;
        }

        public string AppId
        {
            get { return TabletAppIds.Successes; }
        }

        public TabletShellPage BuildPage(TabletShellContext context, TabletRoute route)
        {
            var statuses = _tracker != null
                ? _tracker.GetStatuses()
                : Array.Empty<PlayerSuccessStatus>();
            var doctrines = _tracker != null
                ? _tracker.GetDoctrineStatuses()
                : Array.Empty<CompanyDoctrineStatus>();
            var endgame = _tracker != null
                ? _tracker.GetEndgameSummary()
                : new CompanyEndgameSummary();
            var unlockedCount = _tracker != null ? _tracker.UnlockedCount : 0;
            var totalCount = _tracker != null ? _tracker.TotalCount : 0;
            var items = new List<MenuItem>();

            items.Add(TabletUiHelpers.CreateInfoItem(
                "Progress",
                string.Format("{0}/{1} unlocked", unlockedCount, totalCount),
                totalCount > 0 ? (float?)unlockedCount / totalCount : null));

            items.Add(TabletUiHelpers.CreateInfoItem(
                endgame.Headline ?? "Endgame posture forming",
                endgame.Detail ?? "Doctrine, HQ, and district prestige will appear here as the company matures.",
                endgame.PrestigeScore > 0.001f ? (float?)(endgame.PrestigeScore / 100f) : 0f));

            for (int i = 0; i < doctrines.Count; i++)
            {
                var doctrine = doctrines[i];
                if (doctrine == null)
                {
                    continue;
                }

                var capturedDoctrine = doctrine;
                items.Add(TabletUiHelpers.CreateInfoItem(
                    capturedDoctrine.IsActive
                        ? string.Format("{0} [ACTIVE]", capturedDoctrine.Name ?? string.Empty)
                        : capturedDoctrine.Name ?? string.Empty,
                    BuildDoctrineDetail(capturedDoctrine),
                    capturedDoctrine.ProgressRatio));
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null)
                {
                    continue;
                }

                var capturedStatus = status;
                items.Add(new MenuItem
                {
                    CaptionFactory = () => capturedStatus.Name ?? string.Empty,
                    DetailFactory = () => BuildDetail(capturedStatus),
                    IconLabelFactory = () => capturedStatus.IsUnlocked ? "DONE" : "LOCK",
                    ProgressRatioFactory = () => capturedStatus.IsUnlocked ? 1f : capturedStatus.ProgressRatio,
                    IdleBackgroundColor = capturedStatus.IsUnlocked ? UnlockedIdle : LockedIdle,
                    SelectedBackgroundColor = capturedStatus.IsUnlocked ? UnlockedActive : LockedActive,
                });
            }

            items.Add(TabletUiHelpers.CreateNavigationItem("Back", "Return to the company hub.", () => context.GoBack(), "BACK"));

            return new TabletShellPage
            {
                Title = "Successes",
                Subtitle = string.Format("{0}/{1} unlocked | Prestige {2:0}", unlockedCount, totalCount, endgame.PrestigeScore),
                HeaderRightText = string.Format("{0}/{1}", unlockedCount, totalCount),
                FooterText = "Arrow Keys Navigate | Enter Select | Backspace/Esc Back",
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 7,
                Layout = SimpleMenuTabletLayout.List,
                Items = items,
            };
        }

        private static string BuildDetail(PlayerSuccessStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            if (status.IsUnlocked)
            {
                return string.Format("{0}\nUnlocked", status.Description ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(status.ProgressText))
            {
                return status.Description ?? string.Empty;
            }

            return string.Format("{0}\nProgress: {1}", status.Description ?? string.Empty, status.ProgressText);
        }

        private static string BuildDoctrineDetail(CompanyDoctrineStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            if (status.Tier <= 0)
            {
                return string.Format(
                    "{0}\nProgress: {1}",
                    status.FocusSummary ?? string.Empty,
                    status.ProgressText ?? string.Empty);
            }

            return string.Format(
                "{0}\nTier {1} | {2}\nTradeoff: {3}\nProgress: {4}",
                status.FocusSummary ?? string.Empty,
                CompanyDoctrineSystem.BuildTierLabel(status.EffectiveTier > 0 ? status.EffectiveTier : status.Tier),
                status.BonusSummary ?? string.Empty,
                status.TradeoffSummary ?? string.Empty,
                status.ProgressText ?? string.Empty);
        }
    }
}