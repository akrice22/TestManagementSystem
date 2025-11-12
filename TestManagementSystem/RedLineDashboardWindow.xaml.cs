using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace TestManagementSystem
{
    public partial class RedLineDashboardWindow : Window
    {
        private ICollectionView _redLinesView;
        private ObservableCollection<RedLineItemView> _redLinesData; // Bound data source
        private List<TestModel> _allTestsSnapshot; // Snapshot of all tests for persistence logic

        // Private fields to hold filter state
        private string _statusFilter = "All";
        private int? _testNumberFilter = null;
        private DateTime? _startDateFilter = null;
        private DateTime? _endDateFilter = null;

        // Constructor receives the data and a snapshot of all tests
        public RedLineDashboardWindow(List<RedLineItemView> redLinesData, List<TestModel> allTestsSnapshot)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            _redLinesData = new ObservableCollection<RedLineItemView>(redLinesData);
            _allTestsSnapshot = allTestsSnapshot;

            this.DataContext = _redLinesData;

            // Initialize the CollectionView for filtering
            _redLinesView = CollectionViewSource.GetDefaultView(this.DataContext);
            if (_redLinesView != null)
            {
                _redLinesView.Filter = new Predicate<object>(FilterRedLineList);
            }
        }

        // Predicate used by ICollectionView to determine which items to display
        private bool FilterRedLineList(object item)
        {
            if (item is not RedLineItemView redLine) return false;

            // 1. Status Filter
            if (_statusFilter == "Open" && redLine.IsResolved) return false;
            if (_statusFilter == "Resolved" && !redLine.IsResolved) return false;

            // 2. Test Number Filter
            if (_testNumberFilter.HasValue && redLine.TestNumber != _testNumberFilter.Value)
            {
                return false;
            }

            // 3. Date Range Filter (Start Date - Opens on or after)
            if (_startDateFilter.HasValue && redLine.DateOpened.Date < _startDateFilter.Value.Date)
            {
                return false;
            }

            // 4. Date Range Filter (End Date - Opens on or before)
            if (_endDateFilter.HasValue && redLine.DateOpened.Date > _endDateFilter.Value.Date)
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
            if (StatusFilterComboBox == null) return;

            // 1. Update Status Filter
            _statusFilter = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";

            // 2. Update Test Number Filter
            if (TestNumberFilterTextBox != null && int.TryParse(TestNumberFilterTextBox.Text, out int testNum))
            {
                _testNumberFilter = testNum;
            }
            else
            {
                _testNumberFilter = null;
            }

            // 3. Update Date Range Filters
            _startDateFilter = StartDateFilterPicker?.SelectedDate;
            _endDateFilter = EndDateFilterPicker?.SelectedDate;

            // 4. Apply filter refresh
            if (_redLinesView != null)
            {
                _redLinesView.Refresh();
            }
        }

        // Handles the Resolve button click, which updates the comment in the JSON file
        private void ResolveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is RedLineItemView selectedRedLine)
            {
                // 1. Find the parent TestModel in the snapshot
                var testToUpdate = _allTestsSnapshot.FirstOrDefault(t => t.TestNumber == selectedRedLine.TestNumber);

                if (testToUpdate != null)
                {
                    // 2. Find the corresponding CommentModel
                    // Since RedLineNumber is a dashboard-specific sequence, we must search by content/details.
                    // We look for an UNRESOLVED RedLine with matching details.
                    var commentToUpdate = testToUpdate.Comments.FirstOrDefault(c =>
                        !c.IsRedLineResolved &&
                        c.RedLineDetails?.Document == selectedRedLine.Document &&
                        c.RedLineDetails?.PageNumber == selectedRedLine.PageNumber);

                    if (commentToUpdate != null)
                    {
                        // 3. Update the model status
                        commentToUpdate.IsRedLineResolved = true;
                        // Add resolution time to history
                        commentToUpdate.EditHistory.Add(DateTime.Now);

                        // 4. Save the full list of tests back to JSON
                        if (SaveTestModelToJson(testToUpdate))
                        {
                            // 5. Update the local dashboard view immediately
                            selectedRedLine.IsResolved = true;
                            selectedRedLine.DateClosed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                            _redLinesView.Refresh(); // Refresh the grid to disable the button
                            MessageBox.Show($"RedLine #{selectedRedLine.RedLineNumber} in Test T-{selectedRedLine.TestNumber:D3} has been resolved.",
                                            "Resolution Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to save changes to the test data.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Could not find the original RedLine comment to update. Data might be stale or corrupted.",
                                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Helper method to save the updated TestModel to JSON
        private bool SaveTestModelToJson(TestModel updatedTest)
        {
            const string TestDataFile = "tests.json";

            try
            {
                List<TestModel> allTests = new List<TestModel>();
                if (File.Exists(TestDataFile))
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    allTests = JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();

                    // Remove the old version of the current test from the list
                    var testToRemove = allTests.FirstOrDefault(t => t.TestNumber == updatedTest.TestNumber);
                    if (testToRemove != null)
                    {
                        allTests.Remove(testToRemove);
                    }
                }

                // Add the updated test model
                allTests.Add(updatedTest);

                // Update the local snapshot for subsequent saves
                _allTestsSnapshot = allTests;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJsonString = JsonSerializer.Serialize(allTests, options);
                File.WriteAllText(TestDataFile, updatedJsonString);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving the test data: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Clears all filters and resets the view
        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            if (StatusFilterComboBox != null) StatusFilterComboBox.SelectedIndex = 0;
            if (TestNumberFilterTextBox != null) TestNumberFilterTextBox.Text = string.Empty;
            if (StartDateFilterPicker != null) StartDateFilterPicker.SelectedDate = null;
            if (EndDateFilterPicker != null) EndDateFilterPicker.SelectedDate = null;

            UpdateFilterState();
        }
    }
}