using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Toolkit.Graphics;

namespace Shade {
   public class Camera {
      private readonly GraphicsConfiguration graphicsConfiguration;
      private readonly MouseEventBus mouseEventBus;
      private readonly Character character;
      private float pitch = -MathUtil.PiOverFour;
      private float yaw = 0;
      private float desiredRadius = 60 * (float)Math.Sqrt(2);
      private float currentRadius = 60 * (float)Math.Sqrt(2);
      private bool isDragRotating = false;
      private int lastMouseX;
      private int lastMouseY;

      public Camera(GraphicsConfiguration graphicsConfiguration, MouseEventBus mouseEventBus, Character character) {
         this.graphicsConfiguration = graphicsConfiguration;
         this.mouseEventBus = mouseEventBus;
         this.character = character;
      }

      public Matrix View { get; private set; }
      public Matrix Projection { get; private set; }
      public ViewportF Viewport { get; private set; }

      public void Initialize() {
         mouseEventBus.Event += HandleMouseEvent;

         Viewport = new ViewportF(0, 0, graphicsConfiguration.Width, graphicsConfiguration.Height);
      }

      private void HandleMouseEvent(object sender, MouseEventInfo e) {
         switch (e.Type) {
            case MouseEventType.Wheel:
               Zoom(e.Delta);
               break;
            case MouseEventType.Down:
               if (e.Button.HasFlag(MouseButtons.Middle)) {
                  isDragRotating = true;
                  lastMouseX = e.X;
                  lastMouseY = e.Y;
               }
               break;
            case MouseEventType.Move:
               if (isDragRotating) {
                  var dx = e.X - lastMouseX;
                  var dy = e.Y - lastMouseY;
                  Drag(dx, dy);
                  lastMouseX = e.X;
                  lastMouseY = e.Y;
               }
               break;
            case MouseEventType.Up:
               if (e.Button.HasFlag(MouseButtons.Middle)) {
                  isDragRotating = false;
               }
               break;
         }
      }

      public void UpdatePrerender(GraphicsDevice graphicsDevice) {
         var r = new Vector4(0, -currentRadius, 0, 1.0f);
         var transform = Matrix.RotationX(pitch) * Matrix.RotationZ(yaw);
         Vector4 result;
         Vector4.Transform(ref r, ref transform, out result);

         View = Matrix.LookAtRH(new Vector3(result.X, result.Y, result.Z) + character.Position, character.Position, Vector3.UnitZ);
         Projection = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(60.0f), (float)graphicsDevice.BackBuffer.Width / graphicsDevice.BackBuffer.Height, 0.5f, 200.0f);

         currentRadius = currentRadius * 0.8f + desiredRadius * 0.2f;
      }

      public void Drag(int dx, int dy) {
         yaw += -dx * 0.01f;

         pitch += -dy * 0.01f;
         pitch = Math.Min(pitch, MathUtil.PiOverTwo * 0.9f);
         pitch = Math.Max(pitch, -MathUtil.PiOverTwo * 0.9f);
      }

      public void Zoom(int delta) {
         desiredRadius -= delta * 0.02f;
         desiredRadius = Math.Min(100, Math.Max(10, desiredRadius));
      }

      public Ray GetPickRay(int cursorX, int cursorY) {
         return Ray.GetPickRay(cursorX, cursorY, Viewport, View * Projection);
      }
   }
}
