using System.Linq;
using SharpDX;
using SharpDX.Toolkit;

namespace Shade {
   public class NavigationGridletElement : SceneElement {
      private readonly NavigationGridlet gridlet;

      public NavigationGridletElement(NavigationGridlet gridlet) {
         this.gridlet = gridlet;
      }

      public override OrientedBoundingBox Bounds => gridlet.OrientedBoundingBox;

      public override void Step(GameTime gameTime) { }

      public override void Render(Renderer renderer) {
//         var worldTransform = Matrix.Scaling(gridlet.XLength, gridlet.YLength, gridlet.Cells.Max(x => x.Height)) * gridlet.Orientation * Matrix.Translation(gridlet.X, gridlet.Y, gridlet.Z);
//         renderer.DrawCube(worldTransform, Vector4.One, false);
         renderer.DrawOrientedBoundingBox(gridlet.OrientedBoundingBox, new Vector4(1, 0, 0, 1));
         //         RenderNeighbors(renderer);
         //         RenderNavmesh(renderer);
//                  RenderDebugTileFlags(renderer);
      }

      private void RenderNavmesh(Renderer renderer) {
         var transformation = gridlet.OrientedBoundingBox.Transformation;
         foreach (var triangle in gridlet.Mesh) {
            for (var i = 0; i < 3; i++) {
               var aVect = new Vector3(triangle.Points[i].Xf, triangle.Points[i].Yf, 1);
               var bVect = new Vector3(triangle.Points[(i + 1) % 3].Xf, triangle.Points[(i + 1) % 3].Yf, 1);
               Vector3.Transform(ref aVect, ref transformation, out aVect);
               Vector3.Transform(ref bVect, ref transformation, out bVect);
               renderer.DrawDebugLine(
                  aVect,
                  bVect,
                  Color.Lime
                  );
            }
         }
      }

      private void RenderNeighbors(Renderer renderer) {
         foreach (var neighbor in gridlet.Neighbors) {
            renderer.DrawDebugLine(
               gridlet.OrientedBoundingBox.Center, neighbor.OrientedBoundingBox.Center, Color.Cyan
               );
         }
      }

      private void RenderDebugTileFlags(Renderer renderer) {
         for (var y = 0; y < gridlet.YLength; y++) {
            for (var x = 0; x < gridlet.XLength; x++) {
               var cellIndex = y * gridlet.XLength + x;
               var cellHeight = gridlet.Cells[cellIndex].Height;
               var cellFlags = gridlet.Cells[cellIndex].Flags;
               var transform = gridlet.Cells[cellIndex].OrientedBoundingBox.Transformation;
               Vector4 color;
               if (cellFlags.HasFlag(CellFlags.Connector)) {
                  var derp = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                  color = new Vector4(derp, derp / 2, 0, 1.0f);
               } else if (cellFlags.HasFlag(CellFlags.Debug)) {
                  var derp = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                  color = new Vector4(0.0f, 0, derp, 1.0f);
               } else if (cellFlags.HasFlag(CellFlags.Blocked)) {
                  var derp = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                  color = new Vector4(derp, 0.0f, 0, 1.0f);
               } else if (cellFlags.HasFlag(CellFlags.Edge)) {
                  var derp = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                  color = new Vector4(0.0f, derp, 0, 1.0f);
               } else {
                  var derp = ((x + y) % 2 == 0) ? 0.2f : 0.4f;
                  color = new Vector4(derp, derp, derp, 1.0f);
               }
               renderer.DrawCube(transform, color, false);
            }
         }
      }
   }
}