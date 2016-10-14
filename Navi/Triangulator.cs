using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;
using Poly2Tri;

namespace Navi {
   /// <summary>
   /// Port of https://github.com/raptor/clip2tri/blob/master/clip2tri/clip2tri.cpp
   /// </summary>
   public class Triangulator {
      private const double kClipperScaleFactor = 1000.0;
      private const double kInverseClipperScaleFactor = 1 / kClipperScaleFactor;
      private const int kTriangulatorBoundsLength = int.MaxValue / 2;

      private static readonly List<IntPoint> kTriangulatorBounds = new List<IntPoint> {
         new IntPoint(-kTriangulatorBoundsLength, -kTriangulatorBoundsLength),
         new IntPoint(kTriangulatorBoundsLength, -kTriangulatorBoundsLength),
         new IntPoint(kTriangulatorBoundsLength, kTriangulatorBoundsLength),
         new IntPoint(-kTriangulatorBoundsLength, kTriangulatorBoundsLength),
         new IntPoint(-kTriangulatorBoundsLength, -kTriangulatorBoundsLength)
      };

      public IReadOnlyList<DelaunayTriangle> TriangulateNavigationMesh(
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> land,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> holes,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> blockers,
         double delta
      ) {
         // Preprocessing
         var landUnionPolyTree = Offset(Union(land), delta * kClipperScaleFactor);
         var holeUnionPolyTree = Offset(Union(holes), -delta * kClipperScaleFactor);

         var staticTerrain = Punch(landUnionPolyTree, holeUnionPolyTree);

         // Blocker-specific stuff
         var sw = new Stopwatch();
         sw.Start();
         var blockersUnionPolyTree = Union(blockers);
         var subtractPolyTree = Punch(staticTerrain, blockersUnionPolyTree);

         var triangulateComplex = TriangulateComplex(subtractPolyTree);
         Console.WriteLine(sw.Elapsed.TotalMilliseconds);
         return triangulateComplex;
      }

      public IReadOnlyList<DelaunayTriangle> TriangulatePairNavigationMesh(
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> land1,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> holes1,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> land2,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> holes2,
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> blockers,
         double delta
      ) {
         // Preprocessing
         var landUnionPolyTree1 = Offset(Union(land1), delta * kClipperScaleFactor);
         var holeUnionPolyTree1 = Offset(Union(holes1), -delta * kClipperScaleFactor);
         var staticTerrain1 = Punch(landUnionPolyTree1, holeUnionPolyTree1);

         var landUnionPolyTree2 = Offset(Union(land2), delta * kClipperScaleFactor);
         var holeUnionPolyTree2 = Offset(Union(holes2), -delta * kClipperScaleFactor);
         var staticTerrain2 = Punch(landUnionPolyTree2, holeUnionPolyTree2);

         var staticTerrain = Union(staticTerrain1, staticTerrain2);

         // Blocker-specific stuff
         var sw = new Stopwatch();
         sw.Start();
         var blockersUnionPolyTree = Union(blockers);
         var subtractPolyTree = Punch(staticTerrain, blockersUnionPolyTree);

         var triangulateComplex = TriangulateComplex(subtractPolyTree);
         Console.WriteLine(sw.Elapsed.TotalMilliseconds);
         return triangulateComplex;
      }

      private PolyTree Union(IReadOnlyList<IReadOnlyList<TriangulationPoint>> input) {
         var landUnionClipper = new Clipper(Clipper.ioStrictlySimple);
         landUnionClipper.AddPaths(input.MapList(x => x.MapList(UpscalePoint)), PolyType.ptSubject, true);
         PolyTree landUnionPolyTree = new PolyTree();
         landUnionClipper.Execute(ClipType.ctUnion, landUnionPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
         return landUnionPolyTree;
      }

      private PolyTree Union(PolyTree a, PolyTree b) {
         var landUnionClipper = new Clipper(Clipper.ioStrictlySimple);
         var s = new Stack<PolyNode>();
         s.Push(a);
         s.Push(b);
         while (s.Any()) {
            var node = s.Pop();
            if (!node.IsHole) {
               landUnionClipper.AddPath(node.Contour, PolyType.ptSubject, true);
            }
            node.Childs.ForEach(s.Push);
         }
         PolyTree landUnionPolyTree = new PolyTree();
         landUnionClipper.Execute(ClipType.ctUnion, landUnionPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
         return landUnionPolyTree;
      }

      private PolyTree Punch(PolyTree input, PolyTree hole) {
         var subtractClipper = new Clipper(Clipper.ioStrictlySimple);
         for (var it = input.GetFirst(); it != null; it = it.GetNext()) {
            subtractClipper.AddPath(it.Contour, PolyType.ptSubject, true);
         }
         for (var it = hole.GetFirst(); it != null; it = it.GetNext()) {
            subtractClipper.AddPath(it.Contour, PolyType.ptClip, true);
         }
         var result = new PolyTree();
         subtractClipper.Execute(ClipType.ctDifference, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
         return result;
      }

      private PolyTree Offset(PolyTree input, double delta) {
         var clipperOffset = new ClipperOffset();
         for (var currentNode = input.GetFirst(); currentNode != null; currentNode = currentNode.GetNext()) {
            clipperOffset.AddPath(currentNode.Contour, JoinType.jtMiter, EndType.etClosedPolygon);
         }
         var result = new PolyTree();
         clipperOffset.Execute(ref result, delta);
         return result;
      }

      private IReadOnlyList<TriangulationPoint> UnpackTriangle(DelaunayTriangle t) {
         return new[] {
            t.Points[0],
            t.Points[1],
            t.Points[2]
         };
      }

      private static void DrawPolyTree(PolyTree inputUnionTree) {
         var bitmap = new Bitmap(200, 200);
         using (var g = Graphics.FromImage(bitmap)) {
            const double kScale = kInverseClipperScaleFactor * 10;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (var current = inputUnionTree.GetFirst(); current != null; current = current.GetNext()) {
               Console.WriteLine("Current Is Hole: " + current.IsHole);
               Console.WriteLine("Current Contour Count: " + current.Contour.Count);
               Console.WriteLine("Current Child Count: " + current.Childs.Count);
               for (var i = 0; i < current.Contour.Count; i++) {
                  foreach (var child in current.Childs) {
                     for (var j = 0; j < child.Contour.Count - 1; j++) {
                        for (var k = j + 1; k < child.Contour.Count; k++) {
                           var a = child.Contour[j];
                           var b = child.Contour[k];
                           g.DrawLine(Pens.Red, (float)(a.X * kScale), (float)(a.Y * kScale), (float)(b.X * kScale), (float)(b.Y * kScale));
                        }
                     }
                  }
                  {
                     var a = current.Contour[i];
                     var b = current.Contour[(i + 1) % current.Contour.Count];
                     g.DrawLine(Pens.Cyan, (float)(a.X * kScale), (float)(a.Y * kScale), (float)(b.X * kScale), (float)(b.Y * kScale));
                  }
               }
            }
         }
         NaviUtil.ShowBitmap(bitmap);
      }

      public List<DelaunayTriangle> TriangulateComplex(
         PolyTree polyTree,
         bool ignoreFills = false, bool ignoreHoles = true) {
         PolyNode rootNode;
         if (polyTree.Total == 0) {
            Console.WriteLine(0);
            rootNode = new PolyNode();
         } else {
            rootNode = polyTree.GetFirst().Parent;
         }

         // Equivalent to rootNode.Contour = bounds;
         var contourField = rootNode.GetType().GetField("m_polygon", BindingFlags.Instance | BindingFlags.NonPublic);
         if (contourField == null) {
            throw new Exception("Could not find field contour backing field.");
         }
         contourField.SetValue(rootNode, kTriangulatorBounds);

         var result = new List<DelaunayTriangle>();
         int i = 0;
         for (var currentNode = rootNode; currentNode != null; currentNode = currentNode.GetNext()) {
            if ((ignoreHoles && currentNode.IsHole) || (ignoreFills && !currentNode.IsHole)) continue;
            var polyline = DownscalePolygon(currentNode.Contour);
            var finalPolygon = new Polygon(polyline);
            foreach (var child in currentNode.Childs) {
               var shrunkContour = EdgeShrink(child.Contour);
               var holePoints = DownscalePolygon(shrunkContour);
               var holePoly = new Polygon(holePoints);
               finalPolygon.AddHole(holePoly);
            }
            P2T.Triangulate(finalPolygon);
            result.AddRange(finalPolygon.Triangles);
         }
         return result;
      }

      private List<IntPoint> EdgeShrink(List<IntPoint> path) {
         var newPath = new List<IntPoint>(path.Count);
         int prev = path.Count - 1;
         for (var i = 0; i < path.Count; i++) {
            long x, y;
            // Adjust coordinate by 1 depending on the direction
            if (path[i].X - path[prev].X > 0) {
               x = path[i].X - 1;
            } else {
               x = path[i].X + 1;
            }
            if (path[i].Y - path[prev].Y > 0) {
               y = path[i].Y - 1;
            } else {
               y = path[i].Y + 1;
            }
            newPath.Add(new IntPoint(x, y));
            prev = i;
         }
         return newPath;
      }


      public PolyTree UnionPolygonsToTree(IReadOnlyList<IReadOnlyList<TriangulationPoint>> polygons) {
         if (polygons == null) return null;

         var clipper = new Clipper(Clipper.ioStrictlySimple);
         foreach (var polygon in polygons) {
            clipper.AddPath(polygon.MapList(UpscalePoint), PolyType.ptSubject, true);
         }
         PolyTree polyTree = new PolyTree();
         clipper.Execute(ClipType.ctUnion, polyTree, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
//         Console.WriteLine(polygons.Count + " vs " + polyTree.Total + " " + polyTree.Childs.Count);
         return polyTree;
      }

      private IntPoint UpscalePoint(TriangulationPoint p) {
         var result = new IntPoint(p.X * kClipperScaleFactor, p.Y * kClipperScaleFactor);
//         Console.WriteLine("Upscale " + p + " to " + result);
         return result;
      }

      private IList<PolygonPoint> DownscalePolygon(IReadOnlyList<IntPoint> n) {
         return n.MapArray(DownscalePoint);
      }

      private PolygonPoint DownscalePoint(IntPoint p) {
         return new PolygonPoint(p.X * kInverseClipperScaleFactor, p.Y * kInverseClipperScaleFactor);
      }
   }
}
