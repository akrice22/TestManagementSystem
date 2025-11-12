// TestDetailsWindow.xaml.cs

using System.Windows;
using System.Linq;
using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TestManagementSystem
{
    /// <summary>
    /// Interaction logic for TestDetailsWindow.xaml
    /// </summary>
    public partial class TestDetailsWindow : Window
    {
        // Store the TestModel data globally for this window
        private TestModel _currentTestData;

        // define file constant here
        private const string TestDataFile = "tests.json";

        // Constructor that accepts the newly created TestModel
        public TestDetailsWindow(TestModel testData)
        {
           

            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            TestModel? latestTestData = LoadSingleTestModel(testData.TestNumber);
            if (latestTestData == null) latestTestData = testData;

            _currentTestData = latestTestData;
            this.DataContext = _currentTestData;

            DisplayTestDetails(_currentTestData);
            RefreshCommentsDisplay();

            // Initialize Recheck Display
            InitializeRecheckDisplay();

            // Wire up button handlers
            AddCommentButton.Click += AddCommentButton_Click;
            EndTestButton.Click += EndTestButton_Click;
            SaveProgressButton.Click += SaveProgressButton_Click;
            EditDetailsButton.Click += EditDetailsButton_Click;

            CheckTestStatus(); // Check if test is finished on load
        }

        // Set up the Recheck display visibility
        private void InitializeRecheckDisplay()
        {
            if (_currentTestData.RecheckItems != null && _currentTestData.RecheckItems.Any())
            {
                // NEW: set the IsDRClosedForRecheck flag based on the current status
                foreach(var item in _currentTestData.RecheckItems)
                {
                    // if current status is Open, isDRClosedForRecheck should be false
                    item.IsDRClosedForRecheck = item.LatestStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase);
                }
                DRRecheckPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DRRecheckPanel.Visibility = Visibility.Collapsed;
            }
        }

        // method to save the recheck comments
        private void SaveRecheckComments()
        {
            if (!_currentTestData.RecheckItems.Any()) return;

            // This is simple because the DataGrid/ItemsControl uses TwoWay binding by default.
            // When the user types into the RecheckCommentTextBox, the RecheckComment property 
            // on the corresponding DRRecheckItem object in _currentTestData.RecheckItems is updated.
            // We just need to stamp the time for any non-empty comment.

            foreach (var item in _currentTestData.RecheckItems)
            {
                if (!string.IsNullOrWhiteSpace(item.RecheckComment) && item.RecheckCommentTimestamp == null)
                {
                    item.RecheckCommentTimestamp = DateTime.Now;
                }
            }
        }





        private void CheckTestStatus()
        {
            if (_currentTestData.IsFinished)
            {
                EndTestButton.Content = "Test Ended";
                EndTestButton.IsEnabled = false;
                AddCommentButton.IsEnabled = false;
                SaveProgressButton.IsEnabled = false;

                // Change the End Test Button to a soft grey to indicate disabled/finished status
                EndTestButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#303030"));
            }
        }

        private void DisplayTestDetails(TestModel testData)
        {
            // Set main header details
            HeaderTestNumber.Text = $"TEST T-{testData.TestNumber:D3}";
            HeaderTestTitle.Text = testData.TestTitle;

            // Set detail fields
            DateStartTextBlock.Text = testData.DateStart.ToShortDateString();
            DateEndTextBlock.Text = testData.IsFinished
                ? $"{testData.DateEnd.ToShortDateString()} (Final)"
                : testData.DateEnd.ToShortDateString();

            TestTypeTextBlock.Text = testData.TestType;
            DescriptionTextBlock.Text = testData.Description;
            DevicesTestedTextBlock.Text = string.Join(", ", testData.DevicesTested);

            // NEW FIELDS
            LoadNumberTextBlock.Text = testData.LoadNumber;
            SystemTextBlock.Text = testData.System;
            TestTypeDetailTextBlock.Text = testData.TestTypeDetail;

            /* // Display the end date with a note if the test is finished NEW
             DateEndTextBlock.Text = testData.IsFinished
                 ? $"{testData.DateEnd.ToShortDateString()} (Final)"
                 : testData.DateEnd.ToShortDateString();

             // Set detail fields
             DateStartTextBlock.Text = testData.DateStart.ToShortDateString();
             DateEndTextBlock.Text = testData.DateEnd.ToShortDateString();
             TestTypeTextBlock.Text = testData.TestType;
             DescriptionTextBlock.Text = testData.Description;
             // Join the list of devices into a single, comma-separated string
             DevicesTestedTextBlock.Text = string.Join(", ", testData.DevicesTested);*/
        }

        private void EditDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Reload latest data just in case it was edited elsewhere
            TestModel? latestTestData = LoadSingleTestModel(_currentTestData.TestNumber);
            if (latestTestData == null)
            {
                MessageBox.Show("Error: Could not reload test data for editing.", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2. Open the InitialTestDetail window in edit mode
            InitialTestDetail editWindow = new InitialTestDetail(latestTestData);
            editWindow.ShowDialog();

            // 3. After the edit window closes, reload and refresh this window
            TestModel? reloadedTestData = LoadSingleTestModel(_currentTestData.TestNumber);
            if (reloadedTestData != null)
            {
                _currentTestData = reloadedTestData;
                DisplayTestDetails(_currentTestData);
                RefreshCommentsDisplay();
                CheckTestStatus(); // In case Load Number was edited on a finished test
            }
        }

        //End Test Button Handler
        private void EndTestButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirmation box with light/grey text (system default for dark theme)
            MessageBoxResult result = MessageBox.Show(
                "Are you absolutely certain you want to end this test event? This action finalizes the test date and disables further comments/modifications.",
                "Confirm End Test Event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {

                SaveRecheckComments();
                // Set the end date to now and mark as finished
                _currentTestData.DateEnd = DateTime.Now;
                _currentTestData.IsFinished = true;

                // Save and close
                if (SaveTestModelToJson(_currentTestData))
                {
                    MessageBox.Show($"Test T-{_currentTestData.TestNumber:D3} has been successfully ended and saved.", "Test Event Ended", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    _currentTestData.IsFinished = false;
                    MessageBox.Show("Failed to save the finalized test data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // NEW: Save Progress Button Handler
        private void SaveProgressButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecheckComments();

            if (_currentTestData.RecheckItems != null && _currentTestData.RecheckItems.Any())
            {
                // 1. Get the current state of ALL tests from the JSON file
                List<TestModel> allTestsInFile = LoadAllTests() ?? new List<TestModel>();
                bool fileNeedsUpdate = false;

                foreach (var recheckItem in _currentTestData.RecheckItems)
                {
                    // Find the original test and original comment based on creation date
                    var originalTest = allTestsInFile.FirstOrDefault(t => t.TestNumber == recheckItem.OriginatingTestNumber);

                    if (originalTest != null)
                    {
                        // Find the original CommentModel by its creation date
                        var originalComment = originalTest.Comments
                            .FirstOrDefault(c => c.CreatedTimestamp == recheckItem.DateCreated);

                        if (originalComment != null)
                        {
                            // 2. Apply Priority update (LatestPriority is two-way bound to the ComboBox)
                            if (!originalComment.DrPriority.Equals(recheckItem.LatestPriority))
                            {
                                originalComment.DrPriority = recheckItem.LatestPriority;
                                fileNeedsUpdate = true;
                            }

                            // 3. Apply Status update (IsDRClosedForRecheck is two-way bound to the CheckBox)
                            string newStatus = recheckItem.IsDRClosedForRecheck ? "Closed" : "Open";

                            if (!originalComment.DrStatus.Equals(newStatus))
                            {
                                originalComment.DrStatus = newStatus;
                                originalComment.EditHistory.Add(DateTime.Now); // Log the status change
                                fileNeedsUpdate = true;
                            }
                        }
                    }
                }

                // 4. Save the entire collection of tests back to the file if changes were made
                if (fileNeedsUpdate && !SaveAllTestsToFile(allTestsInFile))
                {
                    MessageBox.Show("Failed to save updates to the original DRs.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                /*
                if (SaveTestModelToJson(_currentTestData))
                {
                    MessageBox.Show("Test progress saved successfully.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Reload and refresh to show latest data
                    TestModel? reloadedTestData = LoadSingleTestModel(_currentTestData.TestNumber);
                    if (reloadedTestData != null)
                    {
                        _currentTestData = reloadedTestData;
                        RefreshCommentsDisplay();
                        InitializeRecheckDisplay();
                    }
                }*/
            }

            // Final save of the current test details (including RecheckComments)
            if (SaveTestModelToJson(_currentTestData))
            {
                MessageBox.Show("Test progress saved successfully.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                // Reload and refresh to show latest data
                TestModel? reloadedTestData = LoadSingleTestModel(_currentTestData.TestNumber);
                if (reloadedTestData != null)
                {
                    _currentTestData = reloadedTestData;
                    RefreshCommentsDisplay();
                    InitializeRecheckDisplay();
                }
            }
        }

        // NEW: saves entire List<TestModel> Collection back to JSON (required to cross-test updates)
        private bool SaveAllTestsToFile(List<TestModel> allTests)
        {
            try
            {
                allTests = allTests.OrderBy(t => t.TestNumber).ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJsonString = JsonSerializer.Serialize(allTests, options);
                File.WriteAllText(TestDataFile, updatedJsonString);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal Error saving all tests: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // NEW helped to reload all tests (required for SaveProgressButton_Click
        private List<TestModel>? LoadAllTests()
        {
            try
            {
                if (File.Exists(TestDataFile))
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    return JsonSerializer.Deserialize<List<TestModel>>(jsonString);
                }
            }
            catch { }
            return null;
        }

        // Handler for the "Add New Comment" button
        private void AddCommentButton_Click(object sender, RoutedEventArgs e)
        {
            // Reload the test model from JSON before opening to ensure next comment number is correct
            TestModel? latestTestData = LoadSingleTestModel(_currentTestData.TestNumber);
            if (latestTestData == null) return;
            _currentTestData = latestTestData; // Update local copy with latest data

            NewCommentWindow commentWindow = new NewCommentWindow(_currentTestData);
            bool? result = commentWindow.ShowDialog();

            if (result == true)
            {
                // The comment was saved, reload the test model to get the updated list of comments
                TestModel? reloadedTestData = LoadSingleTestModel(_currentTestData.TestNumber);
                if (reloadedTestData != null)
                {
                    _currentTestData = reloadedTestData;
                    RefreshCommentsDisplay();
                }
                else
                {
                    MessageBox.Show("Error reloading test data after comment save.", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Helper to reload a single test model from JSON
        private TestModel? LoadSingleTestModel(int testNumber)
        {
            const string TestDataFile = "tests.json";

            if (File.Exists(TestDataFile))
            {
                try
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    List<TestModel> allTests = JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();
                    return allTests.FirstOrDefault(t => t.TestNumber == testNumber);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading test data: {ex.Message}", "Data Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            return null;
        }

        // Helper to re-save the entire test list (used for delete logic)
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

                allTests.Add(updatedTest);
                allTests = allTests.OrderBy(t => t.TestNumber).ToList();

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

        // Method to dynamically create and display comment UIElements
        private void RefreshCommentsDisplay()
        {
            // Clear existing comments
            CommentsStackPanel.Children.Clear();

            if (!_currentTestData.Comments.Any())
            {
                CommentsStackPanel.Children.Add(new TextBlock
                {
                    Text = "No comments have been added for this test.",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")),
                    Margin = new Thickness(0, 5, 0, 0)
                });
                return;
            }

            var sortedComments = _currentTestData.Comments.OrderBy(c => c.CommentNumber);

            foreach (var comment in sortedComments)
            {
                // Main border for the comment
                Border commentBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")), // BackgroundSecondaryColor
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                Grid commentGrid = new Grid();
                // 3 columns: Icon (Auto), Content (*), Buttons (Auto)
                commentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                commentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                commentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // --- 1. Icon (Left Side) ---
                TextBlock iconTextBlock = new TextBlock
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 10, 0),
                    Text = "!"
                };

                bool hasIcon = false;

                // Set color and icon based on status
                if (comment.DrStatus != null) // It's a DR
                {
                    hasIcon = true;
                    if (comment.IsDRClosed)
                    {
                        // Green Checkmark for closed
                        iconTextBlock.Text = "✓";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // AccentColor
                    }
                    else
                    {
                        // Red Exclamation for open DR
                        iconTextBlock.Text = "!";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935")); // Custom Red
                    }
                }
                else if (comment.IssueStatus != null) // It's an Issue
                {
                    hasIcon = true;
                    if (comment.IsIssueClosed)
                    {
                        // Green Checkmark for closed
                        iconTextBlock.Text = "✓";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // AccentColor
                    }
                    else
                    {
                        // Yellow/Amber Exclamation for open Issue
                        iconTextBlock.Text = "!";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")); // Custom Yellow/Amber
                    }
                }
                else if (comment.RedLineDetails != null) // It's a RedLine
                {
                    hasIcon = true;
                    if (comment.IsRedLineResolved)
                    {
                        // Green Checkmark for resolved
                        iconTextBlock.Text = "✓";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // AccentColor
                    }
                    else
                    {
                        // Orange Exclamation for RedLine
                        iconTextBlock.Text = "!";
                        iconTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); // Custom Orange
                    }
                }

                if (!hasIcon)
                {
                    // Regular comment - no icon
                    iconTextBlock.Text = "";
                    iconTextBlock.Width = 0;
                    commentGrid.ColumnDefinitions.Clear(); // Remove auto column if not needed
                    commentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    commentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(iconTextBlock, 0); // Put the content in the first column
                }
                else
                {
                    Grid.SetColumn(iconTextBlock, 0);
                    commentGrid.Children.Add(iconTextBlock);
                }

                // --- 2. Comment Content (Center) ---
                StackPanel contentStackPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                Grid.SetColumn(contentStackPanel, hasIcon ? 1 : 0); // Use 1 if icon is present, 0 otherwise

                // Title and Number
                TextBlock titleTextBlock = new TextBlock
                {
                    Text = $"Comment #{comment.CommentNumber} - {comment.Title}",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")), // ForegroundColor
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                // Description
                TextBlock descriptionTextBlock = new TextBlock
                {
                    Text = comment.Description,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")), // ForegroundColor
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                // Timestamp and Details
                TextBlock infoTextBlock = new TextBlock
                {
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")), // ForegroundSecondaryColor
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                // Build the info text
                string infoText = $"Added: {comment.CreatedTimestamp:yyyy-MM-dd HH:mm:ss}";

                // Display all edit times if edited
                if (comment.EditHistory.Count > 1)
                {
                    infoText += $"\nHistory: {string.Join(" | ", comment.EditHistory.Select(t => t.ToString("yyyy-MM-dd HH:mm:ss")))}";
                }

                if (comment.DrPriority != null)
                {
                    infoText += $"\nDR Details: {comment.DrPriority} | Status: {comment.DrStatus}";
                }
                else if (comment.IssueStatus != null)
                {
                    infoText += $"\nIssue Status: {comment.IssueStatus}";
                }
                else if (comment.RedLineDetails != null)
                {
                    infoText += $"\nRedLine: Doc: {comment.RedLineDetails.Document} | Page: {comment.RedLineDetails.PageNumber} | Section: {comment.RedLineDetails.Section} | Resolved: {(comment.IsRedLineResolved ? "Yes" : "No")}";
                }

                infoTextBlock.Text = infoText;

                contentStackPanel.Children.Add(titleTextBlock);
                contentStackPanel.Children.Add(descriptionTextBlock);
                contentStackPanel.Children.Add(infoTextBlock);

                commentGrid.Children.Add(contentStackPanel);

                // --- 3. Buttons (Right Side) ---
                StackPanel buttonStackPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                Grid.SetColumn(buttonStackPanel, hasIcon ? 2 : 1); // Use 2 if icon is present, 1 otherwise

                // Edit Button
                Button editButton = new Button
                {
                    Content = "Edit",
                    Style = (Style)this.FindResource("ActionButtonStyle"),
                    Tag = comment.CommentNumber, // Store the comment number for identification
                    Margin = new Thickness(0, 0, 5, 0)
                };
                editButton.Click += EditCommentButton_Click;

                // Delete Button
                Button deleteButton = new Button
                {
                    Content = "Delete",
                    Style = (Style)this.FindResource("DeleteButtonStyle"),
                    Tag = comment.CommentNumber
                };
                deleteButton.Click += DeleteCommentButton_Click;

                buttonStackPanel.Children.Add(editButton);
                buttonStackPanel.Children.Add(deleteButton);

                commentGrid.Children.Add(buttonStackPanel);

                commentBorder.Child = commentGrid;
                CommentsStackPanel.Children.Add(commentBorder);
            }
        }

        // Handler for Edit Button
        private void EditCommentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int commentNumber)
            {
                CommentModel? commentToEdit = _currentTestData.Comments.FirstOrDefault(c => c.CommentNumber == commentNumber);
                if (commentToEdit != null)
                {
                    // Open the window in edit mode
                    NewCommentWindow editWindow = new NewCommentWindow(_currentTestData, commentToEdit);
                    bool? result = editWindow.ShowDialog();

                    if (result == true)
                    {
                        // Reload and refresh
                        TestModel? reloadedTestData = LoadSingleTestModel(_currentTestData.TestNumber);
                        if (reloadedTestData != null)
                        {
                            _currentTestData = reloadedTestData;
                            RefreshCommentsDisplay();
                        }
                    }
                }
            }
        }

        // Handler for Delete Button
        private void DeleteCommentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int commentNumber)
            {
                // Confirmation popup
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete Comment #{commentNumber}? This action cannot be undone.",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Remove the comment from the list
                    CommentModel? commentToDelete = _currentTestData.Comments.FirstOrDefault(c => c.CommentNumber == commentNumber);
                    if (commentToDelete != null)
                    {
                        _currentTestData.Comments.Remove(commentToDelete);

                        // Re-save the updated list of all tests to JSON file
                        if (SaveTestModelToJson(_currentTestData))
                        {
                            RefreshCommentsDisplay();
                        }
                        else
                        {
                            MessageBox.Show("Failed to delete comment and save data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
    }
}