using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using WinForms = System.Windows.Forms;
using OfficeMenuItem = LSOL.UI.MenuItem;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private const float BankInteractionDistance = 3.8f;
        private const float MinimumBankLoanAmountStep = 25000f;

        private LemonMenu _bankMenu;
        private BankDefinition _menuBank;
        private int _selectedBankLoanAmountIndex;
        private int _selectedBankComparisonLoanAmountIndex;
        private int _selectedBankLoanTermIndex;
        private bool _bankMenuShowingComparison;
        private bool _bankMenuShowingHistory;

        private void InitializeBankingMenus()
        {
            _bankMenu = new LemonMenu("Bank")
            {
                Subtitle = "Arrange company financing in person",
                AlignRight = true,
                MaxVisibleItems = 12,
                Theme = LemonMenuTheme.Default,
            };
            _selectedBankLoanAmountIndex = 0;
            _selectedBankComparisonLoanAmountIndex = 0;
            _selectedBankLoanTermIndex = 0;
            _bankMenuShowingComparison = false;
            _bankMenuShowingHistory = false;
        }

        private void DrawBankMenu()
        {
            if (_bankMenu != null)
            {
                _bankMenu.Draw();
            }
        }

        private void CloseBankMenu()
        {
            _bankMenuShowingComparison = false;
            _bankMenuShowingHistory = false;
            if (_bankMenu != null)
            {
                _bankMenu.Close();
            }
        }

        private bool HandleBankMenuKey(WinForms.Keys key)
        {
            if (_bankMenu == null || !_bankMenu.IsOpen)
            {
                return false;
            }

            if ((_bankMenuShowingComparison || _bankMenuShowingHistory) && (key == _controls.MenuBack || key == WinForms.Keys.Escape))
            {
                ReturnToBankOfferPage();
                return true;
            }

            _bankMenu.HandleKey(key, _controls);
            return true;
        }

        private void ProcessBankLoanRepayments()
        {
            if (_bankLoanManager == null)
            {
                return;
            }

            var updatedBalance = _profit;
            var messages = _bankLoanManager.ProcessWeeklyRepayments(GetCurrentInGameWeekMinute(), ref updatedBalance);
            if (Math.Abs(updatedBalance - _profit) > 0.01f)
            {
                _profit = updatedBalance;
                _tabletStateStore.MarkAllDirty();
            }

            if (messages.Count > 0)
            {
                ShowStatus(messages[messages.Count - 1], 5000);
                if (_bankMenu != null && _bankMenu.IsOpen)
                {
                    RebuildBankMenuItems();
                }
            }
        }

        private void DrawBankMarkers(Ped player, bool canShowPrompts, ref bool promptShown)
        {
            if (_bankLoanManager == null || player == null || !player.Exists())
            {
                return;
            }

            var playerPosition = player.Position;
            var drawDistanceSq = IndustryMarkerDrawDistance * IndustryMarkerDrawDistance;
            for (int i = 0; i < _bankLoanManager.Banks.Count; i++)
            {
                var bank = _bankLoanManager.Banks[i];
                if (bank == null || playerPosition.DistanceToSquared(bank.Position) > drawDistanceSq)
                {
                    continue;
                }

                var hasActiveLoan = _bankLoanManager.HasActiveLoan;
                World.DrawMarker(
                    MarkerType.Cylinder,
                    bank.Position,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(_config.MarkerRadius * 1.25f, _config.MarkerRadius * 1.25f, _config.MarkerHeight),
                    hasActiveLoan ? Color.FromArgb(205, 70, 156, 102) : Color.FromArgb(205, 76, 148, 216),
                    false,
                    false,
                    false,
                    null,
                    null,
                    false);

                if (canShowPrompts && !promptShown && playerPosition.DistanceTo(bank.Position) <= BankInteractionDistance)
                {
                    Screen.ShowHelpTextThisFrame(PrefixMessage(string.Format(
                        "Press {0} to review financing at {1}.",
                        KeyName(_controls.Interact),
                        bank.DisplayName)));
                    promptShown = true;
                }
            }
        }

        private bool HandleBankInteraction(Ped player)
        {
            var bank = GetBankInInteractionRange(player != null ? player.Position : Vector3.Zero);
            if (bank == null)
            {
                return false;
            }

            OpenBankMenuFor(bank);
            return true;
        }

        private BankDefinition GetBankInInteractionRange(Vector3 playerPosition)
        {
            if (_bankLoanManager == null)
            {
                return null;
            }

            for (int i = 0; i < _bankLoanManager.Banks.Count; i++)
            {
                var bank = _bankLoanManager.Banks[i];
                if (bank != null && playerPosition.DistanceTo(bank.Position) <= BankInteractionDistance)
                {
                    return bank;
                }
            }

            return null;
        }

        private void OpenBankMenuFor(BankDefinition bank)
        {
            if (bank == null)
            {
                ShowStatus("No bank is in range.");
                return;
            }

            _menuBank = bank;
            _bankMenuShowingComparison = false;
            _bankMenuShowingHistory = false;
            CloseAllMenus();
            RebuildBankMenuItems();
            _bankMenu.Open();
        }

        private void RebuildBankMenuItems()
        {
            if (_bankMenu == null)
            {
                return;
            }

            if (_menuBank == null)
            {
                _menuBank = _bankLoanManager != null ? _bankLoanManager.Banks.FirstOrDefault() : null;
            }

            _bankMenu.Title = _menuBank != null ? BuildBankDisplayLabel(_menuBank) : "Bank";
            _bankMenu.Subtitle = _bankLoanManager != null && _bankLoanManager.HasActiveLoan
                ? (_bankMenuShowingHistory ? "Recent weekly branch offers" : "Active company loan")
                : (_bankMenuShowingComparison
                    ? "Weekly comparison across all branches"
                    : (_bankMenuShowingHistory ? "Recent weekly branch offers" : "Weekly company financing offer"));

            var items = new List<OfficeMenuItem>();
            if (_menuBank == null || _bankLoanManager == null)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No bank data loaded",
                    DetailFactory = () => string.Format("Check {0} for valid bank definitions.", RuntimeLayoutResolver.BuildPreferredConfigFileDisplayPath("Banks.xml")),
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = CloseBankMenu,
                });
                _bankMenu.SetItems(items);
                return;
            }

            if (_bankMenuShowingComparison)
            {
                RebuildBankComparisonMenuItems(items);
                _bankMenu.SetItems(items);
                return;
            }

            if (_bankMenuShowingHistory)
            {
                RebuildBankOfferHistoryMenuItems(items);
                _bankMenu.SetItems(items);
                return;
            }

            if (_bankLoanManager.HasActiveLoan)
            {
                var activeLoan = _bankLoanManager.ActiveLoan;
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Origin bank",
                    DetailFactory = () => ResolveActiveLoanBankDisplayLabel(activeLoan),
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Original principal",
                    DetailFactory = () => activeLoan != null ? ModFormatting.FormatMoney(activeLoan.OriginalPrincipal) : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Locked interest rate",
                    DetailFactory = () => activeLoan != null ? ModFormatting.FormatPercent(activeLoan.LockedInterestRatePercent) : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Total repayment",
                    DetailFactory = () => activeLoan != null ? ModFormatting.FormatMoney(activeLoan.TotalRepayment) : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Remaining balance",
                    DetailFactory = () => activeLoan != null ? ModFormatting.FormatMoney(activeLoan.RemainingBalance) : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Weekly installment",
                    DetailFactory = () => activeLoan != null ? ModFormatting.FormatMoney(activeLoan.WeeklyInstallment) : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Weeks remaining",
                    DetailFactory = () => activeLoan != null ? activeLoan.WeeksRemaining.ToString() : string.Empty,
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Next due week",
                    DetailFactory = () => activeLoan != null ? FormatWeekLabel(activeLoan.NextDueWeekIndex) : string.Empty,
                });
                AddBankCreditStandingRows(items);
                AddBankOfferHistoryNavigationRow(items);
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    DetailFactory = () => "A company can only carry one active bank loan at a time.",
                    OnActivate = CloseBankMenu,
                });
                _bankMenu.SetItems(items);
                return;
            }

            var amountOptions = GetCurrentBankLoanAmountOptions(_menuBank);
            EnsureBankLoanSelectionIndices(amountOptions.Count);

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Current offer rate",
                DetailFactory = CurrentBankOfferDetail,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Maximum available",
                DetailFactory = () => ModFormatting.FormatMoney(_menuBank.LoanAmountMaxLimit),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Company balance",
                DetailFactory = () => ModFormatting.FormatMoney(_profit),
            });
            AddBankCreditStandingRows(items);
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Loan amount",
                DetailFactory = CurrentBankLoanAmountDetail,
                IconLabelFactory = () => string.Format("< {0} >", ModFormatting.FormatMoney(GetSelectedBankLoanAmount())),
                OnLeft = () => ChangeBankLoanAmount(-1),
                OnRight = () => ChangeBankLoanAmount(1),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Repayment term",
                DetailFactory = CurrentBankLoanTermDetail,
                IconLabelFactory = () => string.Format("< {0}w >", GetSelectedBankLoanTermWeeks()),
                OnLeft = () => ChangeBankLoanTerm(-1),
                OnRight = () => ChangeBankLoanTerm(1),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Compare weekly offers",
                DetailFactory = () => "Open a branch-by-branch rate, installment, total, and cap view for a chosen amount and term.",
                OnActivate = OpenBankComparisonPage,
            });
            AddBankOfferHistoryNavigationRow(items);
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Preview principal",
                DetailFactory = () => FormatPreviewValue(preview => preview.Principal),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Preview locked rate",
                DetailFactory = () => FormatPreviewRate(),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Preview total",
                DetailFactory = () => FormatPreviewValue(preview => preview.TotalRepayment),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Preview installment",
                DetailFactory = () => FormatPreviewValue(preview => preview.WeeklyInstallment),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Preview term",
                DetailFactory = () => string.Format("{0} weeks", GetSelectedBankLoanTermWeeks()),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Confirm loan",
                DetailFactory = () => "Send the payout to the LSOL company balance and lock this week’s rate.",
                OnActivate = ConfirmBankLoan,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Close",
                DetailFactory = () => "Return to the world marker.",
                OnActivate = CloseBankMenu,
            });

            _bankMenu.SetItems(items);
        }

        private void RebuildBankComparisonMenuItems(List<OfficeMenuItem> items)
        {
            var amountOptions = GetBankComparisonLoanAmountOptions();
            EnsureBankComparisonSelectionIndices(amountOptions.Count);
            var requestedAmount = GetSelectedBankComparisonLoanAmount();
            var termWeeks = GetSelectedBankLoanTermWeeks();
            var currentMinute = GetCurrentInGameWeekMinute();

            _bankMenu.Title = "Compare Offers";
            _bankMenu.Subtitle = string.Format("{0} requested over {1} weeks", ModFormatting.FormatMoney(requestedAmount), termWeeks);

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Current branch",
                DetailFactory = () => string.Format("Comparing against {0}.", BuildBankDisplayLabel(_menuBank)),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Compare amount",
                DetailFactory = () => string.Format("{0} requested across all banks. Rows marked capped show each branch maximum.", ModFormatting.FormatMoney(requestedAmount)),
                IconLabelFactory = () => string.Format("< {0} >", ModFormatting.FormatMoney(requestedAmount)),
                OnLeft = () => ChangeBankComparisonLoanAmount(-1),
                OnRight = () => ChangeBankComparisonLoanAmount(1),
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Repayment term",
                DetailFactory = () => string.Format("{0} weeks. Weekly installment previews refresh for every branch below.", termWeeks),
                IconLabelFactory = () => string.Format("< {0}w >", termWeeks),
                OnLeft = () => ChangeBankLoanTerm(-1),
                OnRight = () => ChangeBankLoanTerm(1),
            });

            var displayNameCounts = _bankLoanManager.Banks
                .Where(bank => bank != null)
                .GroupBy(bank => bank.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            var orderedBanks = _bankLoanManager.Banks
                .Where(bank => bank != null)
                .OrderByDescending(bank => _menuBank != null && string.Equals(bank.BankId, _menuBank.BankId, StringComparison.OrdinalIgnoreCase))
                .ThenBy(bank => BankOfferComparisonFormatter.BuildBranchLabel(
                    bank.DisplayName,
                    bank.BankId,
                    displayNameCounts.ContainsKey(bank.DisplayName ?? string.Empty) && displayNameCounts[bank.DisplayName ?? string.Empty] > 1),
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < orderedBanks.Count; i++)
            {
                var bank = orderedBanks[i];
                var preview = _bankLoanManager.CreatePreview(bank, requestedAmount, termWeeks, currentMinute);
                var key = bank.DisplayName ?? string.Empty;
                var row = new BankOfferComparisonRow
                {
                    BankId = bank.BankId,
                    DisplayName = bank.DisplayName,
                    NameIsDuplicated = displayNameCounts.ContainsKey(key) && displayNameCounts[key] > 1,
                    IsCurrentBranch = _menuBank != null && string.Equals(bank.BankId, _menuBank.BankId, StringComparison.OrdinalIgnoreCase),
                    RequestedPrincipal = requestedAmount,
                    MaxAvailablePrincipal = Math.Max(0f, bank.LoanAmountMaxLimit),
                    OfferedRatePercent = _bankLoanManager.GetCurrentOfferRate(bank, currentMinute),
                    PreviewPrincipal = preview != null ? Math.Max(0f, preview.Principal) : 0f,
                    PreviewTotalRepayment = preview != null ? Math.Max(0f, preview.TotalRepayment) : 0f,
                    PreviewWeeklyInstallment = preview != null ? Math.Max(0f, preview.WeeklyInstallment) : 0f,
                };

                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => BankOfferComparisonFormatter.BuildCaption(row),
                    DetailFactory = () => BankOfferComparisonFormatter.BuildDetail(row),
                });
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                DetailFactory = () => "Return to the current branch loan preview and confirmation page.",
                OnActivate = ReturnToBankOfferPage,
            });
        }

        private void RebuildBankOfferHistoryMenuItems(List<OfficeMenuItem> items)
        {
            var currentWeekIndex = BankLoanManager.GetWeekIndex(GetCurrentInGameWeekMinute());
            var history = _bankLoanManager != null && _menuBank != null
                ? _bankLoanManager.GetOfferHistory(_menuBank.BankId).ToList()
                : new List<BankOfferRateSnapshot>();

            _bankMenu.Title = "Offer History";
            _bankMenu.Subtitle = BuildBankDisplayLabel(_menuBank);

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Current branch",
                DetailFactory = () => string.Format("Recent weekly offers for {0}.", BuildBankDisplayLabel(_menuBank)),
            });

            if (history.Count == 0)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No offer history yet",
                    DetailFactory = () => "This branch starts tracking once a weekly rate is first resolved.",
                });
            }
            else
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Recent range",
                    DetailFactory = () => BuildBankOfferHistoryRangeDetail(history),
                });

                var previousOffer = history.Skip(1).FirstOrDefault();
                if (previousOffer != null)
                {
                    var currentOffer = history[0];
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => "Delta vs previous",
                        DetailFactory = () => string.Format(
                            "{0} against {1}.",
                            FormatSignedRateDelta(currentOffer.RatePercent - previousOffer.RatePercent),
                            FormatWeekLabel(previousOffer.WeekIndex)),
                    });
                }

                for (int i = 0; i < history.Count; i++)
                {
                    var index = i;
                    items.Add(new OfficeMenuItem
                    {
                        CaptionFactory = () => BuildBankOfferHistoryCaption(history[index], currentWeekIndex),
                        DetailFactory = () => BuildBankOfferHistoryDetail(history, index),
                    });
                }
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Back",
                DetailFactory = () => "Return to the current branch bank page.",
                OnActivate = ReturnToBankOfferPage,
            });
        }

        private void OpenBankComparisonPage()
        {
            _bankMenuShowingComparison = true;
            _bankMenuShowingHistory = false;
            SyncBankComparisonAmountSelection(GetSelectedBankLoanAmount());
            RebuildBankMenuItems();
        }

        private void OpenBankHistoryPage()
        {
            _bankMenuShowingComparison = false;
            _bankMenuShowingHistory = true;
            RebuildBankMenuItems();
        }

        private void ReturnToBankOfferPage()
        {
            _bankMenuShowingComparison = false;
            _bankMenuShowingHistory = false;
            RebuildBankMenuItems();
        }

        private void AddBankCreditStandingRows(List<OfficeMenuItem> items)
        {
            if (items == null)
            {
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Credit standing",
                DetailFactory = CurrentCompanyCreditStandingDetail,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Standing drivers",
                DetailFactory = CurrentCompanyCreditStandingSummary,
            });
            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Future offer posture",
                DetailFactory = CurrentCompanyCreditStandingEffect,
            });
        }

        private void AddBankOfferHistoryNavigationRow(List<OfficeMenuItem> items)
        {
            if (items == null)
            {
                return;
            }

            items.Add(new OfficeMenuItem
            {
                CaptionFactory = () => "Offer history",
                DetailFactory = CurrentBankOfferHistoryDetail,
                OnActivate = OpenBankHistoryPage,
            });
        }

        private void ConfirmBankLoan()
        {
            if (_bankLoanManager == null || _menuBank == null)
            {
                ShowStatus("No bank offer is available.");
                return;
            }

            var amount = GetSelectedBankLoanAmount();
            var termWeeks = GetSelectedBankLoanTermWeeks();
            var updatedBalance = _profit;
            string message;
            if (!_bankLoanManager.TryTakeLoan(_menuBank, amount, termWeeks, GetCurrentInGameWeekMinute(), ref updatedBalance, out message))
            {
                ShowStatus(message);
                return;
            }

            _profit = updatedBalance;
            SyncPlayerSuccessBalance();
            _tabletStateStore.MarkAllDirty();
            RebuildBankMenuItems();
            ShowStatus(message, 5000);
        }

        private void ChangeBankLoanAmount(int delta)
        {
            var options = GetCurrentBankLoanAmountOptions(_menuBank);
            if (options.Count == 0)
            {
                _selectedBankLoanAmountIndex = 0;
                return;
            }

            _selectedBankLoanAmountIndex = WrapSelectionIndex(_selectedBankLoanAmountIndex, delta, options.Count);
        }

        private void ChangeBankComparisonLoanAmount(int delta)
        {
            var options = GetBankComparisonLoanAmountOptions();
            if (options.Count == 0)
            {
                _selectedBankComparisonLoanAmountIndex = 0;
                return;
            }

            _selectedBankComparisonLoanAmountIndex = WrapSelectionIndex(_selectedBankComparisonLoanAmountIndex, delta, options.Count);
        }

        private void ChangeBankLoanTerm(int delta)
        {
            _selectedBankLoanTermIndex = WrapSelectionIndex(_selectedBankLoanTermIndex, delta, BankLoanManager.LoanTerms.Count);
        }

        private string CurrentBankOfferDetail()
        {
            if (_bankLoanManager == null || _menuBank == null)
            {
                return string.Empty;
            }

            var currentMinute = GetCurrentInGameWeekMinute();
            return string.Format(
                "{0} for {1}",
                ModFormatting.FormatPercent(_bankLoanManager.GetCurrentOfferRate(_menuBank, currentMinute)),
                FormatWeekLabel(BankLoanManager.GetWeekIndex(currentMinute)));
        }

        private string CurrentCompanyCreditStandingDetail()
        {
            var standing = GetCurrentCompanyCreditStanding();
            return standing == null
                ? string.Empty
                : string.Format("{0} | Score {1}", standing.Label, standing.Score);
        }

        private string CurrentCompanyCreditStandingSummary()
        {
            var standing = GetCurrentCompanyCreditStanding();
            return standing != null ? standing.Summary : string.Empty;
        }

        private string CurrentCompanyCreditStandingEffect()
        {
            var standing = GetCurrentCompanyCreditStanding();
            return standing != null ? standing.RateEffectLabel : string.Empty;
        }

        private string CurrentBankOfferHistoryDetail()
        {
            if (_bankLoanManager == null || _menuBank == null)
            {
                return string.Empty;
            }

            var history = _bankLoanManager.GetOfferHistory(_menuBank.BankId);
            return history.Count > 0
                ? string.Format("Review the last {0} locked weekly offer(s) for this branch.", history.Count)
                : "Review the recent weekly rates that have locked for this branch.";
        }

        private string CurrentBankLoanAmountDetail()
        {
            var selectedAmount = GetSelectedBankLoanAmount();
            if (selectedAmount <= 0.01f)
            {
                return "No valid amount options.";
            }

            return string.Format(
                "{0} selected | max {1}",
                ModFormatting.FormatMoney(selectedAmount),
                ModFormatting.FormatMoney(_menuBank != null ? _menuBank.LoanAmountMaxLimit : 0f));
        }

        private string CurrentBankLoanTermDetail()
        {
            return string.Format("{0} weeks", GetSelectedBankLoanTermWeeks());
        }

        private string FormatPreviewValue(Func<CompanyLoanPreview, float> selector)
        {
            var preview = GetSelectedBankLoanPreview();
            if (preview == null || selector == null)
            {
                return string.Empty;
            }

            return ModFormatting.FormatMoney(selector(preview));
        }

        private string FormatPreviewRate()
        {
            var preview = GetSelectedBankLoanPreview();
            return preview != null ? ModFormatting.FormatPercent(preview.RatePercent) : string.Empty;
        }

        private CompanyLoanPreview GetSelectedBankLoanPreview()
        {
            if (_bankLoanManager == null || _menuBank == null)
            {
                return null;
            }

            return _bankLoanManager.CreatePreview(
                _menuBank,
                GetSelectedBankLoanAmount(),
                GetSelectedBankLoanTermWeeks(),
                GetCurrentInGameWeekMinute());
        }

        private float GetSelectedBankLoanAmount()
        {
            var options = GetCurrentBankLoanAmountOptions(_menuBank);
            if (options.Count == 0)
            {
                return 0f;
            }

            EnsureBankLoanSelectionIndices(options.Count);
            return options[_selectedBankLoanAmountIndex];
        }

        private int GetSelectedBankLoanTermWeeks()
        {
            if (BankLoanManager.LoanTerms.Count == 0)
            {
                return 4;
            }

            if (_selectedBankLoanTermIndex < 0 || _selectedBankLoanTermIndex >= BankLoanManager.LoanTerms.Count)
            {
                _selectedBankLoanTermIndex = 0;
            }

            return BankLoanManager.LoanTerms[_selectedBankLoanTermIndex];
        }

        private float GetSelectedBankComparisonLoanAmount()
        {
            var options = GetBankComparisonLoanAmountOptions();
            if (options.Count == 0)
            {
                return 0f;
            }

            EnsureBankComparisonSelectionIndices(options.Count);
            return options[_selectedBankComparisonLoanAmountIndex];
        }

        private List<float> GetCurrentBankLoanAmountOptions(BankDefinition bank)
        {
            return BuildBankLoanAmountOptions(bank != null ? bank.LoanAmountMaxLimit : 0f);
        }

        private List<float> GetBankComparisonLoanAmountOptions()
        {
            var maxLoanLimit = _bankLoanManager != null && _bankLoanManager.Banks != null
                ? _bankLoanManager.Banks.Where(bank => bank != null).Select(bank => bank.LoanAmountMaxLimit).DefaultIfEmpty(0f).Max()
                : 0f;
            return BuildBankLoanAmountOptions(maxLoanLimit);
        }

        private List<float> BuildBankLoanAmountOptions(float maxLoanLimit)
        {
            var options = new List<float>();
            if (maxLoanLimit <= 0.01f)
            {
                return options;
            }

            var maxLoan = Math.Max(MinimumBankLoanAmountStep, maxLoanLimit);
            var rawStep = Math.Max(MinimumBankLoanAmountStep, maxLoan / 20f);
            var snappedStep = Math.Max(MinimumBankLoanAmountStep, (float)Math.Round(rawStep / MinimumBankLoanAmountStep) * MinimumBankLoanAmountStep);
            var amount = snappedStep;
            while (amount < maxLoan - 0.01f)
            {
                options.Add(amount);
                amount += snappedStep;
            }

            options.Add(maxLoanLimit);
            return options
                .Where(value => value > 0.01f)
                .Distinct()
                .OrderBy(value => value)
                .ToList();
        }

        private void EnsureBankLoanSelectionIndices(int amountOptionCount)
        {
            if (amountOptionCount <= 0)
            {
                _selectedBankLoanAmountIndex = 0;
            }
            else if (_selectedBankLoanAmountIndex < 0 || _selectedBankLoanAmountIndex >= amountOptionCount)
            {
                _selectedBankLoanAmountIndex = Math.Max(0, Math.Min(amountOptionCount - 1, amountOptionCount / 2));
            }

            if (_selectedBankLoanTermIndex < 0 || _selectedBankLoanTermIndex >= BankLoanManager.LoanTerms.Count)
            {
                _selectedBankLoanTermIndex = 0;
            }
        }

        private void EnsureBankComparisonSelectionIndices(int amountOptionCount)
        {
            if (amountOptionCount <= 0)
            {
                _selectedBankComparisonLoanAmountIndex = 0;
                return;
            }

            if (_selectedBankComparisonLoanAmountIndex < 0 || _selectedBankComparisonLoanAmountIndex >= amountOptionCount)
            {
                _selectedBankComparisonLoanAmountIndex = Math.Max(0, Math.Min(amountOptionCount - 1, amountOptionCount / 2));
            }
        }

        private void SyncBankComparisonAmountSelection(float requestedAmount)
        {
            var options = GetBankComparisonLoanAmountOptions();
            if (options.Count == 0)
            {
                _selectedBankComparisonLoanAmountIndex = 0;
                return;
            }

            var bestIndex = 0;
            var bestDistance = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                var distance = Math.Abs(options[i] - requestedAmount);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            _selectedBankComparisonLoanAmountIndex = bestIndex;
        }

        private string BuildBankDisplayLabel(BankDefinition bank)
        {
            return bank == null
                ? string.Empty
                : BankOfferComparisonFormatter.BuildBranchLabel(bank.DisplayName, bank.BankId, ShouldDisambiguateBankLabel(bank.DisplayName));
        }

        private CompanyCreditStanding GetCurrentCompanyCreditStanding()
        {
            return _bankLoanManager != null
                ? _bankLoanManager.GetCreditStanding(GetCurrentInGameWeekMinute())
                : null;
        }

        private static string BuildBankOfferHistoryRangeDetail(IReadOnlyList<BankOfferRateSnapshot> history)
        {
            if (history == null || history.Count == 0)
            {
                return string.Empty;
            }

            var bestRate = history.Min(entry => entry != null ? entry.RatePercent : float.MaxValue);
            var worstRate = history.Max(entry => entry != null ? entry.RatePercent : 0f);
            return string.Format(
                "Best {0} | Worst {1} across {2} week(s).",
                ModFormatting.FormatPercent(bestRate),
                ModFormatting.FormatPercent(worstRate),
                history.Count);
        }

        private static string BuildBankOfferHistoryCaption(BankOfferRateSnapshot offer, int currentWeekIndex)
        {
            if (offer == null)
            {
                return string.Empty;
            }

            return offer.WeekIndex == currentWeekIndex
                ? string.Format("{0} [CURRENT]", FormatWeekLabel(offer.WeekIndex))
                : FormatWeekLabel(offer.WeekIndex);
        }

        private static string BuildBankOfferHistoryDetail(IReadOnlyList<BankOfferRateSnapshot> history, int index)
        {
            if (history == null || index < 0 || index >= history.Count || history[index] == null)
            {
                return string.Empty;
            }

            var offer = history[index];
            var detail = string.Format("Rate {0}", ModFormatting.FormatPercent(offer.RatePercent));
            if (index + 1 >= history.Count || history[index + 1] == null)
            {
                return detail;
            }

            var previousOffer = history[index + 1];
            return string.Format(
                "{0} | vs previous {1}",
                detail,
                FormatSignedRateDelta(offer.RatePercent - previousOffer.RatePercent));
        }

        private static string FormatSignedRateDelta(float value)
        {
            if (Math.Abs(value) <= 0.001f)
            {
                return "0.00%";
            }

            return ModFormatting.FormatSignedPercent(value);
        }

        private string ResolveActiveLoanBankDisplayLabel(CompanyLoanState activeLoan)
        {
            if (activeLoan == null)
            {
                return string.Empty;
            }

            return BankOfferComparisonFormatter.BuildBranchLabel(
                activeLoan.BankName,
                activeLoan.BankId,
                ShouldDisambiguateBankLabel(activeLoan.BankName));
        }

        private bool ShouldDisambiguateBankLabel(string displayName)
        {
            if (_bankLoanManager == null || _bankLoanManager.Banks == null)
            {
                return false;
            }

            var normalizedName = displayName ?? string.Empty;
            return _bankLoanManager.Banks.Count(bank => bank != null && string.Equals(bank.DisplayName ?? string.Empty, normalizedName, StringComparison.OrdinalIgnoreCase)) > 1;
        }

        private static int WrapSelectionIndex(int currentIndex, int delta, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var next = (currentIndex + delta) % count;
            return next < 0 ? next + count : next;
        }

        private static string FormatWeekLabel(int weekIndex)
        {
            return string.Format("Week {0}", Math.Max(0, weekIndex) + 1);
        }
    }
}