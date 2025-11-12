// InitialTestDetail.xaml.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace TestManagementSystem
{
    /// <summary>
    /// Interaction logic for InitialTestDetail.xaml
    /// </summary>
    public partial class InitialTestDetail : Window
    {
        private const string TestDataFile = "tests.json";
        private int NextTestNumber { get; set; }
        private bool _isEditMode = false;

        public InitialTestDetail()
        {
            InitializeComponent();
            CalculateNextTestNumber();
            // Ensure the owner is set so this new window is centered on the MainWindow
            this.Owner = Application.Current.MainWindow;
            DateEndPicker.SelectedDate = null;
        }

        public InitialTestDetail(TestModel testToEdit)
        {
            InitializeComponent();
            _isEditMode = true;
            this.Title = $"Edit Test Details T-{testToEdit.TestNumber:D3}";
            TestTitleTextBox.Text = testToEdit.TestTitle;
            LoadNumberTextBox.Text = testToEdit.LoadNumber;
            SystemTextBox.Text = testToEdit.System;

            // setting combo box values
            SetComboBoxItem(TestTypeComboBox, testToEdit.TestType);
            SetComboBoxItem(TestTypeDetailComboBox, testToEdit.TestTypeDetail);

            DateStartPicker.SelectedDate = testToEdit.DateStart;
            DateEndPicker.SelectedDate = testToEdit.DateEnd;

            DescriptionTextBox.Text = testToEdit.Description;

            SetCheckBoxes(testToEdit.DevicesTested);

            if (testToEdit.IsFinished)
            {
                TestTitleTextBox.IsEnabled = false; // prevent modifiying
            }
        }

        private void SetComboBoxItem(ComboBox comboBox, string content)
        {
            var itemToSelect = comboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == content);

            if(itemToSelect != null)
            {
                comboBox.SelectedItem = itemToSelect;
            }

        }

        private void SetCheckBoxes(List<string> devices)
        {
            if (devices.Contains(OFT1CheckBox.Content.ToString())) OFT1CheckBox.IsChecked = true;
            if (devices.Contains(OFT2CheckBox.Content.ToString())) OFT2CheckBox.IsChecked = true;
            if (devices.Contains(OFT3CheckBox.Content.ToString())) OFT3CheckBox.IsChecked = true;
            if (devices.Contains(OFT4CheckBox.Content.ToString())) OFT4CheckBox.IsChecked = true;
            if (devices.Contains(OFT5CheckBox.Content.ToString())) OFT5CheckBox.IsChecked = true;
        }

        private void CalculateNextTestNumber()
        {
            List<TestModel> existingTests = new List<TestModel>();
            // 1. Read existing JSON file to find the last TestNumber
            if (File.Exists(TestDataFile))
            {
                try
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    existingTests = JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading existing test data: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // On error, start numbering from 1
                    NextTestNumber = 1;
                    TestNumberTextBlock.Text = $"T-{NextTestNumber:D3}";
                    return;
                }
            }

            // 2. Find the highest existing TestNumber and add 1, or start at 1 if no tests exist
            NextTestNumber = existingTests.Any() ?
                existingTests.Max(t => t.TestNumber) + 1 : 1;
            TestNumberTextBlock.Text = $"T-{NextTestNumber:D3}";
            // Formats as T-001, T-010, etc.
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(TestTitleTextBox.Text) ||
                string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ||
                DateStartPicker.SelectedDate == null ||
                TestTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please fill in all required fields (marked with *).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check for selected devices
            List<string> selectedDevices = new List<string>();
            if (OFT1CheckBox.IsChecked == true) selectedDevices.Add(OFT1CheckBox.Content.ToString());
            if (OFT2CheckBox.IsChecked == true) selectedDevices.Add(OFT2CheckBox.Content.ToString());
            if (OFT3CheckBox.IsChecked == true) selectedDevices.Add(OFT3CheckBox.Content.ToString());
            if (OFT4CheckBox.IsChecked == true) selectedDevices.Add(OFT4CheckBox.Content.ToString());
            if (OFT5CheckBox.IsChecked == true) selectedDevices.Add(OFT5CheckBox.Content.ToString());

            if (!selectedDevices.Any())
            {
                MessageBox.Show("Please select at least one device to be tested.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Load existing data
            List<TestModel> allTests = new List<TestModel>();
            TestModel? existingTest = null;

            if (File.Exists(TestDataFile))
            {
                string jsonString = File.ReadAllText(TestDataFile);
                allTests = JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();
            }

            if (_isEditMode)
            {
                existingTest = allTests.FirstOrDefault(t => t.TestNumber == NextTestNumber);
                if(existingTest == null)
                {
                    MessageBox.Show("Error: Could not find existing test for update", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;

                }
                allTests.Remove(existingTest);
            }

            // 3. Create or update the TestModel object
            var newOrUpdatedTest = existingTest ?? new TestModel
            {
                TestNumber = NextTestNumber
            };

            newOrUpdatedTest.TestTitle = TestTitleTextBox.Text;
            newOrUpdatedTest.LoadNumber = LoadNumberTextBox.Text; // NEW
            newOrUpdatedTest.System = SystemTextBox.Text;         // NEW
            newOrUpdatedTest.TestType = ((ComboBoxItem)TestTypeComboBox.SelectedItem).Content.ToString();
            newOrUpdatedTest.TestTypeDetail = ((ComboBoxItem)TestTypeDetailComboBox.SelectedItem).Content.ToString(); // NEW
            newOrUpdatedTest.DateStart = DateStartPicker.SelectedDate.Value;
            // Use start date if end date is not set (for new tests)
            newOrUpdatedTest.DateEnd = DateEndPicker.SelectedDate ??
                DateStartPicker.SelectedDate.Value;
            newOrUpdatedTest.Description = DescriptionTextBox.Text;
            newOrUpdatedTest.DevicesTested = selectedDevices;

            // Check for DR Recheck Test Type
            string selectedTestType = newOrUpdatedTest.TestType;

            if(selectedTestType.Equals("DR Recheck", StringComparison.OrdinalIgnoreCase) && !_isEditMode)
            {
                newOrUpdatedTest.RecheckItems = GetOpenDRRecheckItems(allTests);
                /*
                List<CommentModel> openDRComments = GetOpenDRComments();

                var allDRsForNumbering = allTests.SelectMany(t => t.Comments)
                .Where(c => c.DrStatus != null || c.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == true)
                .GroupBy(c => c.CreatedTimestamp)
                .OrderBy(g => g.Key)
                .Select(g => g.First()) // Get the original creation comment for each unique DR
                .ToList();

                List<DRRecheckItem> recheckList = new List<DRRecheckItem>();
                int drCounter = 1;

                foreach (var creationComment in allDRsForNumbering)
                {
                    // Find the actual open comment data (which might be the latest status update)
                    var currentOpenComment = openDRComments.FirstOrDefault(c => c.CreatedTimestamp == creationComment.CreatedTimestamp);

                    if (currentOpenComment != null)
                    {
                        recheckList.Add(new DRRecheckItem
                        {
                            GlobalDRNumber = drCounter,
                            OriginatingTestNumber = allTests.FirstOrDefault(t => t.Comments.Contains(currentOpenComment))?.TestNumber ?? 0,
                            DateCreated = currentOpenComment.CreatedTimestamp,
                            LatestStatus = currentOpenComment.DrStatus ?? "Open",
                            LatestPriority = currentOpenComment.DrPriority ?? "P3",
                            LatestDescription = currentOpenComment.Description,
                            OriginalSubComments = currentOpenComment.SubComments.OrderBy(sc => sc.Timestamp).ToList()
                        });
                    }
                    drCounter++;
                }
                newOrUpdatedTest.RecheckItems = recheckList;
                */
            }

            // adding test and ssaving to json file
            try
            {
                allTests.Add(newOrUpdatedTest);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJsonString = JsonSerializer.Serialize(allTests, options);
                File.WriteAllText(TestDataFile, updatedJsonString);

                if (_isEditMode)
                {
                    this.Close();
                }
                else
                {
                    TestDetailsWindow detailsWindow = new TestDetailsWindow(newOrUpdatedTest);
                    detailsWindow.Show();
                    this.Close();
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show($"An error occured while saving test data: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }


           
        }

        // pull necessary open DR data 
        private List<CommentModel> GetOpenDRComments()
        {
            const string TestDataFile = "tests.json";
            List<TestModel> allTests = new List<TestModel>();

            if (File.Exists(TestDataFile))
            {
                try
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    allTests = JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();
                }
                catch { /* Ignore read error, return empty list */ }
            }

            // Flatten all comments and filter for open DRs
            return allTests.SelectMany(t => t.Comments)
                .Where(c => c.DrStatus != null && !c.DrStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private List<DRRecheckItem> GetOpenDRRecheckItems(List<TestModel> allTests)
        {
            List<DRRecheckItem> recheckItems = new List<DRRecheckItem>();

            // This forms a "DR thread" - all comments associated with a single logical DR.
            var allDrRelatedComments = allTests
            .SelectMany(test => test.Comments.Select(comment => new { comment, test }))
            .Where(item => item.comment.DrStatus != null ||
                           item.comment.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(item => item.comment.CreatedTimestamp) // Group by the original comment's creation time
            .OrderBy(g => g.Key) // Order groups by creation time for consistent global numbering
            .ToList();

            int globalDrCounter = 1;

            

            foreach (var drThread in allDrRelatedComments)
            {
                // The 'creation entry' is the very first comment in this DR's history.
                var creationEntry = drThread.First();

                // The 'latest entry' is the comment with the most recent EditHistory timestamp (or CreatedTimestamp if no edits).
                // This gives us the current status, priority, and description.
                var latestEntry = drThread
                    .OrderByDescending(x => x.comment.EditHistory.Any() ? x.comment.EditHistory.Last() : x.comment.CreatedTimestamp)
                    .First();

                var latestComment = latestEntry.comment;
                var parentTestOfLatestComment = latestEntry.test;

                // Only include OPEN DRs in the recheck list.
                // A DR is open if its latest DrStatus is NOT "Closed".
                if (!latestComment.DrStatus?.Equals("Closed", StringComparison.OrdinalIgnoreCase) ?? true) // If DrStatus is null, it's considered Open
                {
                    recheckItems.Add(new DRRecheckItem
                    {
                        GlobalDRNumber = globalDrCounter, // Assign the global DR number
                        OriginatingTestNumber = parentTestOfLatestComment.TestNumber,
                        DateCreated = creationEntry.comment.CreatedTimestamp, // Use the *original* creation date for "Date Opened"
                        LatestStatus = latestComment.DrStatus ?? "Open",
                        LatestPriority = latestComment.DrPriority ?? "P3",
                        LatestDescription = latestComment.Description,
                        // Collect all subcomments from the latest entry for history display in the recheck item
                        OriginalSubComments = latestComment.SubComments.OrderBy(sc => sc.Timestamp).ToList()
                    });
                }
                globalDrCounter++;
            }
            return recheckItems;
        }
    }
}
