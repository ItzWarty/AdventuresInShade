using System;
using Poly2Tri;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Mime;
using System.Windows.Forms;

namespace Navi {
   public class Program {
      public static void Main(string[] args) {
         var triangulator = new Triangulator();
         var bounds = new List<TriangulationPoint> {
            new PolygonPoint(0, 0),
            new PolygonPoint(10, 0),
            new PolygonPoint(10, 10),
            new PolygonPoint(0, 10),
            new PolygonPoint(0, 0)
         };
         var land = new List<List<TriangulationPoint>> {
            new List<TriangulationPoint> {
               new PolygonPoint(1, 1),
               new PolygonPoint(9, 1),
               new PolygonPoint(9, 9),
               new PolygonPoint(1, 9),
               new PolygonPoint(1, 1)
            }
         };
         var holes = new List<List<TriangulationPoint>> {
            new List<TriangulationPoint> {
               new PolygonPoint(3, 3),
               new PolygonPoint(3, 7),
               new PolygonPoint(7, 7),
               new PolygonPoint(7, 3),
               new PolygonPoint(3, 3)
            }
         };
         var landAndHoles = land.Concat(holes).ToList();
         var triangulation = triangulator.Triangulate(landAndHoles, bounds);
         var sw = new Stopwatch();
         sw.Start();
         triangulation = triangulator.Shrink(land, holes, bounds, -0);
         sw.Stop();
         Console.WriteLine(sw.Elapsed);
         var bitmap = new Bitmap(200, 200);
         using (var g = Graphics.FromImage(bitmap)) {
            const double kScale = 10;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var triangle in triangulation) {
               for (var i = 0; i < 3; i++) {
                  var a = triangle.Points[i];
                  var b = triangle.Points[(i + 1) % 3];
                  g.DrawLine(Pens.Cyan, (float)(a.X * kScale), (float)(a.Y * kScale), (float)(b.X * kScale), (float)(b.Y * kScale));
               }
               var selfCentroid = triangle.Centroid();
               foreach (var neighbor in triangle.Neighbors.Where(n => n != null && n.IsInterior)) {
                  var neighborCentorid = neighbor.Centroid();
                  g.DrawLine(Pens.Red, (float)(selfCentroid.X * kScale), (float)(selfCentroid.Y * kScale), (float)(neighborCentorid.X * kScale), (float)(neighborCentorid.Y * kScale));
               }
            }
         }
         NaviUtil.ShowBitmap(bitmap);
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

   public class NavigationMesh {
      public NavigationMesh(IReadOnlyList<ConvexPolygon> polygons) {
         Polygons = polygons;
      }

      public IReadOnlyList<ConvexPolygon> Polygons { get; } 
   }

   public class ConvexPolygon {
      public ConvexPolygon(IReadOnlyList<ConvexPolygonEdge> polygonEdges) {
         PolygonEdges = polygonEdges;
      }

      public IReadOnlyList<ConvexPolygonEdge> PolygonEdges { get; }
   }

   public class ConvexPolygonConnection {
      public ConvexPolygonConnection(ConvexPolygon a, ConvexPolygonEdge aEdge, ConvexPolygon b, ConvexPolygonEdge bEdge) {
         A = a;
         AEdge = aEdge;
         B = b;
         BEdge = bEdge;
      }

      public ConvexPolygon A { get; }
      public ConvexPolygonEdge AEdge { get; }
      public ConvexPolygon B { get; }
      public ConvexPolygonEdge BEdge { get; }
   }

   public class ConvexPolygonEdge {
      public ConvexPolygonEdge(Vec3 a, Vec3 b) {
         A = a;
         B = b;
      }

      public Vec3 A { get; }
      public Vec3 B { get; }
      public ConvexPolygonEdge Mate { get; set; }
      public ConvexPolygonEdge ClockwiseNeighbor { get; set; }
      public ConvexPolygonEdge CounterClockwiseNeighbor { get; set; }
   }

   public struct Vec3 {
      public readonly float x;
      public readonly float y;
      public readonly float z;

      public Vec3(float x, float y, float z) {
         this.x = x;
         this.y = y;
         this.z = z;
      }
   }
}
