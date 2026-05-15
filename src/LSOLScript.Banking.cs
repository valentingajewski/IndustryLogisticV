using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
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
        private int _selectedBankLoanTermIndex;

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
            _selectedBankLoanTermIndex = 0;
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
            if (_bankMenu != null)
            {
                _bankMenu.Close();
            }
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

            _bankMenu.Title = _menuBank != null ? _menuBank.DisplayName : "Bank";
            _bankMenu.Subtitle = _bankLoanManager != null && _bankLoanManager.HasActiveLoan
                ? "Active company loan"
                : "Weekly company financing offer";

            var items = new List<OfficeMenuItem>();
            if (_menuBank == null || _bankLoanManager == null)
            {
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "No bank data loaded",
                    DetailFactory = () => "Check LSOL_Config/Banks.xml for valid bank definitions.",
                });
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Close",
                    OnActivate = CloseBankMenu,
                });
                _bankMenu.SetItems(items);
                return;
            }

            if (_bankLoanManager.HasActiveLoan)
            {
                var activeLoan = _bankLoanManager.ActiveLoan;
                items.Add(new OfficeMenuItem
                {
                    CaptionFactory = () => "Origin bank",
                    DetailFactory = () => activeLoan != null ? activeLoan.BankName : string.Empty,
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

        private List<float> GetCurrentBankLoanAmountOptions(BankDefinition bank)
        {
            var options = new List<float>();
            if (bank == null || bank.LoanAmountMaxLimit <= 0.01f)
            {
                return options;
            }

            var maxLoan = Math.Max(MinimumBankLoanAmountStep, bank.LoanAmountMaxLimit);
            var rawStep = Math.Max(MinimumBankLoanAmountStep, maxLoan / 20f);
            var snappedStep = Math.Max(MinimumBankLoanAmountStep, (float)Math.Round(rawStep / MinimumBankLoanAmountStep) * MinimumBankLoanAmountStep);
            var amount = snappedStep;
            while (amount < maxLoan - 0.01f)
            {
                options.Add(amount);
                amount += snappedStep;
            }

            options.Add(bank.LoanAmountMaxLimit);
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