using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace TestManagementSystem
{

    public partial class DRDashboardWindow : Window
    {
        private ICollectionView _drsView;

        private string _statusFilter = "All";
        private string _priorityFilter = "All";
        private int? _testNumberFilter = null;
        private DateTime? _startDateFilter = null;
        private DateTime? _endDateFilter = null;

       


        public DRDashboardWindow(List<DRItemView> drsData)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

  

            
            this.DataContext = drsData;

            _drsView = CollectionViewSource.GetDefaultView(this.DataContext);
            if (_drsView != null)
            {
                _drsView.Filter = new Predicate<object>(FilterDRList);
            }
        }

        public bool FilterDRList(object item)
        {
            if (item is not DRItemView dr) return false;

            if (_statusFilter != "All" && !dr.Status.Equals(_statusFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_priorityFilter != "All" && !dr.Priority.Equals(_priorityFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3. Test Number Filter
            if (_testNumberFilter.HasValue && dr.TestNumber != _testNumberFilter.Value)
            {
                return false;
            }

            // 4. Date Range Filter (Start Date - Opens on or after)
            if (_startDateFilter.HasValue && dr.DateOpened.Date < _startDateFilter.Value.Date)
            {
                return false;
            }

            // 5. Date Range Filter (End Date - Opens on or before)
            if (_endDateFilter.HasValue && dr.DateOpened.Date > _endDateFilter.Value.Date)
            {
                return false;
            }

            return true;
        }

        // Unified handler for all filter events
        private void HandleFilterChange(object sender, RoutedEventArgs e)
        {
            UpdateFilterState();
        }

        private void UpdateFilterState()
        {
            // CRITICAL FIX: Null checks added for all XAML controls in a single block
            if (StatusFilterComboBox == null || PriorityFilterComboBox == null || TestNumberFilterTextBox == null) return;
            // 1. Update Status and Priority Filters
            _statusFilter = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
            _priorityFilter = (PriorityFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";

            // 2. Update Test Number Filter
            if (TestNumberFilterTextBox != null && int.TryParse(TestNumberFilterTextBox.Text, out int testNum))
            {
                _testNumberFilter = testNum;
            }
            else
            {
                _testNumberFilter = null;
            }

            // 3. Update Date Range Filters (Safely using C# nullable access operator '?')
            _startDateFilter = StartDateFilterPicker?.SelectedDate;
            _endDateFilter = EndDateFilterPicker?.SelectedDate;

            // 4. Apply filter refresh
            if (_drsView != null)
            {
                _drsView.Refresh();
            }
        }

        // Clears all filters and resets the view
        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            if (StatusFilterComboBox != null) StatusFilterComboBox.SelectedIndex = 0;
            if (PriorityFilterComboBox != null) PriorityFilterComboBox.SelectedIndex = 0;
            if (TestNumberFilterTextBox != null) TestNumberFilterTextBox.Text = string.Empty;
            if (StartDateFilterPicker != null) StartDateFilterPicker.SelectedDate = null;
            if (EndDateFilterPicker != null) EndDateFilterPicker.SelectedDate = null;

            UpdateFilterState();
        }


    }
}
