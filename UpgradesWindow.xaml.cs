using System.Collections.Generic;
using System.Windows;

namespace BalatroWPF
{
    public partial class UpgradesWindow : Window
    {
        private List<Balbasztro.Upgrade> choices;

        // Expose which upgrade (if any) was chosen by user
        public Balbasztro.Upgrade SelectedUpgrade { get; private set; }

        // accept choices from Game so UI shows exactly the generated set
        public UpgradesWindow(List<Balbasztro.Upgrade> choicesFromGame)
        {
            InitializeComponent();

            choices = choicesFromGame ?? Balbasztro.Game.Instance.GetChoices(3);
            ChoicesList.ItemsSource = choices;
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            var b = sender as System.Windows.Controls.Button;
            var u = b?.Tag as Balbasztro.Upgrade;
            if (u == null) return;

            var game = Balbasztro.Game.Instance;
            if (game.Money < u.Cost)
            {
                MessageBox.Show(this, "Not enough money to buy this upgrade.", "Insufficient funds", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool ok = game.PurchaseUpgrade(u);
            if (ok)
            {
                // record which upgrade was chosen and close
                SelectedUpgrade = u;
                DialogResult = true;
                Close();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // player chose none -> close (DialogResult=false)
            SelectedUpgrade = null;
            DialogResult = false;
            Close();
        }
    }
}