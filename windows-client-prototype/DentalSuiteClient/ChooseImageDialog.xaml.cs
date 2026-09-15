using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DentalSuiteClient
{
    public sealed class ChoosableCapture
    {
        public string Label { get; init; } = "";
        public CaptureItem Item { get; init; } = null!;
    }

    public partial class ChooseImageDialog : Window
    {
        public CaptureItem? SelectedItem { get; private set; }

        public ChooseImageDialog(IEnumerable<CaptureItem> capturedItems)
        {
            InitializeComponent();
            TeethList.ItemsSource = capturedItems
                .Where(t => t.IsCaptured)
                .Select(t => new ChoosableCapture { Label = $"Diente {t.DisplayNumber} — {t.TypeLabel}", Item = t })
                .ToList();
        }

        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            if (TeethList.SelectedItem is ChoosableCapture choice)
            {
                SelectedItem = choice.Item;
                DialogResult = true;
            }
        }
    }
}
