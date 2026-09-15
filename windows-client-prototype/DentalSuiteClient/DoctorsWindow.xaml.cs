using System;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class DoctorsWindow : Window
    {
        private readonly ApiClient _api = new();

        public DoctorsWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadDoctorsAsync();
        }

        private async System.Threading.Tasks.Task LoadDoctorsAsync()
        {
            try
            {
                var doctors = await _api.ListDoctorsAsync();
                DoctorsList.ItemsSource = doctors;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar la lista de doctores: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnEditDoctorClick(object sender, RoutedEventArgs e)
        {
            if (DoctorsList.SelectedItem is not UserDto doctor)
            {
                MessageBox.Show("Selecciona un doctor de la lista primero.", "Falta selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new EditDoctorDialog(doctor) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.UpdateDoctorAsync(
                        doctor.Id, dialog.FullName, dialog.Phone, dialog.Specialty, dialog.ProfessionalLicense);
                    await LoadDoctorsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo editar el doctor: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnDeleteDoctorClick(object sender, RoutedEventArgs e)
        {
            if (DoctorsList.SelectedItem is not UserDto doctor)
            {
                MessageBox.Show("Selecciona un doctor de la lista primero.", "Falta selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"¿Eliminar a {doctor.FullName}? Sus citas y tratamientos existentes no se borran, "
                + "solo deja de aparecer para asignar cosas nuevas.",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await _api.DeleteDoctorAsync(doctor.Id);
                await LoadDoctorsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo eliminar el doctor: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
            await LoadDoctorsAsync();

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnNewDoctorClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewDoctorDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.CreateDoctorAsync(
                        dialog.FullName, dialog.Username, dialog.Password, dialog.Phone,
                        dialog.Specialty, dialog.ProfessionalLicense);
                    await LoadDoctorsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo crear el doctor: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
