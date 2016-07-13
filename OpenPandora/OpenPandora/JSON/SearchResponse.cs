using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenPandora.JSON
{
    public class SearchResponse
    {
        public bool Success
        {
            get;
            set;
        }

        public string Error
        {
            get;
            set;
        }

        public bool PerfectMatchBookmark
        {
            get;
            set;
        }

        public Song[] Results
        {
            get;
            set;
        }
    }
}
