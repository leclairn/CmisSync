using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conf
{
    [Flags]
    public enum TypeSynchro
    {
        Up = 1,
        Down = 2,
        Bidirectionnal = 4,
        Deletion = 8
    }
}
