using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RamjetAnvil.RamNet {
    public interface IObjectMessageCache {
        T GetInstance<T>() where T : IObjectMessage;
    }
}
