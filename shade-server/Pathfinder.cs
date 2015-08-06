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

      public Path FindPath(Vector3 start, Vector3 end) {
         var startRay = new Ray(start + Vector3.UnitZ, -Vector3.UnitZ);
         var endRay = new Ray(end + Vector3.UnitZ, -Vector3.UnitZ);
         var startGridlet = grid.GetGridlets(startRay).FirstOrDefault();
         var endGridlet = grid.GetGridlets(endRay).FirstOrDefault();
         if (startGridlet == null || endGridlet == null) {
            return null;
         } else if (startGridlet == endGridlet) {
            return new Path(LocalPathlet(startGridlet, start, end));
         } else {
            var gridletPath = FindGridletPath(startGridlet, endGridlet);
            
            // Determine paths from start to startGridlet's connectors
            var startConnectors = startGridlet.EdgeCells.Where(x => x.Neighbors.Any(n => n.Gridlet == gridletPath[1])).ToArray();
            var connectorPaths = Util.Generate(
               startConnectors.Length, 
               i => startConnectors[i].PairValue(
                  new Path(LocalPathlet(startGridlet, start, startConnectors[i].OrientedBoundingBox.Center))
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
                                        select new Path(
                                           bp, 
                                           LocalPathlet(gridlet, bc.OrientedBoundingBox.Center, ac.OrientedBoundingBox.Center)
                                        )).MinBy(x => x.Length).PairKey(ac)).ToArray();
               connectorPaths = afterPaths;
            }

            // Find paths to pathing end location.
            return (from cp in connectorPaths
                    select new Path(cp.Value, LocalPathlet(endGridlet, cp.Key.OrientedBoundingBox.Center, end))).MinBy(x => x.Length);
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

      public class Path {
         private readonly Path path;
         private readonly Pathlet pathlet;

         public Path(Pathlet pathlet) {
            this.pathlet = pathlet;
         }

         public Path(Path path, Pathlet pathlet) {
            this.path = path;
            this.pathlet = pathlet;
         }

         public float Length => (path?.Length ?? 0) + pathlet.Length;

         public IEnumerable<Vector3> Points => EnumeratePoints();

         private IEnumerable<Vector3> EnumeratePoints() {
            if (path == null) {
               yield return pathlet.Points[0];
            } else {
               foreach (var x in path.EnumeratePoints()) {
                  yield return x;
               }
            }
            for (var i = 1; i < pathlet.Points.Length; i++) {
               yield return pathlet.Points[i];
            }
         }
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
