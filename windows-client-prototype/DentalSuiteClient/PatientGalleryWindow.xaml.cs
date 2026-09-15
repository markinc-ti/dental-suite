using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class PatientGalleryWindow : Window
    {
        private readonly ApiClient _api = new();
        private readonly string _patientId;
        private readonly string _patientName;

        public PatientGalleryWindow(string patientId, string patientName)
        {
            InitializeComponent();
            _patientId = patientId;
            _patientName = patientName;
            TitleText.Text = $"Radiografías y fotos — {patientName}";
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var studies = await _api.ListPatientStudiesAsync(_patientId);

                var images = new List<PatientImageItem>();
                foreach (var study in studies)
                {
                    foreach (var slot in study.Slots.Where(s => s.IsCaptured && !string.IsNullOrEmpty(s.ImageUrl)))
                    {
                        images.Add(new PatientImageItem
                        {
                            SlotId = slot.Id,
                            ImagePath = slot.ImageUrl!,
                            ToothNumber = slot.ToothNumber,
                            ImageType = slot.ImageType,
                            ImageTypeLabel = slot.ImageType == "photo" ? "Foto" : "Radiografía",
                            CapturedAt = slot.CapturedAt,
                        });
                    }
                }

                // Más reciente primero, y agrupado por diente para que
                // sea fácil comparar la evolución de un mismo diente en
                // el tiempo.
                ImagesList.ItemsSource = images
                    .OrderBy(i => i.ToothNumber)
                    .ThenByDescending(i => i.CapturedAt)
                    .ToList();
                EmptyText.Visibility = images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar las imágenes: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnImageClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: PatientImageItem item })
                return;

            var editor = new ImageAnnotationWindow(
                item.ImagePath, remoteSlotInfo: (item.SlotId, item.ToothNumber, item.ImageType)) { Owner = this };
            editor.ShowDialog();

            if (editor.WasSaved)
                await LoadAsync();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
