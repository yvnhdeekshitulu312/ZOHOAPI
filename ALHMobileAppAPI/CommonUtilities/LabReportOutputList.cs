using System.Collections.Generic;

namespace CommanUtilities.Models
{
    
    public class LabReportOutputListN
    {       
        public long TESTORDERITEMID { get; set; }
        public string TESTNAME { get; set; }
        public int PARAMETERID { get; set; }
        public string PARAMETERNAME { get; set; }
        public string VALUE { get; set; }
        public string UNITS { get; set; }
        public string REFERENCERANGE { get; set; }
        public string ISABNORMAL { get; set; }
        public string ISPANIC { get; set; }
        public string PARAMETERTYPE { get; set; }
       // public string FTPFILENAME { get; set; }



    }
    public class LabReportOutputList : Base
    {
        List<LabReportOutputListN> objLabReportN = new List<LabReportOutputListN>();
        public string FTPFILENAME { get; set; }
        public string FTPPATH { get; set; }
        public List<LabReportOutputListN> objLabReportNList { get { return objLabReportN; } set { objLabReportN = value; } }

    }

}
