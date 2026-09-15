using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class CalendarWindow : Window
    {
        private readonly ApiClient _api = new();
        private readonly Dictionary<string, string> _patientNameCache = new();
        private readonly Dictionary<string, string> _doctorNameCache = new();
        private readonly CultureInfo _esMx = new("es-MX");

        private DateTime _visibleMonth;
        private DateTime _selectedDay;
        private List<AppointmentDto> _monthAppointments = new();

        private static readonly Brush CellBorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        private static readonly Brush SelectedCellBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xE4, 0xE8));
        private static readonly Brush SelectedCellBorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x2C, 0x4A));
        private static readonly Brush PillBrush = new SolidColorBrush(Color.FromRgb(0xBE, 0xE3, 0xD4));

        public CalendarWindow()
        {
            InitializeComponent();
            _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _selectedDay = DateTime.Today;
            Loaded += async (_, _) => await LoadMonthAsync();
        }

        private async void OnPreviousMonthClick(object sender, RoutedEventArgs e)
        {
            _visibleMonth = _visibleMonth.AddMonths(-1);
            await LoadMonthAsync();
        }

        private async void OnNextMonthClick(object sender, RoutedEventArgs e)
        {
            _visibleMonth = _visibleMonth.AddMonths(1);
            await LoadMonthAsync();
        }

        private async System.Threading.Tasks.Task LoadMonthAsync()
        {
            MonthTitleText.Text = _visibleMonth.ToString("MMMM yyyy", _esMx).ToUpper(_esMx);

            try
            {
                var monthStart = _visibleMonth;
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                _monthAppointments = await _api.ListAppointmentsAsync(monthStart, monthEnd);

                // Resolvemos nombres una vez por mes cargado, no por cita.
                var patients = await _api.SearchPatientsAsync();
                foreach (var p in patients)
                    if (p.Id != null) _patientNameCache[p.Id] = p.FullName;

                var doctors = await _api.ListDoctorsAsync();
                foreach (var d in doctors)
                    _doctorNameCache[d.Id] = d.FullName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el calendario: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                _monthAppointments = new List<AppointmentDto>();
            }

            BuildMonthGrid();
            await LoadSelectedDayAsync();
        }

        private void BuildMonthGrid()
        {
            MonthGrid.Children.Clear();
            MonthGrid.RowDefinitions.Clear();
            MonthGrid.ColumnDefinitions.Clear();

            for (int c = 0; c < 7; c++)
                MonthGrid.ColumnDefinitions.Add(new ColumnDefinition());

            int daysInMonth = DateTime.DaysInMonth(_visibleMonth.Year, _visibleMonth.Month);
            int startWeekday = (int)_visibleMonth.DayOfWeek; // 0 = domingo
            int totalCells = startWeekday + daysInMonth;
            int rows = (int)Math.Ceiling(totalCells / 7.0);

            for (int r = 0; r < rows; r++)
                MonthGrid.RowDefinitions.Add(new RowDefinition());

            var appointmentsByDay = _monthAppointments
                .GroupBy(a => a.StartsAt.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.StartsAt).ToList());

            for (int day = 1; day <= daysInMonth; day++)
            {
                int cellIndex = startWeekday + day - 1;
                int row = cellIndex / 7;
                int col = cellIndex % 7;

                var date = new DateTime(_visibleMonth.Year, _visibleMonth.Month, day);
                var cell = BuildDayCell(date, appointmentsByDay.TryGetValue(date, out var list) ? list : new List<AppointmentDto>());
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                MonthGrid.Children.Add(cell);
            }
        }

        private Border BuildDayCell(DateTime date, List<AppointmentDto> dayAppointments)
        {
            bool isSelected = date.Date == _selectedDay.Date;

            var border = new Border
            {
                BorderBrush = isSelected ? SelectedCellBorderBrush : CellBorderBrush,
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                Background = isSelected ? SelectedCellBrush : Brushes.White,
                Margin = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = date,
            };
            border.MouseLeftButtonUp += OnDayCellClick;

            var content = new StackPanel { Margin = new Thickness(4) };
            content.Children.Add(new TextBlock
            {
                Text = date.Day.ToString(),
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? SelectedCellBorderBrush : Brushes.Black,
            });

            const int maxVisiblePills = 3;
            foreach (var appt in dayAppointments.Take(maxVisiblePills))
            {
                string patientName = _patientNameCache.TryGetValue(appt.PatientId, out var pn) ? pn : "Paciente";
                var pill = new Border
                {
                    Background = PillBrush,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 2, 0, 0),
                    Padding = new Thickness(4, 1, 4, 1),
                };
                pill.Child = new TextBlock
                {
                    Text = $"{appt.StartsAt:HH:mm} {patientName}",
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                content.Children.Add(pill);
            }

            if (dayAppointments.Count > maxVisiblePills)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"+{dayAppointments.Count - maxVisiblePills} más",
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            }

            border.Child = content;
            return border;
        }

        private async void OnDayCellClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: DateTime date })
                return;

            _selectedDay = date;
            BuildMonthGrid();
            await LoadSelectedDayAsync();
        }

        private async System.Threading.Tasks.Task LoadSelectedDayAsync()
        {
            SelectedDayText.Text = _selectedDay.ToString("dddd d 'de' MMMM", _esMx);

            var rows = new List<AppointmentRow>();
            foreach (var appt in _monthAppointments.Where(a => a.StartsAt.Date == _selectedDay.Date).OrderBy(a => a.StartsAt))
            {
                rows.Add(new AppointmentRow
                {
                    Id = appt.Id ?? "",
                    DentistId = appt.DentistId,
                    StartsAt = appt.StartsAt,
                    EndsAt = appt.EndsAt,
                    PatientName = _patientNameCache.TryGetValue(appt.PatientId, out var pn) ? pn : "(paciente desconocido)",
                    DoctorName = _doctorNameCache.TryGetValue(appt.DentistId, out var dn) ? dn : "(doctor desconocido)",
                    Reason = appt.Reason,
                    Status = appt.Status,
                });
            }

            SelectedDayList.ItemsSource = rows;
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async void OnNewAppointmentClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewAppointmentDialog(_selectedDay) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.CreateAppointmentAsync(
                        dialog.SelectedPatientId!, dialog.SelectedDentistId!, dialog.StartsAt, dialog.EndsAt, dialog.Reason);
                    await LoadMonthAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo crear la cita: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnSelectedDayItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectedDayList.SelectedItem is not AppointmentRow row)
                return;

            var dialog = new EditAppointmentDialog(row) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await LoadMonthAsync();
            }
        }
    }
}
