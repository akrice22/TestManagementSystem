using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace TestManagementSystem

    // class is for data that is displayed on the start page
{
    public class DashboardSummaryData
    {
        // DR totals
        public int OpenDRsTotal { get; set; }
        public string Priority1DRs { get; set; } = "P1 Critical: 0";
        public string Priority2DRs { get; set; } = "P2 High: 0";
        public string Priority345DRs { get; set; } = "...(P3, P4, P5 details: 0";

        //Issues totals
        public int OpenIssuesTotal { get; set; }
        public string TrackIssues { get; set; } = "Tracked: 0";
        public string ResearchIssues { get; set; } = "Research: 0";

        // RedLine Totals
        public int OpenRedlinesTotal { get; set; }
    }
}
