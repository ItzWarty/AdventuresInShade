using System;
using Poly2Tri;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Windows.Forms;
using ItzWarty;
using ItzWarty.Collections;

namespace Navi {
   public static class GeometryFactory {
      public static Vec3[] Rectangle(double x, double y, double z, double w, double h) {
         return new[] {
            new Vec3(x, y, z),
            new Vec3(x + w, y, z),
            new Vec3(x + w, y + h, z),
            new Vec3(x, y + h, z),
            new Vec3(x, y, z)
         };
      }
   }
   public class Program {

      public static void Main(string[] args) {
         var triangulator = new Triangulator();

         var leftR = new NavMeshUnit(
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(0, 1.2, 0, 1.7, 2.8)
            },
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(1, 2, 0, 1, 2)
            });
         var upperU = new NavMeshUnit(
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(1, 0, 0, 2.2, 1.8)
            },
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(1.8, 0, 0, 0.7, 1.2)
            });
         var upperRotatedR = new NavMeshUnit(
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3, 0.25, 0, 1.75, 1)
            },
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3, 0.75, 0, 1.25, 0.6)
            });
         var rightT = new NavMeshUnit(
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3, 1, 0, 2.25, 2.5)
            },
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3, 1.66, 0, 1.1, 2),
               GeometryFactory.Rectangle(4.75, 1.66, 0, 1, 2),
            });
         var lowerRightR = new NavMeshUnit(
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3, 2.3, 0, 1.3, 1.3)
            },
            new NavMeshPrimitiveCollection {
               GeometryFactory.Rectangle(3.4, 2.75, 0, 1.3, 1.3)
            });

         var units = new[] { leftR, upperU, upperRotatedR, rightT, lowerRightR };

         // Build units' octrees.
         foreach (var unit in units) {
            var triangulation = triangulator.TriangulateNavigationMesh(
               unit.Land.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               unit.Holes.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               new List<IReadOnlyList<TriangulationPoint>>(), 0);
            var quadTree = TriangulationQuadTree.Build(triangulation);
            unit.QuadTree = quadTree;
         }

         var colorMap = new Dictionary<NavMeshUnit, Color> {
            [leftR] = Color.Red,
            [upperU] = Color.Green,
            [upperRotatedR] = Color.LightSlateGray,
            [rightT] = Color.Pink,
            [lowerRightR] = Color.Blue
         };

         var bitmap = new Bitmap(800, 800);
         using (var g = Graphics.FromImage(bitmap)) {
            const double kScale = 80;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var unit in units) {
               var triangulation = triangulator.TriangulateNavigationMesh(ConvertToTriangulatable(unit.Land), ConvertToTriangulatable(unit.Holes), new List<IReadOnlyList<TriangulationPoint>>(), 0);
               DrawTriangulation(triangulation, g, kScale, colorMap[unit]);
            }
         }
//         NaviUtil.ShowBitmap(bitmap);

         var leftRNode = new NavMeshNode(leftR);
         var upperUNode = new NavMeshNode(upperU);
         var upperRotatedRNode = new NavMeshNode(upperRotatedR);
         var rightTNode = new NavMeshNode(rightT);
         var lowerRightRNode = new NavMeshNode(lowerRightR);
         leftRNode.AddNeighbor(upperUNode);
         upperUNode.AddNeighbor(upperRotatedRNode);
         upperUNode.AddNeighbor(rightTNode);
         rightTNode.AddNeighbor(upperRotatedRNode);
         rightTNode.AddNeighbor(lowerRightRNode);
         var nodes = new[] { leftRNode, upperUNode, upperRotatedRNode, rightTNode, lowerRightRNode };
         var neighborPairs = nodes.SelectMany(x => x.Neighbors.Select(n => new NavMeshUnion(x, n))).Distinct().ToArray();

         // foreach neighbor pair, union their triangulations:
         // note: this requires that holes from one will not intersect land from another
         var unionTriangulationByPair = new Dictionary<NavMeshUnion, IReadOnlyList<DelaunayTriangle>>();
         foreach (var neighborPair in neighborPairs) {
            var firstUnit = neighborPair.First.Unit;
            var secondUnit = neighborPair.Second.Unit;
            var land = new NavMeshPrimitiveCollection();
            var holes = new NavMeshPrimitiveCollection();
            firstUnit.Land.ForEach(x => land.Add(x));
            firstUnit.Holes.ForEach(x => holes.Add(x));
            secondUnit.Land.ForEach(x => land.Add(x));
            secondUnit.Holes.ForEach(x => holes.Add(x));

            var triangulation = triangulator.TriangulatePairNavigationMesh(
               firstUnit.Land.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               firstUnit.Holes.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               secondUnit.Land.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               secondUnit.Holes.MapList(l => l.MapArray(p => new TriangulationPoint(p.X, p.Y))),
               new List<IReadOnlyList<TriangulationPoint>>(),
               0);
            unionTriangulationByPair.Add(neighborPair, triangulation);
         }

         bitmap = new Bitmap(800, 800);
         using (var g = Graphics.FromImage(bitmap)) {
            const double kScale = 80;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var colors = new Stack<Color>(new [] {
               Color.Red,
               Color.Green,
               Color.Blue,
               Color.Magenta, 
               Color.Cyan,
            });
            foreach (var pair in unionTriangulationByPair) {
               DrawTriangulation(pair.Value, g, kScale, colors.Pop(), 0);
            }
//            NaviUtil.ShowBitmap(bitmap);
         }

         // Navigation between first pair
         bitmap = new Bitmap(800, 800);
         using (var g = Graphics.FromImage(bitmap)) {
            const double kScale = 80;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawTriangulation(unionTriangulationByPair.First().Value, g, kScale, Color.Red);


            NaviUtil.ShowBitmap(bitmap);
         }
      }

      private static IReadOnlyList<IReadOnlyList<TriangulationPoint>> ConvertToTriangulatable(NavMeshPrimitiveCollection input) {
         var result = new List<IReadOnlyList<TriangulationPoint>>();
         foreach (var x in input) {
            result.Add(x.MapArray(p => new TriangulationPoint(p.X, p.Y)));
         }
         return result;
      }

      private static void DrawTriangulation(IReadOnlyList<DelaunayTriangle> triangulation, Graphics g, double kScale, Color boundsColor, int offset = 0, bool drawConnectivity = false) {
         using (var pen = new Pen(boundsColor, 3)) {
            foreach (var triangle in triangulation) {
               for (var i = 0; i < 3; i++) {
                  var a = triangle.Points[i];
                  var b = triangle.Points[(i + 1) % 3];
                  g.DrawLine(pen, (float)(a.X * kScale) + offset, (float)(a.Y * kScale) + offset, (float)(b.X * kScale) + offset, (float)(b.Y * kScale) + offset);
               }
               if (drawConnectivity) {
                  var selfCentroid = triangle.Centroid();
                  foreach (var neighbor in triangle.Neighbors.Where(n => n != null && n.IsInterior)) {
                     var neighborCentorid = neighbor.Centroid();
                     g.DrawLine(Pens.Red, (float)(selfCentroid.X * kScale) + offset, (float)(selfCentroid.Y * kScale) + offset, (float)(neighborCentorid.X * kScale) + offset, (float)(neighborCentorid.Y * kScale) + offset);
                  }
               }
            }
         }
      }
   }

   public static class NaviUtil {
      public static void ShowBitmap(Bitmap bitmap) {
         var pb = new PictureBox { Image = bitmap, SizeMode = PictureBoxSizeMode.AutoSize };
         var f = new Form { ClientSize = pb.Size };
         f.Controls.Add(pb);
         Application.Run(f);
      }

      public static U[] MapArray<T, U>(this IReadOnlyList<T> array, Func<T, U> map) {
         var result = new U[array.Count];
         for (var i = 0; i < array.Count; i++) {
            result[i] = map(array[i]);
         }
         return result;
      }

      public static List<U> MapList<T, U>(this IReadOnlyList<T> array, Func<T, U> map) {
         var result = new List<U>(array.Count);
         for (var i = 0; i < array.Count; i++) {
            result.Add(map(array[i]));
         }
         return result;
      }
   }
}
