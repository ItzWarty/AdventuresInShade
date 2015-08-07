using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Toolkit;

namespace Shade {
   public class DebugClickableBox : SceneElement {
      private readonly MouseEventBus mouseEventBus;
      private OrientedBoundingBox bounds;
      private bool toggle = false;

      public DebugClickableBox(MouseEventBus mouseEventBus) {
         this.mouseEventBus = mouseEventBus;
      }

      public override OrientedBoundingBox Bounds => bounds;

      public void Initialize() {
         bounds = new OrientedBoundingBox(-Vector3.One / 2, Vector3.One / 2);
         bounds.Translate(new Vector3(0, 0, 2));
      }

      public override void Step(GameTime gameTime) {
      }

      public override void Render(Renderer renderer) {
         var x = toggle ? 1 : 0;
         renderer.DrawOrientedBoundingBox(Bounds, new Vector4(1 - x, 1, 1 - x, 1));
      }

      public override void HandlePickRay(SceneMouseEventInfo e) {
         if (e.Type == MouseEventType.Down && e.Button == MouseButtons.Left && e.Rank == 0) {
            Console.WriteLine("BOX! " + e.Rank);
            toggle = !toggle;
         }
      }
   }
}
