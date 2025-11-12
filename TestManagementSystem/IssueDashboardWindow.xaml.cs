using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading.Tasks;


namespace TestManagementSystem
{
    public partial class IssueDashboardWindow : Window
    {
        private ICollectionView _issuesView;

        private string _statusFilter = "All";
        private int? _testNumberFilter = null;
        private DateTime? _startDateFilter = null;
        private DateTime? _endDateFilter = null;

        public IssueDashboardWindow(List<IssueItemView> issuesData)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            // Set the DataContext to the list of issues
            this.DataContext = issuesData;

            // Initialize the CollectionView for filtering
            _issuesView = CollectionViewSource.GetDefaultView(this.DataContext);
            if (_issuesView != null)
            {
                _issuesView.Filter = new Predicate<object>(FilterIssueList);
            }
        }

        // Predicate used by ICollectionView to determine which items to display
        private bool FilterIssueList(object item)
        {
            if (item is not IssueItemView issue) return false;

            // 1. Status Filter
            if (_statusFilter != "All" && !issue.Status.Equals(_statusFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 2. Test Number Filter
            if (_testNumberFilter.HasValue && issue.TestNumber != _testNumberFilter.Value)
            {
                return false;
            }

            // 3. Date Range Filter (Start Date - Opens on or after)
            if (_startDateFilter.HasValue && issue.DateOpened.Date < _startDateFilter.Value.Date)
            {
                return false;
            }

            // 4. Date Range Filter (End Date - Opens on or before)
            // Note: This filters based on the *open date*, not the closed date
            if (_endDateFilter.HasValue && issue.DateOpened.Date > _endDateFilter.Value.Date)
            {
                return false;
            }

            return true;
        }

        private void HandleFilterChange(object sender, RoutedEventArgs e)
        {
            UpdateFilterState();
        }

        /*
        // Event handler to capture filter changes and trigger a refresh
        private void FilterDataGrid(object sender, SelectionChangedEventArgs e)
        {
            UpdateFilterState();
        }

        // Overload for TextBox TextChanged event
        private void FilterDataGrid(object sender, TextChangedEventArgs e)
        {
            UpdateFilterState();
        }

        // Overload for DatePicker SelectedDateChanged event
        private void FilterDataGrid(object sender, DatePicker and)
        {
            UpdateFilterState();
        }*/

        private void UpdateFilterState()
        {
            // CRITICAL FIX 1: If the main control (Status ComboBox) is null, the window is not ready. Stop.
            if (StatusFilterComboBox == null) return;

            // 1. Read Status Filter
            _statusFilter = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";

            // 2. Read Test Number Filter (Safely check for null before accessing the Text property)
            if (TestNumberFilterTextBox != null)
            {
                if (int.TryParse(TestNumberFilterTextBox.Text, out int testNum))
                {
                    _testNumberFilter = testNum;
                }
                else
                {
                    // Set to null if parsing failed or text is empty
                    _testNumberFilter = null;
                }
            }
            else
            {
                // Set to null if the control itself is null
                _testNumberFilter = null;
            }

            // 3. Read Date Range Filters (Safely using C# nullable access operator '?')
            _startDateFilter = StartDateFilterPicker?.SelectedDate;
            _endDateFilter = EndDateFilterPicker?.SelectedDate;

            // 4. Apply filter refresh (Safely check _issuesView before calling Refresh)
            if (_issuesView != null)
            {
                _issuesView.Refresh();
            }
        }

        // Clears all filters and resets the view
        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            StatusFilterComboBox.SelectedIndex = 0;
            TestNumberFilterTextBox.Text = string.Empty;
            StartDateFilterPicker.SelectedDate = null;
            EndDateFilterPicker.SelectedDate = null;

            // Recalculate and refresh the view
            UpdateFilterState();
        }
    }
}
