using System.Globalization;
using System.Windows;

namespace DentalSuiteClient
{
    public partial class NewTreatmentTypeDialog : Window
    {
        public new string Name => NameBox.Text.Trim();
        public string? Description => string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        public int SessionCount { get; private set; } = 1;
        public int IntervalDays { get; private set; } = 30;
        public decimal? Cost { get; private set; }

        public NewTreatmentTypeDialog()
        {
            InitializeComponent();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("El nombre es obligatorio.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(SessionCountBox.Text.Trim(), out int sessionCount) || sessionCount < 1)
            {
                MessageBox.Show("El número de sesiones debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(IntervalDaysBox.Text.Trim(), out int intervalDays) || intervalDays < 1)
            {
                MessageBox.Show("El intervalo debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? cost = null;
            if (!string.IsNullOrWhiteSpace(CostBox.Text))
            {
                if (!decimal.TryParse(CostBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCost))
                {
                    MessageBox.Show("El costo no es un número válido.", "Formato inválido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                cost = parsedCost;
            }

            SessionCount = sessionCount;
            IntervalDays = intervalDays;
            Cost = cost;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
