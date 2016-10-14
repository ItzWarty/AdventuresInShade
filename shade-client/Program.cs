using System;
using Navi;
using Poly2Tri;
using System.Collections.Generic;
using System.Linq;
using SharpDX;
using SharpDX.Toolkit;

namespace Shade {
   public static class Program {
      public static void Main(string[] args) {
         var program = new CanvasProgramImpl();
         var engine = new CanvasEngine(program);
         engine.Run();
      }
   }

   public class CanvasProgramImpl : CanvasProgram {
      private const double kScale = 30;
      private List<DelaunayTriangle> triangulation;

      public override void Setup() {
         base.Setup();

         var land = new List<List<TriangulationPoint>> {
            GeometryFactory.Rectangle(1, 1, 8, 8),
            GeometryFactory.Rectangle(9, 4, 2, 2),
            GeometryFactory.Rectangle(11, 1, 8, 8),
            GeometryFactory.Rectangle(14, 9, 2, 2),
            GeometryFactory.Rectangle(11, 11, 8, 8)
         };
         var holes = new List<List<TriangulationPoint>> {
            GeometryFactory.Rectangle(3, 3, 4, 4),
            GeometryFactory.Rectangle(12.6, 2.6, 1.6, 1.6),
            GeometryFactory.Rectangle(15.8, 2.6, 1.6, 1.6),
            GeometryFactory.Rectangle(12.6, 5.8, 1.6, 1.6),
            GeometryFactory.Rectangle(15.8, 5.8, 1.6, 1.6),
            GeometryFactory.Rectangle(11, 11, 1.6, 1.6),
            GeometryFactory.Rectangle(17.4, 11, 1.6, 1.6),
            GeometryFactory.Rectangle(17.4, 17.4, 1.6, 1.6),
            GeometryFactory.Rectangle(11, 17.4, 1.6, 1.6),
            GeometryFactory.Rectangle(13, 14, 4, 2),
            GeometryFactory.Rectangle(14, 13, 2, 4)
         };
         var blockers = new List<List<TriangulationPoint>> {
//            GeometryFactory.Rectangle(9, 4, 6, 1)
         };

         triangulation = new Triangulator().TriangulateNavigationMesh(land, holes, blockers, 0);
      }

      public override void Render(GameTime gameTime) {
         base.Render(gameTime);

         Canvas.Clear(Color.FromBgra(0xFF000000 + 0x010101 * 10));
         
         foreach (var triangle in triangulation) {
            Canvas.BeginPath();
            Canvas.SetLineStyle(1, Color.Cyan);
            Canvas.MoveTo(triangle.Points[0].X * kScale, triangle.Points[0].Y * kScale);
            Canvas.LineTo(triangle.Points[1].X * kScale, triangle.Points[1].Y * kScale);
            Canvas.LineTo(triangle.Points[2].X * kScale, triangle.Points[2].Y * kScale);
            Canvas.LineTo(triangle.Points[0].X * kScale, triangle.Points[0].Y * kScale);
            Canvas.FillPath(Color.White);
            Canvas.Stroke();

            Canvas.BeginPath();
            Canvas.SetLineStyle(1, Color.Red);
            foreach (var neighbor in triangle.Neighbors.Where(n => n != null)) {
               if (neighbor.IsInterior) {
                  Canvas.MoveTo(triangle.Centroid().X * kScale, triangle.Centroid().Y * kScale);
                  Canvas.LineTo(neighbor.Centroid().X * kScale, neighbor.Centroid().Y * kScale);
               }
            }
            Canvas.Stroke();
         }
      }
   }

   public class DynamicElement : Canvas {
      public DynamicElement(int width, int height) : base(width, height) {

      }
   }

//   public class TriangulationElement : DynamicElement {
//      private readonly List<DelaunayTriangle> triangles;
//
//      public TriangulationElement(List<DelaunayTriangle> triangles) {
//         Step += HandleStep;
//      }
//
//      private void HandleStep() {
//         foreach (var triangle in triangles) {
//            var points = triangle.Points;
//            BeginPath();
//            SetLineStyle(1, 0x00FF00);
//            MoveTo(points[0].X, points[0].Y);
//            LineTo(points[1].X, points[1].Y);
//            LineTo(points[2].X, points[2].Y);
//            LineTo(points[0].X, points[0].Y);
//            Stroke();
//         }
//      }
//   }

   public class GeometryLayer2D {

   }
}
