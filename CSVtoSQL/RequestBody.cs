using System;
using System.Collections.Generic;
using System.Text;

namespace CSVtoSQL
{
    public class RequestBody
    {
        public string FileName { get; set; }
        public string FileLocation { get; set; }
        public string TableName { get; set; }
        public List<ColumnMapping> ColumnMappings { get; set; }
    }

    public class ColumnMapping
    {
        public string Source { get; set; }
        public string Destination { get; set; }
    }
}
