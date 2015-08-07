using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItzWarty.Collections;
using SharpDX;

namespace Shade {
   public class DungeonKeyInventory {
      public IConcurrentSet<Vector4> Keys { get; set; } = new ConcurrentSet<Vector4>();
   }
}
