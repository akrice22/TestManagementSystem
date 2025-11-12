// TestModel.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TestManagementSystem
{

    // Class to represent the structure of a single test record
    public class TestModel
    {
        public int TestNumber
        {
            get;
            set;
        }
        public string TestTitle
        {
            get; set;
        }
        public string TestType
        {
            get; set;
        }
        public DateTime DateStart
        {
            get; set;
        }
        public DateTime DateEnd
        {
            get; set;
        }
        public string Description
        {
            get; set;
        }
        public List<string> DevicesTested { get; set; } = new List<string>();
        // Property to hold comments
        public List<CommentModel> Comments { get; set; } = new List<CommentModel>();

        // property to track if the test event has finished
        public bool IsFinished { get; set; } = false;

        public string? LoadNumber { get; set; }
        public string? System { get; set; }
        public string TestTypeDetail { get; set; } = "Contractor"; // default value 

        public List<DRRecheckItem> RecheckItems { get; set; } = new List<DRRecheckItem>();
    }

    // represents a single DR included in a recheck test
    public class DRRecheckItem
    {
        public int GlobalDRNumber { get; set; }
        public int OriginatingTestNumber { get; set; }
        public DateTime DateCreated { get; set; }
        public string LatestStatus { get; set; }
        public string LatestPriority { get; set; }
        public string LatestDescription { get; set; } // The original DR's main description/comment
        // holds user's decision to close the DR NEW
        public bool IsDRClosedForRecheck { get; set; }

        // New comment specific to *this* recheck test
        public string? RecheckComment { get; set; }
        public DateTime? RecheckCommentTimestamp { get; set; }

        // The full history of the original DR's sub-comments for display
        public List<SubCommentModel> OriginalSubComments { get; set; } = new List<SubCommentModel>();
    }

    // class to represent a comment
    public class CommentModel
    {
        public int CommentNumber { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedTimestamp { get; set; }
        // Keep track of all modifications
        public List<DateTime> EditHistory { get; set; } = new List<DateTime>();

        // DR Details
        public string? DrPriority { get; set; } // e.g., "P1", "P5"
        public string? DrStatus { get; set; } // "Open", "Closed"
        public bool IsDRClosed => DrStatus?.Equals("Closed", StringComparison.OrdinalIgnoreCase) ?? false;

        // Issue Details
        public string? IssueStatus { get; set; } // "Open", "Closed", "Research", "Track", "Moved to DR"
        public bool IsIssueClosed => IssueStatus?.Equals("Closed", StringComparison.OrdinalIgnoreCase) ?? false;

        // RedLine Details
        public RedLineDetailModel? RedLineDetails { get; set; }
        public bool IsRedLineResolved { get; set; } = false;

        // New property to track DRRB history
        public List<string> DRRBsParticipated { get; set; } = new List<string>();

        // Fields for Issue to DR conversion
        public int DRNumberCreated { get; set; } = 0; // If this Issue created a DR, this is the global DR #
        public int IssueNumberOrigin { get; set; } = 0; // If this DR was created from an Issue, this is the global Issue #
        public DateTime? ConversionTimestamp { get; set; } // When the conversion happened

        // Nest comments (for DR, Issue, or RedLine tracking)
        public List<SubCommentModel> SubComments { get; set; } = new List<SubCommentModel>();
        
    }

   

   

    // Nested class for individual sub-comments/updates
    public class SubCommentModel
    {
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

    }

    // Nested class for RedLine details
    public class RedLineDetailModel
    {
        public string? Document { get; set; }
        public string? PageNumber { get; set; }
        public string? Section { get; set; } // Using "Section" for "Paragraph/Section"
    }
}

