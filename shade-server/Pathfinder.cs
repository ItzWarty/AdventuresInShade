using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

      public Pathlet FindPath(Vector3 start, Vector3 end) {
         var startRay = new Ray(start + Vector3.UnitZ, -Vector3.UnitZ);
         var endRay = new Ray(end + Vector3.UnitZ, -Vector3.UnitZ);
         var startGridlet = grid.GetGridlets(startRay).FirstOrDefault();
         var endGridlet = grid.GetGridlets(endRay).FirstOrDefault();

         if (startGridlet == null || endGridlet == null) {
            return null;
         } else if (startGridlet == endGridlet) {
            return LocalPathlet(startGridlet, start, end);
         } else {
            var gridletPath = FindGridletPath(startGridlet, endGridlet);

            if (gridletPath == null) {
               return null;
            }
            
            // Determine paths from start to startGridlet's connectors
            var startConnectors = startGridlet.EdgeCells.Where(x => x.Neighbors.Any(n => n.Gridlet == gridletPath[1])).ToArray();
            var connectorPaths = Util.Generate(
               startConnectors.Length, 
               i => startConnectors[i].PairValue(
                  LocalPathlet(startGridlet, start, startConnectors[i].OrientedBoundingBox.Center)
               ));

            // Run down gridlets, finding shortest path to next connectors
            for (var i = 1; i < gridletPath.Length - 1; i++) {
               var gridlet = gridletPath[i];
               var after = gridletPath[i + 1];
               var afterConnectors = gridlet.EdgeCells.Where(x => x.Neighbors.Any(n => n.Gridlet == after)).ToArray();
               var afterPaths = (from ac in afterConnectors
                                 select (from kvp in connectorPaths
                                        let bc = kvp.Key
                                        let bp = kvp.Value
                                        select Pathlet.Combine(
                                           bp, 
                                           LocalPathlet(gridlet, bc.OrientedBoundingBox.Center, ac.OrientedBoundingBox.Center)
                                        )).MinBy(x => x.Length).PairKey(ac)).ToArray();
               connectorPaths = afterPaths;
            }

            // Find paths to pathing end location.
            return (from cp in connectorPaths
                    select Pathlet.Combine(cp.Value, LocalPathlet(endGridlet, cp.Key.OrientedBoundingBox.Center, end))).MinBy(x => x.Length);
         }
      }

      private List<Pathlet> GetConnectorToConnectorPathlets(Vector3 startPosition, Vector3 endPosition, int currentGridletIndex, NavigationGridlet[] gridletPath) {
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

      private Pathlet LocalPathlet(NavigationGridlet gridlet, Vector3 start, Vector3 end) {
         return new Pathlet(new[] { start, end });
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

         public static Pathlet Combine(Pathlet a, Pathlet b) {
            Vector3[] points = new Vector3[a.Points.Length + b.Points.Length - 1];
            for (var i = 0; i < a.Points.Length; i++) {
               points[i] = a.Points[i];
            }
            for (var i = 1; i < b.Points.Length; i++) {
               points[a.Points.Length + i - 1] = b.Points[i];
            }
            return new Pathlet(points);
         }
      }

      public NavigationGridlet[] FindGridletPath(NavigationGridlet start, NavigationGridlet end) {
         Dictionary<NavigationGridlet, int> scoresByGridlet = new Dictionary<NavigationGridlet, int>();
         scoresByGridlet.Add(start, 0);
         var s = new Stack<KeyValuePair<NavigationGridlet, int>>();
         s.Push(new KeyValuePair<NavigationGridlet, int>(start, 0));
         bool success = false;
         while (s.Any()) {
            var kvp = s.Pop();
            foreach (var neighbor in kvp.Key.Neighbors) {
               if (!scoresByGridlet.ContainsKey(neighbor)) {
                  scoresByGridlet.Add(neighbor, kvp.Value + 1);
                  s.Push(new KeyValuePair<NavigationGridlet, int>(neighbor, kvp.Value + 1));
                  if (neighbor == end) {
                     success = true;
                     break;
                  }
               }
            }
         }
         if (!success) {
            return null;
         } else {
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
}
