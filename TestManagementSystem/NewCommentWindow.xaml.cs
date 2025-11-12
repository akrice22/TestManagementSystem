using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace TestManagementSystem
{
    public partial class NewCommentWindow : Window
    {
        private TestModel _currentTest;
        private int _nextCommentNumber;
        private bool _isEditMode = false;
        private CommentModel? _commentToEdit;

        private List<SubCommentModel> _drSubComments = new List<SubCommentModel>();
        private List<SubCommentModel> _issueSubComments = new List<SubCommentModel>();
        private List<SubCommentModel> _redLineSubComments = new List<SubCommentModel>();

        // Constructor for adding a new comment
        public NewCommentWindow(TestModel testData)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            _currentTest = testData;
            _isEditMode = false;
            CalculateNextCommentNumber();
            CommentNumberTextBlock.Text = $"Comment #{_nextCommentNumber}";

            DRSubCommentPanel.Visibility = Visibility.Collapsed;
            IssueSubCommentPanel.Visibility = Visibility.Collapsed;
            RedLineSubCommentPanel.Visibility = Visibility.Collapsed;
        }

        // Constructor for editing an existing comment
        public NewCommentWindow(TestModel testData, CommentModel commentToEdit)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            _currentTest = testData;
            _isEditMode = true;
            _commentToEdit = commentToEdit;

            // Update UI for Edit Mode
            this.Title = $"Edit Comment #{commentToEdit.CommentNumber}";
            // Display comment number and last modified time
            CommentNumberTextBlock.Text = $"Comment #{commentToEdit.CommentNumber} (Last Modified: {commentToEdit.EditHistory.LastOrDefault():yyyy-MM-dd HH:mm:ss})";
            SaveCommentButton.Content = "Update Comment";

            NewCommentHeaderTextBlock.Visibility = Visibility.Collapsed;

            this.Title = $"Edit Comment #{commentToEdit.CommentNumber}";
            SaveCommentButton.Content = "Update Comment";

            LoadCommentForEdit(commentToEdit);
        }

        private void CalculateNextCommentNumber()
        {
            _nextCommentNumber = _currentTest.Comments.Any() ?
                                 _currentTest.Comments.Max(c => c.CommentNumber) + 1 : 1;
        }

        private void LoadCommentForEdit(CommentModel comment)
        {
            CommentTitleTextBox.Text = comment.Title;
            DescriptionTextBox.Text = comment.Description;

            // intial panel visibility for subcomments for DR,Issue, or Redline
            DRSubCommentPanel.Visibility = Visibility.Collapsed;
            IssueSubCommentPanel.Visibility = Visibility.Collapsed;
            RedLineSubCommentPanel.Visibility = Visibility.Collapsed;

            // Set the existing sub-comments to the temporary lists and display them
            if (comment.SubComments.Any())
            {
                if (comment.DrStatus != null)
                {
                    _drSubComments = comment.SubComments;
                    RefreshSubCommentDisplay(DRSubCommentList, _drSubComments);
                }
                else if (comment.IssueStatus != null)
                {
                    _issueSubComments = comment.SubComments;
                    RefreshSubCommentDisplay(IssueSubCommentList, _issueSubComments);
                }
                else if (comment.RedLineDetails != null)
                {
                    _redLineSubComments = comment.SubComments;
                    RefreshSubCommentDisplay(RedLineSubCommentList, _redLineSubComments);
                }
            }

            // DR Details
            if (comment.DrPriority != null || comment.DrStatus != null)
            {
                DRCheckBox.IsChecked = true;
                // Select the matching ComboBoxItem based on content
                DRPriorityComboBox.SelectedItem = DRPriorityComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString()!.StartsWith(comment.DrPriority!, StringComparison.OrdinalIgnoreCase));
                DRStatusComboBox.SelectedItem = DRStatusComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString()!.Equals(comment.DrStatus, StringComparison.OrdinalIgnoreCase));
            }

            // Issue Details
            if (comment.IssueStatus != null)
            {
                IssueCheckBox.IsChecked = true;
                IssueStatusComboBox.SelectedItem = IssueStatusComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString()!.Equals(comment.IssueStatus, StringComparison.OrdinalIgnoreCase));
            }

            // RedLine Details
            if (comment.RedLineDetails != null)
            {
                RedLineCheckBox.IsChecked = true;
                RedLineDocumentTextBox.Text = comment.RedLineDetails.Document;
                RedLinePageNumberTextBox.Text = comment.RedLineDetails.PageNumber;
                RedLineSectionTextBox.Text = comment.RedLineDetails.Section;
                RedLineResolvedCheckBox.IsChecked = comment.IsRedLineResolved;
            }
        }

        // handled "Add New Comment" button click
        private void AddSubComment(TextBox input, List<SubCommentModel> list, StackPanel displayList)
        {
            string text = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Please enter text for the sub-comment.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            list.Add(new SubCommentModel { Text = text, Timestamp = DateTime.Now });
            input.Text = string.Empty; // Clear input field

            RefreshSubCommentDisplay(displayList, list);
        }

        // visually update list of sub-comments
        // Helper to visually update the list of sub-comments
        private void RefreshSubCommentDisplay(StackPanel displayList, List<SubCommentModel> list)
        {
            displayList.Children.Clear();
            foreach (var sc in list.OrderBy(c => c.Timestamp))
            {
                TextBlock tb = new TextBlock
                {
                    Text = $"[{sc.Timestamp:MM-dd HH:mm:ss}] {sc.Text}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")), // White
                    Margin = new Thickness(0, 2, 0, 2)
                };
                displayList.Children.Add(tb);
            }
        }

        // handling sub-comment buttons
        // Event Handlers for Sub-Comment buttons
        private void AddDRComment_Click(object sender, RoutedEventArgs e)
        {
            AddSubComment(DRSubCommentTextBox, _drSubComments, DRSubCommentList);
        }

        private void AddIssueComment_Click(object sender, RoutedEventArgs e)
        {
            AddSubComment(IssueSubCommentTextBox, _issueSubComments, IssueSubCommentList);
        }

        private void AddRedLineComment_Click(object sender, RoutedEventArgs e)
        {
            AddSubComment(RedLineSubCommentTextBox, _redLineSubComments, RedLineSubCommentList);
        }


        // handler for IssueStatusComboBox
        private void IssueStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(IssueStatusComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string status = selectedItem.Content.ToString() ?? "";

                if(status.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase))
                {
                    DRCheckBox.IsChecked = true;
                    IssueCheckBox.IsChecked = false;

                    // COME BACK AND CHANGE THIS
                    SetComboBoxItem(DRPriorityComboBox, "P3 -Medium");
                    SetComboBoxItem(DRStatusComboBox, "Open");

                    MessageBox.Show("Issue automatically converted to an Open DR. The final DR number will be assigned when the test is saved.",
                        "Status Conversion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SetComboBoxItem(ComboBox comboBox, string content)
        {
            var itemToSelect = comboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString()!.StartsWith(content, StringComparison.OrdinalIgnoreCase));

            if (itemToSelect != null)
            {
                comboBox.SelectedItem = itemToSelect;
            }
            
                
         
        }

        private void SaveCommentButton_Click(object sender, RoutedEventArgs e)
        {
            // for switching an Issue to "Moved to DR" to close it 
            string finalIssueStatus = ((ComboBoxItem)IssueStatusComboBox.SelectedItem)?.Content.ToString() ?? "";

            // 1. Validation
            if (string.IsNullOrWhiteSpace(CommentTitleTextBox.Text) ||
                string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Please fill in the Comment Title and Description.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Enforce "Select one type" rule - check how many checkboxes are selected
            int selectedTypes = 0;
            if (DRCheckBox.IsChecked == true) selectedTypes++;
            if (IssueCheckBox.IsChecked == true) selectedTypes++;
            if (RedLineCheckBox.IsChecked == true) selectedTypes++;

            if (selectedTypes > 1)
            {
                MessageBox.Show("Please select at most one type of associated item (DR, Issue, or RedLine).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Create/Update Comment Model
            CommentModel newOrUpdatedComment = _commentToEdit ?? new CommentModel
            {
                CommentNumber = _nextCommentNumber,
                CreatedTimestamp = DateTime.Now
            };

            newOrUpdatedComment.Title = CommentTitleTextBox.Text;
            newOrUpdatedComment.Description = DescriptionTextBox.Text;

            // Clear existing details if it's an update, or if checkboxes are unchecked
            newOrUpdatedComment.DrPriority = null;
            newOrUpdatedComment.DrStatus = null;
            newOrUpdatedComment.IssueStatus = null;
            newOrUpdatedComment.RedLineDetails = null;
            newOrUpdatedComment.IsRedLineResolved = false;

            // Handle DR Details
            if (DRCheckBox.IsChecked == true)
            {
                string? drPriority = (DRPriorityComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                string? drStatus = (DRStatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

                if (string.IsNullOrWhiteSpace(drPriority) || string.IsNullOrWhiteSpace(drStatus))
                {
                    MessageBox.Show("Please select both Priority and Status for the Defect Report (DR).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Trim priority (e.g., "P1 - Critical" -> "P1")
                newOrUpdatedComment.DrPriority = drPriority.Split(' ')[0];
                newOrUpdatedComment.DrStatus = drStatus;
            }

            // Handle Issue Details
            else if (IssueCheckBox.IsChecked == true)
            {
                string? issueStatus = (IssueStatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (string.IsNullOrWhiteSpace(issueStatus))
                {
                    MessageBox.Show("Please select a Status for the Issue.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                newOrUpdatedComment.IssueStatus = issueStatus;
            }

            // Handle RedLine Details
            else if (RedLineCheckBox.IsChecked == true)
            {
                newOrUpdatedComment.RedLineDetails = new RedLineDetailModel
                {
                    Document = RedLineDocumentTextBox.Text,
                    PageNumber = RedLinePageNumberTextBox.Text,
                    Section = RedLineSectionTextBox.Text
                };
                newOrUpdatedComment.IsRedLineResolved = RedLineResolvedCheckBox.IsChecked == true;
            }

            if (DRCheckBox.IsChecked == true &&
                finalIssueStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase) &&
                _commentToEdit?.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == false)
            {
                // This indicates a valid conversion from Issue -> DR
                newOrUpdatedComment.ConversionTimestamp = DateTime.Now;
                // We MUST save the IssueStatus as "Moved to DR" for the DR dashboard to know the origin.
                newOrUpdatedComment.IssueStatus = "Moved to DR";
            }
            else
            {
                newOrUpdatedComment.ConversionTimestamp = null;
                newOrUpdatedComment.IssueStatus = finalIssueStatus;
            }

            // collect sub-comments
            List<SubCommentModel> collectedSubComments = new List<SubCommentModel>();
            if (DRCheckBox.IsChecked == true) collectedSubComments.AddRange(_drSubComments);
            else if (IssueCheckBox.IsChecked == true) collectedSubComments.AddRange(_issueSubComments);
            else if (RedLineCheckBox.IsChecked == true) collectedSubComments.AddRange(_redLineSubComments);

            newOrUpdatedComment.SubComments = collectedSubComments; // Attach the sub-comments


            // Update Edit History on modification
            if (_isEditMode)
            {
                // Add current time as a new modification timestamp
                newOrUpdatedComment.EditHistory.Add(DateTime.Now);
            }
            // If it's a new comment, add the creation time to the history
            else if (!newOrUpdatedComment.EditHistory.Any())
            {
                newOrUpdatedComment.EditHistory.Add(newOrUpdatedComment.CreatedTimestamp);
            }

            // Add to TestModel if new
            if (!_isEditMode)
            {
                _currentTest.Comments.Add(newOrUpdatedComment);
            }

            if(IssueCheckBox.IsChecked == false && DRCheckBox.IsChecked == true)
            {
                if(_isEditMode && _commentToEdit?.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == true)
                {
                    newOrUpdatedComment.IssueNumberOrigin = _commentToEdit.CommentNumber; // This is the local Issue #
                    newOrUpdatedComment.ConversionTimestamp = DateTime.Now;
                }
            }

            // 4. Save the updated list of all tests to JSON file
            if (SaveTestModelToJson(_currentTest))
            {
                this.DialogResult = true; // Signal success to the calling window
                this.Close();
            }


        }

        // Helper method to save the updated TestModel to JSON
        private bool SaveTestModelToJson(TestModel updatedTest)
        {
            const string TestDataFile = "tests.json"; // Use the same constant as InitialTestDetail

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

                // Sort by TestNumber before saving (optional, but good practice)
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

        // New for subcomments
        private void AssociatedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkedBox = (CheckBox)sender;

            // 1. Enforce "Select at most one" rule: Uncheck any other box that is currently checked
            CheckBox[] allCheckboxes = { DRCheckBox, IssueCheckBox, RedLineCheckBox };
            foreach (var cb in allCheckboxes)
            {
                // We uncheck others, which will automatically trigger their Unchecked event
                if (cb != checkedBox && cb.IsChecked == true)
                {
                    cb.IsChecked = false;
                }
            }

            // 2. Show the current panel
            UpdatePanelVisibility(checkedBox, Visibility.Visible);
        }

        // NEW: Handler when a CheckBox is unchecked (hides panel)
        private void AssociatedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox uncheckedBox = (CheckBox)sender;

            // 1. Hide the panel
            UpdatePanelVisibility(uncheckedBox, Visibility.Collapsed);
        }

        // NEW: Helper method to link the CheckBox to its corresponding StackPanel and set visibility
        private void UpdatePanelVisibility(CheckBox checkBox, Visibility visibility)
        {
            StackPanel? targetPanel = null;

            if (checkBox == DRCheckBox)
            {
                targetPanel = DRSubCommentPanel;
            }
            else if (checkBox == IssueCheckBox)
            {
                targetPanel = IssueSubCommentPanel;
            }
            else if (checkBox == RedLineCheckBox)
            {
                targetPanel = RedLineSubCommentPanel;
            }

            if (targetPanel != null)
            {
                targetPanel.Visibility = visibility;
            }
        }

       
    }
}
