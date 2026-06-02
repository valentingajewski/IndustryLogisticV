using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
            switch ((route != null ? route.PageId : string.Empty) ?? string.Empty)
            {
                case "doctrine":
                    return BuildDoctrinePage(context, route);
                case "prestige":
                    return BuildPrestigePage(context);
                default:
                    return BuildRootPage(context);
            }
        }

        private TabletShellPage BuildRootPage(TabletShellContext context)
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
            var focusDoctrine = ResolveFocusDoctrine(endgame, doctrines);
            var focusStatus = doctrines.FirstOrDefault(status => status != null && status.Doctrine == focusDoctrine);

            items.Add(TabletUiHelpers.CreateInfoItem(
                LocalizedText.GetOrDefault("tablet.successes.progress", "Progress"),
                LocalizedText.FormatOrDefault("tablet.successes.unlockedCount", "{0}/{1} unlocked", unlockedCount, totalCount),
                totalCount > 0 ? (float?)unlockedCount / totalCount : null));

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.GetOrDefault("tablet.successes.currentDoctrine", "Current Doctrine"),
                TabletEndgameStatusFormatter.BuildStatusDetail(endgame),
                () =>
                {
                    if (context != null)
                    {
                        context.Push(TabletAppIds.Successes, "doctrine", focusDoctrine);
                    }
                },
                UnlockedIdle,
                UnlockedActive,
                focusStatus != null ? (float?)focusStatus.ProgressRatio : 0f,
                "DOC"));

            items.Add(TabletUiHelpers.CreateActionItem(
                LocalizedText.GetOrDefault("tablet.successes.prestige", "Prestige"),
                TabletEndgameStatusFormatter.BuildPrestigeSummaryDetail(endgame),
                () =>
                {
                    if (context != null)
                    {
                        context.Push(TabletAppIds.Successes, "prestige");
                    }
                },
                Color.FromArgb(178, 48, 46, 38),
                Color.FromArgb(220, 118, 108, 78),
                endgame.PrestigeScore > 0.001f ? (float?)(endgame.PrestigeScore / 100f) : 0f,
                "PRS"));

            items.Add(TabletUiHelpers.CreateInfoItem(
                LocalizedText.GetOrDefault("tablet.successes.steering", "Steering"),
                TabletEndgameStatusFormatter.BuildSteeringDetail(endgame),
                focusStatus != null ? (float?)focusStatus.ProgressRatio : null));

            for (int i = 0; i < doctrines.Count; i++)
            {
                var doctrine = doctrines[i];
                if (doctrine == null)
                {
                    continue;
                }

                var capturedDoctrine = doctrine;
                items.Add(TabletUiHelpers.CreateActionItem(
                    capturedDoctrine.IsActive
                        ? LocalizedText.FormatOrDefault("tablet.successes.doctrineActive", "{0} [ACTIVE]", capturedDoctrine.Name ?? string.Empty)
                        : capturedDoctrine.Name ?? string.Empty,
                    TabletEndgameStatusFormatter.BuildDoctrineListDetail(capturedDoctrine),
                    () =>
                    {
                        if (context != null)
                        {
                            context.Push(TabletAppIds.Successes, "doctrine", capturedDoctrine.Doctrine);
                        }
                    },
                    capturedDoctrine.IsActive ? UnlockedIdle : LockedIdle,
                    capturedDoctrine.IsActive ? UnlockedActive : LockedActive,
                    capturedDoctrine.ProgressRatio,
                    capturedDoctrine.IsActive ? "LIVE" : null));
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
            items[items.Count - 1].CaptionFactory = () => LocalizedText.GetOrDefault("tablet.common.back", "Back");
            items[items.Count - 1].DetailFactory = () => LocalizedText.GetOrDefault("tablet.successes.backHub", "Return to the company hub.");

            return new TabletShellPage
            {
                Title = LocalizedText.GetOrDefault("tablet.successes.title", "Successes"),
                Subtitle = LocalizedText.FormatOrDefault(
                    "tablet.successes.subtitle",
                    "{0}/{1} unlocked | Prestige {2:0} | HQ {3}",
                    unlockedCount,
                    totalCount,
                    endgame.PrestigeScore,
                    endgame.HasLandmarkHeadquarters
                        ? LocalizedText.GetOrDefault("tablet.common.online", "Online")
                        : LocalizedText.GetOrDefault("tablet.common.offline", "Offline")),
                HeaderRightText = string.Format("{0}/{1}", unlockedCount, totalCount),
                FooterText = LocalizedText.GetOrDefault("tablet.successes.footer", "Arrow Keys Navigate | Enter Select | Backspace/Esc Back"),
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 7,
                Layout = SimpleMenuTabletLayout.List,
                Items = items,
            };
        }

        private TabletShellPage BuildDoctrinePage(TabletShellContext context, TabletRoute route)
        {
            var doctrines = _tracker != null
                ? _tracker.GetDoctrineStatuses()
                : Array.Empty<CompanyDoctrineStatus>();
            var endgame = _tracker != null
                ? _tracker.GetEndgameSummary()
                : new CompanyEndgameSummary();
            var doctrine = route != null && route.Payload is CompanyDoctrine
                ? (CompanyDoctrine)route.Payload
                : ResolveFocusDoctrine(endgame, doctrines);
            var status = doctrines.FirstOrDefault(entry => entry != null && entry.Doctrine == doctrine);
            var items = new List<MenuItem>();

            if (status == null)
            {
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.successes.status", "Status"),
                    LocalizedText.GetOrDefault("tablet.successes.doctrineUnavailable", "Doctrine state unavailable.")));
            }
            else
            {
                items.Add(TabletUiHelpers.CreateInfoItem(LocalizedText.GetOrDefault("tablet.successes.status", "Status"), TabletEndgameStatusFormatter.BuildDoctrineDetail(status), status.ProgressRatio));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.successes.doctrineEffect", "Doctrine Effect"),
                    TabletEndgameStatusFormatter.BuildDoctrineEffectsDetail(status),
                    status.Tier > 0 ? (float?)Math.Min(1f, Math.Max(status.EffectiveTier, status.Tier) / 4f) : 0f));
                items.Add(TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.successes.leadRule", "Lead Rule"),
                    endgame.DoctrineLead != null && !string.IsNullOrWhiteSpace(endgame.DoctrineLead.TieBreakSummary)
                        ? endgame.DoctrineLead.TieBreakSummary
                        : LocalizedText.GetOrDefault("tablet.successes.leadRuleFallback", "Lead order: tier, then progress, then Industrial > Territorial > Service.")));

                foreach (var component in status.ProgressComponents ?? Array.Empty<CompanyDoctrineProgressComponent>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var capturedComponent = component;
                    items.Add(TabletUiHelpers.CreateInfoItem(
                        capturedComponent.Label ?? string.Empty,
                        TabletEndgameStatusFormatter.BuildDoctrineComponentDetail(capturedComponent),
                        capturedComponent.CompletionRatio));
                }
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(
                LocalizedText.GetOrDefault("tablet.common.back", "Back"),
                LocalizedText.GetOrDefault("tablet.successes.backOverview", "Return to the Successes overview."),
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
                Title = status != null ? status.Name ?? LocalizedText.GetOrDefault("tablet.successes.doctrineTitle", "Doctrine") : LocalizedText.GetOrDefault("tablet.successes.doctrineTitle", "Doctrine"),
                Subtitle = status != null
                    ? LocalizedText.FormatOrDefault(
                        "tablet.successes.doctrineSubtitle",
                        "{0} | Progress {1:0}%",
                        status.IsActive
                            ? LocalizedText.GetOrDefault("tablet.successes.liveDoctrine", "Live doctrine")
                            : LocalizedText.GetOrDefault("tablet.successes.doctrineTrack", "Doctrine track"),
                        Math.Max(0f, status.ProgressRatio) * 100f)
                    : LocalizedText.GetOrDefault("tablet.successes.doctrineUnavailable", "Doctrine state unavailable"),
                HeaderRightText = status != null ? string.Format("{0:0}%", Math.Max(0f, status.ProgressRatio) * 100f) : string.Empty,
                FooterText = LocalizedText.GetOrDefault("tablet.successes.footer", "Arrow Keys Navigate | Enter Select | Backspace/Esc Back"),
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 6,
                Layout = SimpleMenuTabletLayout.List,
                Items = items,
            };
        }

        private TabletShellPage BuildPrestigePage(TabletShellContext context)
        {
            var endgame = _tracker != null
                ? _tracker.GetEndgameSummary()
                : new CompanyEndgameSummary();
            var breakdown = endgame.PrestigeBreakdown ?? new CompanyPrestigeBreakdown();
            var items = new List<MenuItem>
            {
                TabletUiHelpers.CreateInfoItem(
                    LocalizedText.GetOrDefault("tablet.successes.prestigeTotal", "Prestige Total"),
                    TabletEndgameStatusFormatter.BuildPrestigeBreakdownDetail(endgame),
                    endgame.PrestigeScore > 0.001f ? (float?)(endgame.PrestigeScore / 100f) : 0f),
            };

            foreach (var component in breakdown.Components ?? Array.Empty<CompanyPrestigeComponent>())
            {
                if (component == null)
                {
                    continue;
                }

                var capturedComponent = component;
                items.Add(TabletUiHelpers.CreateInfoItem(
                    capturedComponent.Label ?? string.Empty,
                    TabletEndgameStatusFormatter.BuildPrestigeComponentDetail(capturedComponent),
                    capturedComponent.MaxScore > 0.0005f ? (float?)(capturedComponent.Score / capturedComponent.MaxScore) : 0f));
            }

            items.Add(TabletUiHelpers.CreateNavigationItem(
                LocalizedText.GetOrDefault("tablet.common.back", "Back"),
                LocalizedText.GetOrDefault("tablet.successes.backOverview", "Return to the Successes overview."),
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
                Title = LocalizedText.GetOrDefault("tablet.successes.prestige", "Prestige"),
                Subtitle = LocalizedText.FormatOrDefault(
                    "tablet.successes.prestigeSubtitle",
                    "Current {0:0}/100 | Peak {1:0}",
                    Math.Max(0f, endgame.PrestigeScore),
                    Math.Max(Math.Max(0f, endgame.PrestigeScore), Math.Max(0f, endgame.HighestPrestigeScore))),
                HeaderRightText = string.Format("{0:0}", Math.Max(0f, endgame.PrestigeScore)),
                FooterText = LocalizedText.GetOrDefault("tablet.successes.footer", "Arrow Keys Navigate | Enter Select | Backspace/Esc Back"),
                WidthScale = 0.94f,
                CaptionScale = 0.44f,
                DetailScale = 0.27f,
                MaxVisibleItems = 6,
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
                return LocalizedText.FormatOrDefault("tablet.successes.detailUnlocked", "{0}\nUnlocked", status.Description ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(status.ProgressText))
            {
                return status.Description ?? string.Empty;
            }

            return LocalizedText.FormatOrDefault("tablet.successes.detailProgress", "{0}\nProgress: {1}", status.Description ?? string.Empty, status.ProgressText);
        }

        private static CompanyDoctrine ResolveFocusDoctrine(CompanyEndgameSummary endgame, IReadOnlyList<CompanyDoctrineStatus> doctrines)
        {
            endgame = endgame ?? new CompanyEndgameSummary();
            doctrines = doctrines ?? Array.Empty<CompanyDoctrineStatus>();
            if (endgame.ActiveDoctrineTier > 0)
            {
                return endgame.ActiveDoctrine;
            }

            if (endgame.DoctrineLead != null && endgame.DoctrineLead.LeadingDoctrine != CompanyDoctrine.Balanced)
            {
                return endgame.DoctrineLead.LeadingDoctrine;
            }

            var leader = doctrines
                .Where(status => status != null)
                .OrderByDescending(status => status.ProgressRatio)
                .ThenByDescending(status => status.Tier)
                .FirstOrDefault();
            return leader != null ? leader.Doctrine : CompanyDoctrine.Balanced;
        }
    }
}