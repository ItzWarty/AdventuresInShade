using System;
using System.Collections.Generic;
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

      public List<DelaunayTriangle> Triangulate(
         IReadOnlyList<IReadOnlyList<TriangulationPoint>> input,
         IReadOnlyList<TriangulationPoint> bounds
      ) {
         var inputUnionTree = UnionPolygonsToTree(input);
//         DrawPolyTree(inputUnionTree);
         var boundsScaled = bounds.MapList(UpscalePoint);
         return TriangulateComplex(inputUnionTree, boundsScaled);
      }

      public List<DelaunayTriangle> Shrink(
         List<List<TriangulationPoint>> land,
         List<List<TriangulationPoint>> holes,
         IReadOnlyList<TriangulationPoint> bounds, 
         double delta
      ) {
         var landUnionClipper = new Clipper(Clipper.ioStrictlySimple);
         landUnionClipper.AddPaths(land.MapList(x => x.MapList(UpscalePoint)), PolyType.ptSubject, true);
         PolyTree landUnionPolyTree = new PolyTree();
         landUnionClipper.Execute(ClipType.ctUnion, landUnionPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

         var holeUnionClipper = new Clipper(Clipper.ioStrictlySimple);
         holeUnionClipper.AddPaths(holes.MapList(x => x.MapList(UpscalePoint)), PolyType.ptSubject, true);
         PolyTree holeUnionPolyTree = new PolyTree();
         holeUnionClipper.Execute(ClipType.ctUnion, holeUnionPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

         landUnionPolyTree = Offset(landUnionPolyTree, delta * kClipperScaleFactor);
         holeUnionPolyTree = Offset(holeUnionPolyTree, -delta * kClipperScaleFactor);

         var subtractClipper = new Clipper(Clipper.ioStrictlySimple);
         for (var it = landUnionPolyTree.GetFirst(); it != null; it = it.GetNext()) {
            subtractClipper.AddPath(it.Contour, PolyType.ptSubject, true);
         }
         for (var it = holeUnionPolyTree.GetFirst(); it != null; it = it.GetNext()) {
            subtractClipper.AddPath(it.Contour, PolyType.ptClip, true);
         }
         var subtractPolyTree = new PolyTree();
         subtractClipper.Execute(ClipType.ctDifference, subtractPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

         var subtractUnionClipper = new Clipper(Clipper.ioStrictlySimple);
         for (var it = subtractPolyTree.GetFirst(); it != null; it = it.GetNext()) {
            subtractUnionClipper.AddPath(it.Contour, PolyType.ptSubject, true);
         }
         var subtractUnionPolyTree = new PolyTree();
         subtractUnionClipper.Execute(ClipType.ctUnion, subtractUnionPolyTree, PolyFillType.pftPositive, PolyFillType.pftPositive);

         var boundsScaled = bounds.MapList(UpscalePoint);
         return TriangulateComplex(subtractUnionPolyTree, boundsScaled);
      }

      private PolyTree Offset(PolyTree input, double delta) {
         var clipperOffset = new ClipperOffset();
         for (var currentNode = input.GetFirst(); currentNode != null; currentNode = currentNode.GetNext()) {
            clipperOffset.AddPath(currentNode.Contour, JoinType.jtRound, EndType.etClosedPolygon);
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

      private List<DelaunayTriangle> TriangulateComplex(
         PolyTree polyTree, List<IntPoint> bounds,
         bool ignoreFills = false, bool ignoreHoles = true) {
         PolyNode rootNode;
         if (polyTree.Total == 0) {
            rootNode = new PolyNode();
         } else {
            rootNode = polyTree.GetFirst().Parent;
         }

         // Equivalent to rootNode.Contour = bounds;
         var contourField = rootNode.GetType().GetField("m_polygon", BindingFlags.Instance | BindingFlags.NonPublic);
         if (contourField == null) {
            throw new Exception("Could not find field contour backing field.");
         }
         contourField.SetValue(rootNode, bounds);

         var result = new List<DelaunayTriangle>();
         int i = 0;
         for (var currentNode = rootNode; currentNode != null; currentNode = currentNode.GetNext()) {
            if ((ignoreHoles && currentNode.IsHole) || (ignoreFills && !currentNode.IsHole)) continue;
            var polyline = DownscalePolygon(currentNode.Contour);
            var finalPolygon = new Polygon(polyline.MapArray(p => new PolygonPoint(p.X, p.Y)));
            foreach (var child in currentNode.Childs) {
               var shrunkContour = EdgeShrink(child.Contour);
               var holePoints = DownscalePolygon(shrunkContour);
               var holePoly = new Polygon(holePoints.MapArray(p => new PolygonPoint(p.X, p.Y)));
               finalPolygon.AddHole(holePoly);
            }
            P2T.Triangulate(finalPolygon);
            foreach (var triangle in finalPolygon.Triangles) {
               result.Add(triangle);
            }
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

      private List<TriangulationPoint> DownscalePolygon(IReadOnlyList<IntPoint> n) {
         return n.MapList(DownscalePoint);
      }

      private TriangulationPoint DownscalePoint(IntPoint p) {
         return new TriangulationPoint(p.X * kInverseClipperScaleFactor, p.Y * kInverseClipperScaleFactor);
      }
   }
}
