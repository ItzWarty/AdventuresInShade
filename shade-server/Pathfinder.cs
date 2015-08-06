using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItzWarty;
using SharpDX;

namespace Shade {
   public class Pathfinder {
      private readonly NavigationGrid grid;

      public Pathfinder(NavigationGrid grid) {
         this.grid = grid;
      }

      public Path FindPaths(Vector3 start, Vector3 end) {
         var startRay = new Ray(start + Vector3.UnitZ, -Vector3.UnitZ);
         var endRay = new Ray(end + Vector3.UnitZ, -Vector3.UnitZ);
         var startGridlet = grid.GetGridlets(startRay).FirstOrDefault();
         var endGridlet = grid.GetGridlets(endRay).FirstOrDefault();
         if (startGridlet != null && endGridlet != null) {
            var gridletPath = FindGridletPath(startGridlet, endGridlet);
            var results = Recursive(start, end, 0, gridletPath);
            return new Path { Pathlets = results };
         } else {
            return null;
         }
      }

      private List<Pathlet> Recursive(Vector3 startPosition, Vector3 endPosition, int currentGridletIndex, NavigationGridlet[] gridletPath) {
         var currentGridlet = gridletPath[currentGridletIndex];
         var destinationGridlet = gridletPath[currentGridletIndex + 1];
         var edges = currentGridlet.EdgeCells.Where(c => c.Neighbors.Any(n => n.Gridlet == destinationGridlet)).ToArray();
         var currentObb = currentGridlet.OrientedBoundingBox;
         var result = new List<Pathlet>();
         foreach (var connectingEdge in edges) {
            var neighbors = connectingEdge.Neighbors.Where(n => n.Gridlet == destinationGridlet).ToArray();
            foreach (var neighbor in neighbors) {
               var neighborObb = neighbor.OrientedBoundingBox;
               var pathlet = new Pathlet(new[] { startPosition, neighborObb.Center });
               result.Add(pathlet);
            }
         }
         return result;
      }

      private Pathlet LocalPath(NavigationGridlet gridlet, Vector3 start, Vector3 end) {
         return new Pathlet(new[] { start, end });
      }

      public class Path {
         public List<Pathlet> Pathlets { get; set; }
         public float Length => Pathlets.Sum(x => x.Length);
      }

      public class Pathlet {

         public Pathlet(Vector3[] points) {
            Points = points;
            for (var i = 0; i < points.Length - 1; i++) {
               Length += Vector3.Distance(points[i], points[i + 1]);
            }
         }

         public Vector3[] Points { get; set; }
         public float Length { get; set; }
      }

      public NavigationGridlet[] FindGridletPath(NavigationGridlet start, NavigationGridlet end) {
         Dictionary<NavigationGridlet, int> scoresByGridlet = new Dictionary<NavigationGridlet, int>();
         scoresByGridlet.Add(start, 0);
         var s = new Stack<KeyValuePair<NavigationGridlet, int>>();
         s.Push(new KeyValuePair<NavigationGridlet, int>(start, 0));
         while (s.Any()) {
            var kvp = s.Pop();
            foreach (var neighbor in kvp.Key.Neighbors) {
               if (!scoresByGridlet.ContainsKey(neighbor)) {
                  scoresByGridlet.Add(neighbor, kvp.Value + 1);
                  s.Push(new KeyValuePair<NavigationGridlet, int>(neighbor, kvp.Value + 1));
                  if (neighbor == end) {
                     break;
                  }
               }
            }
         }
         var current = end;
         List<NavigationGridlet> path = new List<NavigationGridlet>();
         while (current != start) {
            path.Add(current);
            current = current.Neighbors.MinBy(scoresByGridlet.Get);
         }
         path.Add(start);
         var result = path.ToArray();
         Array.Reverse(result);
         return result;
      }
   }
}
