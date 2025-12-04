using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Balbasztro; // ensure Game, Card classes are in this namespace

namespace BalatroWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Game game;
        private List<int> selectedIndexes = new List<int>();
        private List<Balbasztro.Card> lastPlayedCards = new List<Balbasztro.Card>();
        private int roundStartScore = 0;


        public MainWindow()
        {
            InitializeComponent();

            // wire up buttons
            PlayHandButton.Click += PlayHandButton_Click;
            DiscardButton.Click += DiscardButton_Click;

            // start game
            game = new Game();
            roundStartScore = game.CurrentScore;
            RefreshUI();
        }

        // refresh all UI elements
        private void RefreshUI()
        {
            // Title mapping: 1 -> Small, 2 -> Big, 3 -> Boss (use modulo)
            string title;
            switch (game.RoundNumber % 3)
            {
                case 1:
                    title = "Small Blind";
                    break;
                case 2:
                    title = "Big Blind";
                    break;
                case 0:
                    title = "Boss Blind";
                    break;
                default:
                    title = "Blind";
                    break;
            }
            TitleText.Text = title;

            BlindRequirementText.Text = game.BlindRequirement.ToString("N0");
            RoundScoreText.Text = (game.CurrentScore - roundStartScore).ToString("N0");

            FlushTypeText.Text = string.IsNullOrEmpty(FlushTypeText.Text) ? "Flush" : FlushTypeText.Text;
            // FlushNumberText and FlushMultText are updated when playing a hand

            HandsText.Text = $"Hands {game.PlaysLeft}";
            DiscardsText.Text = $"Discards {game.DiscardsLeft}";

            AnteText.Text = $"Ante {game.Ante}";
            RoundText.Text = $"Round {game.RoundNumber}";

            MoneyText.Text = $"{game.Money} $";

            RenderPlayerHand();
            RenderBoard();
        }

        // create UI for the player's hand (bottom). click toggles selection.
        private void RenderPlayerHand()
        {
            PlayerHandPanel.Children.Clear();
            for (int i = 0; i < game.PlayerHand.Count; i++)
            {
                var card = game.PlayerHand[i];
                var ctrl = CreateCardControl(card, isBottom: true);
                ctrl.Tag = i; // current index in hand
                ctrl.MouseLeftButtonUp += PlayerCard_MouseLeftButtonUp;

                // visual state if selected
                if (selectedIndexes.Contains(i))
                {
                    ctrl.RenderTransform = new TranslateTransform(0, -14);
                    ctrl.BorderBrush = Brushes.Gold;
                    ctrl.BorderThickness = new Thickness(3);
                }
                else
                {
                    ctrl.RenderTransform = null;
                    ctrl.BorderBrush = Brushes.Transparent;
                    ctrl.BorderThickness = new Thickness(0);
                }

                PlayerHandPanel.Children.Add(ctrl);
            }
        }

        // show last played cards on top board
        private void RenderBoard()
        {
            BoardPanel.Children.Clear();

            // if there are lastPlayedCards, render them; otherwise empty
            if (lastPlayedCards != null && lastPlayedCards.Count > 0)
            {
                foreach (var c in lastPlayedCards)
                {
                    var ctrl = CreateCardControl(c, isBottom: false);
                    BoardPanel.Children.Add(ctrl);
                }
            }
            else
            {
                // optionally render placeholders or leave empty
            }
        }

        // create a Border+TextBlock for a Card
        private Border CreateCardControl(Balbasztro.Card card, bool isBottom)
        {
            var styleKey = isBottom ? "PokerCardBottomStyle" : "PokerCardStyle";
            var br = new Border
            {
                Style = (Style)FindResource(styleKey),
                Padding = new Thickness(6),
                Cursor = isBottom ? Cursors.Hand : Cursors.Arrow
            };

            // determine suit glyph and color
            string glyph;
            switch (card.Suit)
            {
                case "Hearts":
                    glyph = "♥";
                    break;
                case "Diamonds":
                    glyph = "♦";
                    break;
                case "Clubs":
                    glyph = "♣";
                    break;
                case "Spades":
                    glyph = "♠";
                    break;
                default:
                    glyph = "?";
                    break;
            }
            var suitColor = (card.Suit == "Hearts" || card.Suit == "Diamonds") ? Brushes.DarkRed : Brushes.Black;

            // rank text: royals show full name beside suit, others show numeric or rank
            bool isRoyal = card.Rank == "Jack" || card.Rank == "Queen" || card.Rank == "King" || card.Rank == "Ace";
            string display;
            if (isRoyal)
            {
                // show e.g. "King ♥" or "Ace ♠"
                display = $"{card.Rank} {glyph}";
            }
            else
            {
                // numeric ranks ("10","9"...)
                display = $"{card.Rank}{glyph}";
            }

            var tb = new TextBlock
            {
                Text = display,
                FontSize = isBottom ? 26 : 26,
                FontWeight = FontWeights.Bold,
                Foreground = suitColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            br.Child = tb;
            return br;
        }

        // toggle selection on player card click
        private void PlayerCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is int idx)
            {
                // if idx out of range (hand changed) refresh and exit
                if (idx < 0 || idx >= game.PlayerHand.Count)
                {
                    RefreshUI();
                    return;
                }

                if (selectedIndexes.Contains(idx))
                {
                    selectedIndexes.Remove(idx);
                }
                else
                {
                    if (selectedIndexes.Count >= 5)
                    {
                        // ignore extra selections
                        return;
                    }
                    selectedIndexes.Add(idx);
                }

                // re-render player hand to reflect selection visuals
                RenderPlayerHand();
            }
        }

        // Play selected cards
        private void PlayHandButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedIndexes == null || selectedIndexes.Count == 0)
            {
                MessageBox.Show("No cards selected to play. Select up to 5 cards.");
                return;
            }

            // validate selection using game.ValidateSelection
            int[] indexes = selectedIndexes.ToArray();
            var validation = game.ValidateSelection(indexes);
            if (!validation.isValid)
            {
                MessageBox.Show($"Invalid selection: {validation.error}");
                selectedIndexes.Clear();
                RefreshUI();
                return;
            }

            if (game.PlaysLeft <= 0)
            {
                MessageBox.Show("No plays left this round.");
                return;
            }

            // capture card objects before they are removed
            var selectedCards = game.SelectCards(indexes);

            try
            {
                var result = game.PlayCards(indexes);
                // store for board rendering
                lastPlayedCards = selectedCards;

                // update flush box (played hand details)
                FlushTypeText.Text = CultureInfoAwareCap(result.handType);
                FlushNumberText.Text = result.gained.ToString();
                FlushMultText.Text = $"x{result.baseMult}";

                // reset selection
                selectedIndexes.Clear();

                // if beat blind -> start new round (follow console logic)
                if (result.beatBlind)
                {
                    // automatically start next round
                    game.StartNewRound();
                    roundStartScore = game.CurrentScore;
                }

                // update UI after changes
                RefreshUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing hand: {ex.Message}");
            }
        }

        // Discard selected cards
        private void DiscardButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedIndexes == null || selectedIndexes.Count == 0)
            {
                MessageBox.Show("No cards selected to discard. Select up to 5 cards.");
                return;
            }

            int[] indexes = selectedIndexes.ToArray();
            var validation = game.ValidateSelection(indexes);
            if (!validation.isValid)
            {
                MessageBox.Show($"Invalid selection: {validation.error}");
                selectedIndexes.Clear();
                RefreshUI();
                return;
            }

            if (game.DiscardsLeft <= 0)
            {
                MessageBox.Show("No discards left this round.");
                return;
            }

            try
            {
                game.DiscardCards(indexes);
                selectedIndexes.Clear();
                RefreshUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error discarding: {ex.Message}");
            }
        }

        // helper: capitalize first char for nicer UI (e.g. "royal flush" -> "Royal flush")
        private string CultureInfoAwareCap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
