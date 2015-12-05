using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ItzWarty.Geometry
{
   public abstract class Shape2D : IReadOnlyPoint2DSet, IEnumerable
   {
      // :: Shape Constants
      public static readonly Point2D[] kEmptyPoints = new Point2D[0];
      public const double kContainsEpsilon = 0.00001;

      // :: Shape2D properties
      public ShapeType2D Type { get; private set; }

      // :: Abstract Properties and Methods
      public abstract bool IsFinite { get; }
      public abstract bool IsCountable { get; }
      public abstract int Count { get; }
      public abstract bool Contains(Point2D p);
      public abstract IEnumerator<Point2D> GetEnumerator();

      protected Shape2D(ShapeType2D type)
      {
         Type = type;
      }

      public Shape2D FindIntersection(Shape2D shape)
      {
         // ensure that 'this' is less or as primative as shape
         if (Type > shape.Type)
            return shape.FindIntersection(this);

         // find intersection
         if (this.Type == ShapeType2D.Point)
            return shape.Contains((Point2D)this) ? this : null;
         else if (this.Type == ShapeType2D.Line) {
            if (shape.Type == ShapeType2D.Line)
               return ((Line2D)this).FindLineIntersection((Line2D)shape);
            else if (shape.Type == ShapeType2D.Triangle) {
               var line = (Line2D)this;
               var triangle = (Triangle2D)shape;
               var abLine = new Line2D(triangle.A, triangle.B);
               var abIntersection = line.FindIntersection(abLine);
               var bcLine = new Line2D(triangle.B, triangle.C);
               var bcIntersection = line.FindIntersection(bcLine);
               var caLine = new Line2D(triangle.C, triangle.A);
               var caIntersection = line.FindIntersection(caLine);

               var abIntersectionLine = abIntersection as Line2D;
               var bcIntersectionLine = bcIntersection as Line2D;
               var caIntersectionLine = caIntersection as Line2D;
               var intersectionLine = abIntersectionLine ?? bcIntersectionLine ?? caIntersectionLine;
               if (intersectionLine != null) {
                  return intersectionLine;
               } else {
                  var points = new List<Point2D>();
                  if (abIntersection != null) {
                     var abIntersectionPoint = (Point2D)abIntersection;
                     var t = abLine.NearestT(abIntersectionPoint);
                     if (0 <= t && t <= 1) {
                        points.Add(abIntersectionPoint);
                     }
                        Console.WriteLine(t);
                  }

                  if (bcIntersection != null) {
                     var bcIntersectionPoint = (Point2D)bcIntersection;
                     var t = bcLine.NearestT(bcIntersectionPoint);
                     if (0 <= t && t <= 1) {
                        points.Add(bcIntersectionPoint);
                     }
                     Console.WriteLine(t);
                  }

                  if (caIntersection != null) {
                     var caIntersectionPoint = (Point2D)caIntersection;
                     var t = caLine.NearestT(caIntersectionPoint);
                     if (0 <= t && t <= 1) {
                        points.Add(caIntersectionPoint);
                     }
                     Console.WriteLine(t);
                  }

                  if (points.Count == 0) {
                     return null;
                  } else if (points.Count == 1) {
                     return points.First();
                  } else {
                     Console.WriteLine(points.Count);
                     Trace.Assert(points.Count == 2);
                     var pointCollection = new PointCollection2D(points.ToArray());
                     return pointCollection;
                  }
               }
            }
         }
         throw new System.NotImplementedException("Intersection between " + shape.Type + " and " + Type + " not implemented.");
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }
   }
}