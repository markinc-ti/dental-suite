using System;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class TreatmentTypesWindow : Window
    {
        private readonly ApiClient _api = new();

        public TreatmentTypesWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                TypesList.ItemsSource = await _api.ListTreatmentTypesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el catálogo: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnNewTypeClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewTreatmentTypeDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.CreateTreatmentTypeAsync(
                        dialog.Name, dialog.Description, dialog.SessionCount, dialog.IntervalDays, dialog.Cost);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo crear el tipo de tratamiento: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
