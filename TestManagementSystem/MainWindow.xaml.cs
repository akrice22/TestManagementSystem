// MainWindow.xaml.cs

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;


namespace TestManagementSystem
{
   

    // Data Model for RedLines Dashboard for the "RedLines" button
    public class RedLineItemView
    {
        public int RedLineNumber { get; set; }
        public int TestNumber { get; set; }
        public string TestTitle { get; set; }
        public DateTime DateOpened { get; set; }
        public string DateClosed { get; set; }
        public string Document { get; set; }
        public string PageNumber { get; set; }
        public string Section { get; set; }
        public bool IsResolved { get; set; }
        public List<SubCommentModel> Comments { get; set; } = new List<SubCommentModel>();
    }

    // Data model for "Issues Dashboard" for "Issues" button
    public class IssueItemView
    {
        public int IssueNumber { get; set; }
        public int TestNumber { get; set; }
        public string TestTitle { get; set; }
        public DateTime DateOpened { get; set; }
        public string DateClosedOrMoved { get; set; }
        public string Status { get; set; }
        // Assumes SubCommentModel is accessible (it should be if TestModel.cs is linked)
        public List<SubCommentModel> Comments { get; set; } = new List<SubCommentModel>();
    }
    // Data model for "DR Dhasboard" for "DRs" button
    public class DRItemView
    {
        public int DRNumber { get; set; }
        public int TestNumber { get; set; }
        public string TestTitle { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public DateTime DateOpened { get; set; }
        public string DateClosedOrMoved { get; set; }
        public string OriginatingIssueStatus { get; set; } // Shows if it came from a "Moved to DR" issue

        public List<CommentModel> History { get; set; } = new List<CommentModel>();
        public string LatestCommentText => History.LastOrDefault()?.SubComments.LastOrDefault()?.Text ?? "[No updates]";

        // NEW
        public string LastUpdatedDisplay
        {
            get
            {
                // Find the latest comment (which holds the EditHistory)
                var lastComment = History.LastOrDefault();
                if (lastComment != null && lastComment.EditHistory.Any())
                {
                    // Get the last item in the EditHistory (which is the latest update time)
                    DateTime lastUpdate = lastComment.EditHistory.Last();
                    return $"Last Updated: {lastUpdate:yyyy-MM-dd HH:mm:ss}";
                }
                // If there's a comment but no explicit edit history, use the creation date.
                if (lastComment != null)
                {
                    return $"Last Updated: {lastComment.CreatedTimestamp:yyyy-MM-dd HH:mm:ss}";
                }
                return "Last Updated: N/A";
            }
        }

    }

    // Helper class for the Test DataGrid view. Provides properties for the DataGrid to bind to
    public class TestListView
    {
        public int TestNumber { get; set; }
        public string TestTitle { get; set; }
        public string TestType { get; set; }
        public DateTime DateStart { get; set; }
        public bool IsFinished { get; set; }
        public int CommentCount { get; set; }


        // counts for associate items
        public int DRCount { get; set; }
        public int IssueCount { get; set; }
        public int RedLineCount { get; set; }
        // Helper properties for the DataGrid Status Column
        public string StatusText => IsFinished ? "Ended" : "In Progress";
        public SolidColorBrush StatusColorBrush => IsFinished
             ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935")) // Red for ended
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // green for in progress

    }

    public class ConsolidatedComment
    {
        public CommentModel Comment { get; set; }
        public TestModel Test { get; set; }
        public int GlobalIssueNumber { get; set; } = 0;
        public int GlobalDRNumber { get; set; } = 0;
    }

    // NEW********** for trying to get Filters on All Tests Content area
    public static class VisualTreeExtensions
    {
        public static T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // 1. Check if the current child is the control we're looking for
                if (child is T typedChild && child is FrameworkElement fe && fe.Name == name)
                {
                    return typedChild;
                }

                // 2. Recursively search children
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
    }

    public partial class MainWindow : Window
    {
        private DispatcherTimer timer = new DispatcherTimer();
        private readonly string lastAccessedKey = "LastAccessedTime";
        private const string TestDataFile = "tests.json"; // Constant for the data file name
        private int _nextTestNumber;

        public int NextTestNumber
        {
            get { return _nextTestNumber; }
            set { _nextTestNumber = value; }
        }

        // Central collection for the Dashboard to enable filtering
        private ObservableCollection<TestListView> _allTests = new ObservableCollection<TestListView>();
        private ICollectionView? _testsView;

        // FILTER STATE FIELDS
        private string _statusFilter = "All";
        private string _typeFilter = "All";
        private DateTime? _dateFilter = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
            LoadLastAccessedTime();
            //DisplayAllTests();

            // Call method to load dynamic summary data and set the DataContext
            CalculateAndSetSummaryData();


            DashboardContentArea.ContentTemplate = (DataTemplate)this.FindResource("SummaryDashboardTemplate");
        }



        private void InitializeTimer()
        {
            // Set up a timer to update the current time every second
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        // Timer_Tick method signature fixed to address the nullability warning.
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update the current time display in the footer
            CurrentTimeTextBlock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void LoadLastAccessedTime()
        {
            // In a real application, we will use JSON file I/O for persistent storage.
            // this is just simulated for a last access time for now.

            DateTime simulatedLastAccess = DateTime.Now.Subtract(TimeSpan.FromDays(2)).Subtract(TimeSpan.FromHours(5));
            LastAccessedTimeTextBlock.Text = simulatedLastAccess.ToString("yyyy-MM-dd HH:mm:ss");
            // TODO: In Part 2, implement file I/O here to read the last saved time from a JSON file 
            // and then save the current time for the next session.
        }

        // Method to switch back to the Summary view
        private void GoToSummaryDashboardClick(object sender, RoutedEventArgs e)
        {
            CalculateAndSetSummaryData();
            DashboardContentArea.ContentTemplate = (DataTemplate)this.FindResource("SummaryDashboardTemplate");
            DashboardContentArea.Content = this.DataContext;
        }

        // Load all TestModel objects from the JSON file
        private List<TestModel> LoadAllTests()
        {
            if (File.Exists(TestDataFile))
            {
                try
                {
                    string jsonString = File.ReadAllText(TestDataFile);
                    return JsonSerializer.Deserialize<List<TestModel>>(jsonString) ?? new List<TestModel>();
                    

                }
                catch(Exception ex)
                {
                    MessageBox.Show($"Error loading test data: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new List<TestModel>();
                }
            }
            return new List<TestModel>();
        }

        
        private void UpdateNextTestNumber()
        {
            List<TestModel> allTests = LoadAllTests(); // Ensure this loads the current state
            _nextTestNumber = allTests.Any() ? allTests.Max(t => t.TestNumber) + 1 : 1;
        }

        // Method to calculate all open item totals and set the DataContext
        private void CalculateAndSetSummaryData()
        {
            List<TestModel> allTests = LoadAllTests();

            // Flatten all comments from all tests into a single list
            var allComments = allTests.SelectMany(t => t.Comments).ToList();

            // 1. DR Calculations
            var openDRs = allComments.Where(c => c.DrStatus != null && !c.DrStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)).ToList();
            var p1Count = openDRs.Count(c => c.DrPriority != null && c.DrPriority.Equals("P1", StringComparison.OrdinalIgnoreCase));
            var p2Count = openDRs.Count(c => c.DrPriority != null && c.DrPriority.Equals("P2", StringComparison.OrdinalIgnoreCase));
            var p345Count = openDRs.Count(c => c.DrPriority != null &&
                                               !c.DrPriority.Equals("P1", StringComparison.OrdinalIgnoreCase) &&
                                               !c.DrPriority.Equals("P2", StringComparison.OrdinalIgnoreCase));

            // 2. Issue Calculations
            var openIssues = allComments.Where(c => c.IssueStatus != null && !c.IssueStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)).ToList();
            var trackCount = openIssues.Count(c => c.IssueStatus != null && c.IssueStatus.Equals("Track", StringComparison.OrdinalIgnoreCase));
            var researchCount = openIssues.Count(c => c.IssueStatus != null && c.IssueStatus.Equals("Research", StringComparison.OrdinalIgnoreCase));

            // 3. RedLine Calculations
            var openRedLines = allComments.Where(c => c.RedLineDetails != null && !c.IsRedLineResolved).ToList();

            // create new DataContext object
            this.DataContext = new DashboardSummaryData
            {
                OpenDRsTotal = openDRs.Count,
                Priority1DRs = $"P1 Critical: {p1Count}",
                Priority2DRs = $"P2 High: {p2Count}",
                Priority345DRs = $"... (P3, P4, P5 details: {p345Count})",

                OpenIssuesTotal = openIssues.Count,
                TrackIssues = $"Tracked: {trackCount}",
                ResearchIssues = $"Research: {researchCount}",

                OpenRedlinesTotal = openRedLines.Count
            };
        }

        

        // Method to display all tests in the DataGrid view
        private void DisplayAllTests()
        {
            List<TestModel> allTests = LoadAllTests();

            _allTests.Clear();
            foreach (var t in allTests.OrderByDescending(t => t.TestNumber))
            {
                _allTests.Add(new TestListView
                {
                    TestNumber = t.TestNumber,
                    TestTitle = t.TestTitle,
                    TestType = t.TestType,
                    DateStart = t.DateStart,
                    IsFinished = t.IsFinished,
                    CommentCount = t.Comments.Count,
                    // Calculate counts for associated items
                    DRCount = t.Comments.Count(c => c.DrPriority != null),
                    IssueCount = t.Comments.Count(c => c.IssueStatus != null),
                    RedLineCount = t.Comments.Count(c => c.RedLineDetails != null)
                });
            }

            // Switch the content to the All Tests DataGrid view
            DashboardContentArea.ContentTemplate = (DataTemplate)this.FindResource("AllTestsDashboardTemplate");
            DashboardContentArea.Content = _allTests;

            // Set up the CollectionView after seeting the content
            _testsView = CollectionViewSource.GetDefaultView(DashboardContentArea.Content);
            if(_testsView != null)
            {
                _testsView.Filter = new Predicate<object>(FilterTestList);
            }

            FilterDataGrid(this, new RoutedEventArgs());
            /* Dispatcher.BeginInvoke(new Action(() => {
                FilterDataGrid(this, new RoutedEventArgs());
            }), DispatcherPriority.Loaded);*/

        }

        private List<ConsolidatedComment> GetConsolidatedComments()
        {
            List<TestModel> allTests = LoadAllTests();
            List<ConsolidatedComment> consolidated = new List<ConsolidatedComment>();

            // Pass 1: Create Consolidated list
            foreach (var test in allTests)
            {
                foreach (var comment in test.Comments)
                {
                    consolidated.Add(new ConsolidatedComment { Comment = comment, Test = test });
                }
            }

            // Pass 2: Assign global Issue and DR numbers
            int issueCounter = 1;
            int drCounter = 1;

            // Sort by creation date to ensure consistent numbering
            foreach (var cc in consolidated.OrderBy(c => c.Comment.CreatedTimestamp))
            {
                if (cc.Comment.IssueStatus != null)
                {
                    cc.GlobalIssueNumber = issueCounter++;
                }
                // An item is a DR if it has DR status OR was converted from an Issue.
                if (cc.Comment.DrStatus != null || cc.Comment.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == true)
                {
                    cc.GlobalDRNumber = drCounter++;
                }
            }

            return consolidated;
        }

        // used when clicked the Issues button on the MainPage
        private void ShowIssuesDashboard()
        {
            //List<TestModel> allTests = LoadAllTests();
            List<IssueItemView> allIssues = new List<IssueItemView>();
         
            List<ConsolidatedComment> consolidated = GetConsolidatedComments();

            //Dictionary<CommentModel, int> issueMap = new Dictionary<CommentModel, int>();

            foreach(var cc in consolidated.Where(c => c.Comment.IssueStatus != null))
            {
                string status = cc.Comment.IssueStatus ?? "N/A";
                string dateClosedOrMoved = string.Empty;

                bool wasMovedToDR = status.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase);

                if (status.Equals("Closed", StringComparison.OrdinalIgnoreCase) || wasMovedToDR)
                {
                    // Use ConversionTimestamp for closed/moved items, otherwise use last edit/creation
                    dateClosedOrMoved = (cc.Comment.ConversionTimestamp ?? cc.Comment.EditHistory.LastOrDefault()).ToString("yyyy-MM-dd HH:mm");

                    // If the Issue was moved to a DR, find the corresponding DR's global number
                    if (wasMovedToDR)
                    {
                        // Find the corresponding DR item (same creation timestamp)
                        var correspondingDR = consolidated.FirstOrDefault(d =>
                            d.Comment.DrStatus != null &&
                            d.Comment.CreatedTimestamp == cc.Comment.CreatedTimestamp);

                        if (correspondingDR != null)
                        {
                            // Update the status string to display the link on the Issue Dashboard
                            status = $"Closed/Moved to DR-{correspondingDR.GlobalDRNumber}";
                        }
                        else
                        {
                            status = "Closed/Moved to DR-N/A"; // Fallback if link is broken
                        }
                    }
                    // If status is just "Closed", keep it as is.
                }

                allIssues.Add(new IssueItemView
                {
                    IssueNumber = cc.GlobalIssueNumber,
                    TestNumber = cc.Test.TestNumber,
                    TestTitle = cc.Test.TestTitle,
                    DateOpened = cc.Comment.CreatedTimestamp,
                    DateClosedOrMoved = dateClosedOrMoved,
                    Status = status, // This now holds the formatted status/link
                    Comments = cc.Comment.SubComments.OrderBy(sc => sc.Timestamp).ToList()
                });
            }

            IssueDashboardWindow issuesWindow = new IssueDashboardWindow(allIssues);
            issuesWindow.ShowDialog();

            CalculateAndSetSummaryData();

           
        }

        // To Display the DRs Dashboard
        // Method to prepare and display the DRs Dashboard
        private void ShowDRsDashboard()
        {
            List<ConsolidatedComment> consolidated = GetConsolidatedComments();
            List<DRItemView> allDRs = new List<DRItemView>();

            var drThreads = consolidated
                   .Where(cc => cc.GlobalDRNumber > 0) // Only look at items that became a DR
                   .GroupBy(cc => cc.Comment.CreatedTimestamp)
                   .OrderBy(g => g.First().GlobalDRNumber); // Order by the global DR number for consistent display
            foreach (var thread in drThreads)
            {
                // The first element gives the original DR creation details and the global number.
                var originalCc = thread.First();
                int globalDrNumber = originalCc.GlobalDRNumber;

                // The latest element gives the most recent status, priority, and sub-comments.
                var latestCc = thread.OrderByDescending(cc => cc.Comment.EditHistory.LastOrDefault()).First();
                var latestComment = latestCc.Comment;
                var parentTest = latestCc.Test;

                // Extract latest status details
                bool isMovedFromIssue = latestComment.IssueStatus?.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) == true;
                string status = latestComment.DrStatus ?? "Open";
                string priority = latestComment.DrPriority ?? "P3";
                string dateClosed = string.Empty;

                if (status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                {
                    dateClosed = latestComment.EditHistory.LastOrDefault().ToString("yyyy-MM-dd HH:mm");
                }

                // Determine Originating Issue Status
                string originNote = string.Empty;
                if (isMovedFromIssue)
                {
                    // Find the global Issue number corresponding to this thread
                    var originatingIssue = consolidated.FirstOrDefault(c =>
                        c.Comment.CreatedTimestamp == latestComment.CreatedTimestamp &&
                        c.GlobalIssueNumber > 0);

                    string issueNum = originatingIssue != null ? $"Issue-{originatingIssue.GlobalIssueNumber}" : "Issue-N/A";
                    string convTime = latestComment.ConversionTimestamp.HasValue
                        ? latestComment.ConversionTimestamp.Value.ToString("yyyy-MM-dd HH:mm")
                        : "N/A";

                    originNote = $"From {issueNum} on {convTime}";
                }

                allDRs.Add(new DRItemView
                {
                    DRNumber = globalDrNumber, // Global Numbering
                    TestNumber = parentTest.TestNumber,
                    TestTitle = parentTest.TestTitle,
                    Priority = priority,
                    Status = status,
                    DateOpened = originalCc.Comment.CreatedTimestamp,
                    DateClosedOrMoved = dateClosed,
                    OriginatingIssueStatus = originNote,

                    // Collect the full history of CommentModel objects for the RowDetails
                    History = thread.Select(cc => cc.Comment).ToList()
                });
            }

            DRDashboardWindow drWindow = new DRDashboardWindow(allDRs);
            drWindow.ShowDialog();

            CalculateAndSetSummaryData();

        }

       

        // Logic for filtering the ICollectionview
        private bool FilterTestList(object item)
        {
            if (item is not TestListView test) return false;

            // 1. Status Filter (Open/Closed)
            if (_statusFilter == "Open" && test.IsFinished) return false;
            if (_statusFilter == "Closed" && !test.IsFinished) return false;

            // 2. Type Filter
            if (_typeFilter != "All" && !test.TestType.Equals(_typeFilter, StringComparison.OrdinalIgnoreCase)) return false;

            // 3. Date Filter (After a certain date)
            if (_dateFilter.HasValue && test.DateStart.Date < _dateFilter.Value.Date) return false;

            return true;
        }

        // Event handler to apply filters
        private void FilterDataGrid(object sender, object e)
        {
            // if (VisualTreeHelper.GetChildrenCount(DashboardContentArea) == 0) return;
            // FrameworkElement? visualRoot = VisualTreeHelper.GetChild(DashboardContentArea, 0) as FrameworkElement;

            // if (visualRoot == null) return;

            // Find filter controls by name on template
            /* ComboBox? statusCombo = visualRoot.FindName("StatusFilterComboBox") as ComboBox;
             ComboBox? typeCombo = visualRoot.FindName("TypeFilterComboBox") as ComboBox;
             DatePicker? datePicker = visualRoot.FindName("DateFilterPicker") as DatePicker;*/

            ComboBox? statusCombo = VisualTreeExtensions.FindVisualChild<ComboBox>(DashboardContentArea, "StatusFilterComboBox");
            ComboBox? typeCombo = VisualTreeExtensions.FindVisualChild<ComboBox>(DashboardContentArea, "TypeFilterComboBox");
            DatePicker? datePicker = VisualTreeExtensions.FindVisualChild<DatePicker>(DashboardContentArea, "DateFilterPicker");



            // Only proceed if all controls are successfully found
            if (statusCombo != null && typeCombo != null && datePicker != null)
            {
                // Update the private fields based on current control values
                _statusFilter = (statusCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
                _typeFilter = (typeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
                _dateFilter = datePicker.SelectedDate;

                // Refresh the view, which triggers FilterTestList to use the new values
                if (_testsView != null)
                {
                    _testsView.Refresh();
                }
            }

            // Fallback for "Clear Filters" button if controls are  not yet fully realized
            else if (sender is Button button && button.Content.ToString() == "Clear Filters")
            {
                _statusFilter = "All";
                _typeFilter = "All";
                _dateFilter = null;
                if (_testsView != null)
                {
                    _testsView.Refresh();
                }
            }
        }

        // Clear Filters Button Handler
        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            // Find controls using FindName on the ContentControl
            if (DashboardContentArea.FindName("StatusFilterComboBox") is ComboBox statusCombo)
                statusCombo.SelectedIndex = 0;
            if (DashboardContentArea.FindName("TypeFilterComboBox") is ComboBox typeCombo)
                typeCombo.SelectedIndex = 0;
            if (DashboardContentArea.FindName("DateFilterPicker") is DatePicker datePicker)
                datePicker.SelectedDate = null;

            // Apply the filter refresh by calling the main filter method
            FilterDataGrid(sender, e);
        }

        // Delete Test Button Handler
        private void DeleteTestButton_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;

            if (button?.DataContext is TestModel testToDelete)
            {
                MessageBoxResult result = MessageBox.Show($"Are you sure you want to delete Test T-{testToDelete.TestNumber:D3} - {testToDelete.TestTitle}?",
                                                          "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    if (DeleteTestFromJSON(testToDelete.TestNumber))
                    {
                        MessageBox.Show($"Test T-{testToDelete.TestNumber:D3} deleted successfully and remaining tests re-sequenced.", "Deletion Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        // The DeleteTestFromJSON method already calls LoadTestsDisplay() and UpdateNextTestNumber()
                        // So no further refresh is needed here.
                    }
                    else
                    {
                        MessageBox.Show($"Failed to delete Test T-{testToDelete.TestNumber:D3}.", "Deletion Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
               
            }
        }

        // Helper method to delete a test record from the JSON file
        private bool DeleteTestFromJSON(int testNumberToDelete)
        {
            try
            {
                List<TestModel> allTests = LoadAllTests();
                int initialCount = allTests.Count;

                // Find and remove the test
                
                var testToRemove = allTests.FirstOrDefault(t => t.TestNumber == testNumberToDelete);
                if (testToRemove != null)
                {
                    allTests.Remove(testToRemove);
                }
                else
                {
                    return false; // Test not found
                }

                // 2. Re-sequence remaining tests. re-sort list just in case then update TestNumber
                allTests = allTests.OrderBy(t => t.TestNumber).ToList();

                for(int i = 0; i < allTests.Count; i++)
                {
                    // test number becomes the index + 1
                    allTests[i].TestNumber = i + 1;
                }

                //3. Save updated list back to the JSON file
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJsonString = JsonSerializer.Serialize(allTests, options);
                File.WriteAllText(TestDataFile, updatedJsonString);

                UpdateNextTestNumber();

                // 4. Recalculate summary data after deletion
                DisplayAllTests();
                CalculateAndSetSummaryData();

                return initialCount > allTests.Count;
               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during file deletion: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ShowRedLinesDashboard()
        {
            List<TestModel> allTests = LoadAllTests();
            List<RedLineItemView> allRedLines = new List<RedLineItemView>();
            int redLineCounter = 1;

            foreach(var test in allTests)
            {
                var redLineComments = test.Comments
                    .Where(c => c.RedLineDetails != null)
                    .OrderBy(c => c.CreatedTimestamp);

                foreach(var comment in redLineComments)
                {
                    string dateClosed = string.Empty;
                    if (comment.IsRedLineResolved)
                    {
                        dateClosed = comment.EditHistory.LastOrDefault().ToString("yyyy-MM-dd HH:mm");
                    }

                    var details = comment.RedLineDetails;

                    allRedLines.Add(new RedLineItemView
                    {
                        RedLineNumber = redLineCounter++,
                        TestNumber = test.TestNumber,
                        TestTitle = test.TestTitle,
                        DateOpened = comment.CreatedTimestamp,
                        DateClosed = dateClosed,
                        Document = details.Document ?? "N/A",
                        PageNumber = details.PageNumber ?? "N/A",
                        Section = details.Section ?? "N/A",
                        IsResolved = comment.IsRedLineResolved,
                        Comments = comment.SubComments.OrderBy(sc => sc.Timestamp).ToList()
                    });
                }
            }

            RedLineDashboardWindow redLineWindow = new RedLineDashboardWindow(allRedLines, allTests);
            redLineWindow.ShowDialog();

            CalculateAndSetSummaryData();
        }

       

        // Placeholder click event for Navigation Buttons
        private void OnNavButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string navTarget)
            {
                if(navTarget == "Dashboard")
                {
                    // switch to the All Tests view when "Testing Dashboard" is clicked
                    DisplayAllTests();

                    // Ensure visual tree is built and then apply filters
                    DashboardContentArea.UpdateLayout();
                    FilterDataGrid(this, new RoutedEventArgs());
                }
                else if(navTarget == "Issues")
                {
                    ShowIssuesDashboard();
                } // Test View for DRs button
                else if(navTarget == "DRs")
                {
                    ShowDRsDashboard(); 
                }
                else if(navTarget == "RedLines")
                {
                    ShowRedLinesDashboard();
                }
                else
                {
                    MessageBox.Show($"Navigation Placeholder: Going to the '{navTarget}' view. This will be implemented in a later step.",
                                    "Navigation Clicked",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
        }

        // Event handler for double-clicking a test row in the DataGrid
        private void AllTestsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ensure the sender is a DataGrid and an item is selected
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is TestListView selectedTestView)
            {
                // Find the full TestModel from the saved file to pass to the details window
                List<TestModel> allTests = LoadAllTests();
                TestModel? fullTestData = allTests.FirstOrDefault(t => t.TestNumber == selectedTestView.TestNumber);

                if (fullTestData != null)
                {
                    // Open the TestDetails window with the selected test data
                    TestDetailsWindow detailsWindow = new TestDetailsWindow(fullTestData);
                    detailsWindow.ShowDialog();

                    // After the details window closes, refresh the test list to reflect any changes (e.g., test ended, comment added)
                    DisplayAllTests();
                }
                else
                {
                    MessageBox.Show("Could not load full test details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        
        // NEW*******Event handler for the "Export to CSV" button
        private void ExportToCsvClick(object sender, RoutedEventArgs e)
        {
            ExportFilteredTestsToCsv();
        }
        
        // NEW******** method to export currently filtered tests to a detailed CSV file
        private void ExportFilteredTestsToCsv()
        {
            if(_testsView == null || !_testsView.OfType<TestListView>().Any())
            {
                MessageBox.Show("No tests are currently displayed or filtered to export.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Prepare Save Dialog
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"TestExport_{DateTime.Now:yyyyMMdd_HHmm}",
                DefaultExt = ".csv",
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            string filePath = dlg.FileName;

            try
            {
                // Load all TestModels once for efficient access to full data
                List<TestModel> allTests = LoadAllTests();
                StringBuilder csvContent = new StringBuilder();

                // 2. Define CSV Header (Detailed flattening of test and comment data)
                csvContent.Append("TestNumber,TestTitle,TestType,DateStart,DateEnd,LoadNumber,System,TestTypeDetail,IsFinished,DevicesTested,");
                csvContent.Append("CommentNumber,CommentTitle,CommentDescription,CreatedTimestamp,LastUpdatedTimestamp,");
                csvContent.Append("Type,Status,Priority,Document,PageNumber,Section,SubComments\n");

                // 3. Iterate over the currently FILTERED view
                foreach (TestListView testListView in _testsView.OfType<TestListView>())
                {
                    // Find the full TestModel data
                    TestModel? fullTest = allTests.FirstOrDefault(t => t.TestNumber == testListView.TestNumber);

                    if (fullTest == null) continue;

                    // Base test details (will be repeated for every comment)
                    string baseTestDetails =
                        $"{fullTest.TestNumber:D3}," +
                        $"{CsvEscape(fullTest.TestTitle)}," +
                        $"{CsvEscape(fullTest.TestType)}," +
                        $"{fullTest.DateStart:yyyy-MM-dd HH:mm:ss}," +
                        $"{fullTest.DateEnd:yyyy-MM-dd HH:mm:ss}," +
                        $"{CsvEscape(fullTest.LoadNumber)}," +
                        $"{CsvEscape(fullTest.System)}," +
                        $"{CsvEscape(fullTest.TestTypeDetail)}," +
                        $"{fullTest.IsFinished}," +
                        $"{CsvEscape(string.Join("|", fullTest.DevicesTested))},";

                    // 4. Iterate over comments and flatten
                    if (!fullTest.Comments.Any())
                    {
                        // Add row for tests with no comments
                        csvContent.AppendLine(baseTestDetails + ",,,,," + ",,,-,-,");
                        continue;
                    }

                    foreach (CommentModel comment in fullTest.Comments.OrderBy(c => c.CommentNumber))
                    {
                        StringBuilder row = new StringBuilder(baseTestDetails);

                        // Comment Details
                        row.Append($"{comment.CommentNumber},");
                        row.Append($"{CsvEscape(comment.Title)},");
                        row.Append($"{CsvEscape(comment.Description)},");
                        row.Append($"{comment.CreatedTimestamp:yyyy-MM-dd HH:mm:ss},");
                        // Get the latest timestamp from EditHistory or CreationTimestamp
                        row.Append($"{(comment.EditHistory.Any() ? comment.EditHistory.Last().ToString("yyyy-MM-dd HH:mm:ss") : comment.CreatedTimestamp.ToString("yyyy-MM-dd HH:mm:ss"))},");

                        // DR/Issue/RedLine Status Details
                        if (comment.DrStatus != null)
                        {
                            // DR
                            row.Append("DR,");
                            row.Append($"{CsvEscape(comment.DrStatus)},");
                            row.Append($"{CsvEscape(comment.DrPriority)},");
                            row.Append("-,,-,"); // Document, Page, Section not applicable
                        }
                        else if (comment.IssueStatus != null)
                        {
                            // Issue
                            row.Append("Issue,");
                            string statusDisplay = comment.IssueStatus;
                            if (statusDisplay.Equals("Moved to DR", StringComparison.OrdinalIgnoreCase) && comment.ConversionTimestamp.HasValue)
                            {
                                statusDisplay = $"Moved to DR (Converted {comment.ConversionTimestamp.Value:yyyy-MM-dd HH:mm:ss})";
                            }
                            row.Append($"{CsvEscape(statusDisplay)},");
                            row.Append("-,"); // Priority not applicable
                            row.Append("-,,-,"); // Document, Page, Section not applicable
                        }
                        else if (comment.RedLineDetails != null)
                        {
                            // RedLine
                            row.Append("RedLine,");
                            row.Append($"{(comment.IsRedLineResolved ? "Resolved" : "Open")},");
                            row.Append("-,"); // Priority not applicable
                            row.Append($"{CsvEscape(comment.RedLineDetails.Document)},");
                            row.Append($"{CsvEscape(comment.RedLineDetails.PageNumber)},");
                            row.Append($"{CsvEscape(comment.RedLineDetails.Section)},");
                        }
                        else
                        {
                            // Plain Comment
                            row.Append("Comment,");
                            row.Append("-,,-,-,-,");
                        }

                        // SubComments: Joined and escaped for a single CSV field
                        string subComments = string.Join(" | ", comment.SubComments.Select(sc => $"[{sc.Timestamp:yyyy-MM-dd HH:mm:ss}] {sc.Text}"));
                        row.Append($"{CsvEscape(subComments)}");

                        csvContent.AppendLine(row.ToString());
                    }

                }

                // 5. Write to file
                File.WriteAllText(filePath, csvContent.ToString(), Encoding.UTF8);
                MessageBox.Show($"Successfully exported {(_testsView.OfType<TestListView>().Count())} tests to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch(Exception ex)
            {
                MessageBox.Show($"An error occurred during CSV export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW*********** helper to safely escape strings for CSV output
        private string CsvEscape(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Replace double quotes with two double quotes
            string escaped = input.Replace("\"", "\"\"");

            // If the string contains comma, newline, or double quotes, enclose it in double quotes
            if (escaped.Contains(',') || escaped.Contains('\n') || input.Contains('"'))
            {
                return $"\"{escaped}\"";
            }

            return escaped;
        }

       
        // Placeholder click event for "Start New Test" Button
        private void OnStartNewTestClick(object sender, RoutedEventArgs e)
        {
            // Open the new initial test detail window
            InitialTestDetail initialDetailWindow = new InitialTestDetail();
            initialDetailWindow.ShowDialog();

            // After adding a new test, refresh the dashboard view if it's currently showing all tests
            if(DashboardContentArea.ContentTemplate == (DataTemplate)this.FindResource("AllTestsDashboardTemplate"))
            {
                DisplayAllTests();
            }
           
        }

        // Handles the click for the 'i' button in header
        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("AH-64 Test Management App\n\nVersion 1.0\nDeveloped for efficient test data tracking.", "Application Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }


}
