using SharpDX;
using System.Collections.Generic;

namespace Shade {
   public class NavigationMesh {
      private readonly IReadOnlyList<NavigationGridlet> gridlets;

      public NavigationMesh(IReadOnlyList<NavigationGridlet> gridlets) {
         this.gridlets = gridlets;
      }

      public List<Vector3> DebugPoints { get; set; } = new List<Vector3>();

      public void Initialize() {
      }
   }

   public class NavigationTriangle {
      public Vector3 A;
      public Vector3 B;
      public Vector3 C;
   }
}
