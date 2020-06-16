using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeconMoon_s_WiiVC_Injector
{
    class BuildStep
    {
        public delegate bool BuildAction();

        public BuildAction Action { get; set; }
        public string Description { get; set; }
        public int ProgressWeight { get; set; }

        public bool Execute()
        {
            return Action();
        }
    }
}
