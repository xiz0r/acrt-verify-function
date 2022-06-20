using System;
using System.Collections.Generic;

namespace AcrtVerify
{
    public class AcrtDto
    {
        public List<string> tags { get; set; }
        public string href { get; set; }
        public string relativeHref { get; set; }
        public string text { get; set; }
        public string title { get; set; }
        public string image { get; set; }
        public string publishDate { get; set; }
        public string _id { get; set; }
        public List<string> roles { get; set; }
        public List<string> sectors { get; set; }
        public DateTime formatDate { get; set; }
    }
}