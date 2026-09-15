using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DentalSuiteClient
{
    public partial class ComparisonWindow : Window
    {
        private readonly List<CaptureItem> _availableItems;

        public ComparisonWindow(List<CaptureItem> availableItems)
        {
            InitializeComponent();
            _availableItems = availableItems;
        }

        private void OnChooseLeftClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ChooseImageDialog(_availableItems) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                LeftLabel.Text = $"Diente {dialog.SelectedItem.DisplayNumber} — {dialog.SelectedItem.TypeLabel}";
                LeftImage.Source = LoadImage(dialog.SelectedItem.ImagePath);
            }
        }

        private void OnChooseRightClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ChooseImageDialog(_availableItems) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                RightLabel.Text = $"Diente {dialog.SelectedItem.DisplayNumber} — {dialog.SelectedItem.TypeLabel}";
                RightImage.Source = LoadImage(dialog.SelectedItem.ImagePath);
            }
        }

        private static BitmapImage? LoadImage(string? path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return null;

            byte[] fileBytes = System.IO.File.ReadAllBytes(path);
            var bitmap = new BitmapImage();
            using (var stream = new System.IO.MemoryStream(fileBytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }
    }
}
