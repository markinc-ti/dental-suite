using System;
using System.Globalization;
using System.Windows;

namespace DentalSuiteClient
{
    public partial class PickDateTimeDialog : Window
    {
        public DateTime SelectedDateTime { get; private set; }
        public string? Notes { get; private set; }

        public PickDateTimeDialog(string header, DateTime initialDate, bool showNotes = false)
        {
            InitializeComponent();
            HeaderText.Text = header;
            DatePickerControl.SelectedDate = initialDate.Date;
            TimeBox.Text = initialDate.ToString("HH:mm");

            if (!showNotes)
            {
                NotesLabel.Visibility = Visibility.Collapsed;
                NotesBox.Visibility = Visibility.Collapsed;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (DatePickerControl.SelectedDate is not DateTime date)
            {
                MessageBox.Show("Selecciona una fecha.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParseExact(TimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time))
            {
                MessageBox.Show("La hora debe tener el formato HH:mm, por ejemplo 10:00.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedDateTime = date.Date + time;
            Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
