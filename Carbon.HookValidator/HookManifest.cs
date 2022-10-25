using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carbon
{
    public class HookManifest
    {
        public List<Hook> Hooks { get; set; } = new List<Hook> ();

        public class Hook
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public string Type { get; set; }

            public List<HookParameter> Parameters { get; set; } = new List<HookParameter> ();
            public List<string> Info { get; set; } = new List<string> ();

            public HookPatch Patch { get; set; }
            public List<HookInstruction> Instructions { get; set; } = new List<HookInstruction> ();

            public class HookParameter
            {
                public string Name { get; set; }
                public string Type { get; set; }
            }
            public class HookPatch
            {
                public string Type { get; set; }
                public string Method { get; set; }
                public bool UseExactParameters { get; set; }
                public string [] ParameterTypes { get; set; }
            }
            public class HookInstruction
            {
                
            }
        }
    }
}
