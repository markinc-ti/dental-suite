using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    /// <summary>Una entrada de la lista, envolviendo el DTO del servidor
    /// con textos ya listos para mostrar.</summary>
    public sealed class HistoryEntry
    {
        public XRayStudyDto Study { get; init; } = null!;
        public string DateLabel => Study.TakenAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public string SummaryLabel
        {
            get
            {
                int capturados = Study.Slots.Count(s => s.IsCaptured);
                int dientes = Study.Slots.Where(s => s.IsCaptured).Select(s => s.ToothNumber).Distinct().Count();
                return $"{capturados} captura(s) en {dientes} diente(s)";
            }
        }
    }

    public partial class HistoryWindow : Window
    {
        public XRayStudyDto? SelectedStudy { get; private set; }

        public HistoryWindow(string patientName, IEnumerable<XRayStudyDto> studies)
        {
            InitializeComponent();
            TitleText.Text = $"Historial de odontogramas — {patientName}";

            var entries = studies
                .OrderByDescending(s => s.TakenAt)
                .Select(s => new HistoryEntry { Study = s })
                .ToList();

            StudiesList.ItemsSource = entries;
            EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnStudyDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (StudiesList.SelectedItem is HistoryEntry entry)
            {
                SelectedStudy = entry.Study;
                DialogResult = true;
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
