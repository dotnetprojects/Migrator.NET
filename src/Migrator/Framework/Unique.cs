using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Migrator.Framework
{
    public class Unique : IDbField
    {
        public string Name { get; set; }
        public string[] KeyColumns { get; set; }
    }
}
