using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Toolkit.Graphics;

namespace Shade {
   public class Camera {
      private readonly Character character;
      private float pitch = -MathUtil.PiOverFour;
      private float yaw = 0;
      private float desiredRadius = 60 * (float)Math.Sqrt(2);
      private float currentRadius = 60 * (float)Math.Sqrt(2);

      public Camera(Character character) {
         this.character = character;
      }

      public Matrix View { get; private set; }
      public Matrix Projection { get; private set; }

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
   }
}
