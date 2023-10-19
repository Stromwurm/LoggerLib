using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logger
{
    public readonly struct Criticality
    {
        public readonly string Info = "Info";
        public readonly string Minor = "Minor";
        public readonly string Major = "Major";
        public readonly string Critical = "Critical";

        public Criticality()
        {
            
        }
    }
}
