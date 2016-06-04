using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlData
{
    public interface IReading
    {
        int temperature { get; }

        int humidity { get; }
    }

    public class ReadingEH : IReading
    {
        public int temperature { get; set; }

        public int humidity { get; set; }
    }
}
