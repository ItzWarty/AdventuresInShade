using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItzWarty;
using ItzWarty.Geometry;
using Poly2Tri;

namespace Navi {
   public class TriangulationQuadTree {
      private const double kUpScale = 10000;
      private const double kDownScale = 1/kUpScale;

      private readonly QuadTree<DelaunayTriangle, Vec2> quadTree;

      public TriangulationQuadTree(QuadTree<DelaunayTriangle, Vec2> quadTree) {
         this.quadTree = quadTree;
      }

      public static TriangulationQuadTree Build(IReadOnlyList<DelaunayTriangle> triangulation) {
         var quadTree = new QuadTree<DelaunayTriangle, Vec2>(
            GetBoundingRectangle(triangulation.SelectMany(x => x.Points)),
            GetRectangle,
            Convert,
            Test);
         return new TriangulationQuadTree(quadTree);
      }

      private static Rectangle GetRectangle(DelaunayTriangle triangle) {
         return GetBoundingRectangle(triangle.Points);
      }

      private static Rectangle GetBoundingRectangle(IEnumerable<TriangulationPoint> input) {
         var points = input.Select(Convert).ToList();
         var minX = points.Min(p => p.X);
         var minY = points.Min(p => p.Y);
         var maxX = points.Max(p => p.X);
         var maxY = points.Max(p => p.Y);
         return new Rectangle(minX, minY, maxX - minX, maxY - minY);
      }

      public static Point Convert(TriangulationPoint p) {
         return new Point((int)(p.X * kUpScale), (int)(p.Y * kUpScale));
      }

      public static Point Convert(Vec2 p) {
         return new Point((int)(p.X * kUpScale), (int)(p.Y * kUpScale));
      }

      public static bool Test(DelaunayTriangle triangle, Vec2 p) {
         return triangle.Contains(new TriangulationPoint(p.X, p.Y));
      }
   }
}
