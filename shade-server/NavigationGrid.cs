using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ItzWarty;
using Poly2Tri;
using SharpDX;

namespace Shade {
   public class NavigationGrid {
      public NavigationGrid(IReadOnlyList<NavigationGridlet> gridlets) {
         this.Gridlets = gridlets;
      }

      public void Initialize() {
         for (var i = 0; i < Gridlets.Count - 1; i++) {
            var iGridlet = Gridlets[i];
            var iObb = iGridlet.OrientedBoundingBox;
            for (var j = i + 1; j < Gridlets.Count; j++) {
               var jGridlet = Gridlets[j];
               var intersectionType = jGridlet.OrientedBoundingBox.Contains(ref iObb);
               Console.WriteLine(intersectionType);
               if (intersectionType != ContainmentType.Disjoint) {
                  jGridlet.AddNeighbor(iGridlet);
                  iGridlet.AddNeighbor(jGridlet);

                  foreach (var iEdgeCell in iGridlet.EdgeCells) {
                     var iEdgeCellObb = iEdgeCell.OrientedBoundingBox;
                     foreach (var jEdgeCell in jGridlet.EdgeCells) {
                        if (jEdgeCell.OrientedBoundingBox.Contains(ref iEdgeCellObb) != ContainmentType.Disjoint) {
                           iEdgeCell.Flags |= CellFlags.Connector;
                           jEdgeCell.Flags |= CellFlags.Connector;
                           iEdgeCell.Neighbors.Add(jEdgeCell);
                           jEdgeCell.Neighbors.Add(iEdgeCell);
                        }
                     }
                  }
               }
            }
         }

         foreach (var gridlet in Gridlets) {
            gridlet.UpdateNavigationMesh();
         }
      }

      public IReadOnlyList<NavigationGridlet> Gridlets { get; }

      public IEnumerable<NavigationGridlet> GetGridlets(float x, float y) {
         Vector3 result;
         var query = new Ray(new Vector3(x, y, -10000f), new Vector3(0, 0, 1));
         return from gridlet in Gridlets
                where gridlet.OrientedBoundingBox.Intersects(ref query, out result)
                select gridlet;
      }

      public IEnumerable<NavigationGridlet> GetGridlets(Ray query) {
         List<KeyValuePair<NavigationGridlet, float>> results = new List<KeyValuePair<NavigationGridlet, float>>();
         foreach (var gridlet in Gridlets) {
            Vector3 intersection;
            if (gridlet.OrientedBoundingBox.Intersects(ref query, out intersection)) {
               results.Add(gridlet.PairValue(Vector3.Distance(query.Position, intersection)));
            }
         }
         return results.OrderBy(x => x.Value).Select(x => x.Key);
      }

      public NavigationGridletCell GetCell(Vector3 position) {
         var query = new Ray(position - Vector3.UnitZ * 10000f, new Vector3(0, 0, 1));
         var gridlets = GetGridlets(query);
         var cells = gridlets.SelectMany(x => x.GetCells(query).Select(x.PairValue));
         var highestCell = cells.MaxBy(x => x.Value.Height).Value;
         return highestCell;
      }
   }

   public class NavigationGridletCell {
      public NavigationGridletCell(int index, int x, int y) {
         Index = index;
         X = x;
         Y = y;
      }

      public NavigationGridlet Gridlet { get; set; }
      public int Index { get; set; }
      public int X { get; set; }
      public int Y { get; set; }
      public float Height { get; set; }
      public CellFlags Flags { get; set; }
      public OrientedBoundingBox OrientedBoundingBox { get; set; }
      public List<NavigationGridletCell> Neighbors { get; set; } = new List<NavigationGridletCell>();
   }

   public class NavigationGridlet {
      public float X { get; set; }
      public float Y { get; set; }
      public float Z { get; set; }
      public int XLength { get; set; }
      public int YLength { get; set; }
      public Matrix Orientation { get; set; }
      public OrientedBoundingBox OrientedBoundingBox { get; set; }

      public NavigationGridletCell[] Cells { get; set; }
      public HashSet<NavigationGridlet> Neighbors { get; set; } = new HashSet<NavigationGridlet>();
      public NavigationGridletCell[] EdgeCells { get; set; }
      public IList<DelaunayTriangle> Mesh { get; set; }

      public void Initialize() {
         var obb = new OrientedBoundingBox(-Vector3.One / 2, Vector3.One / 2);
         var gridletHeight = Cells.Max(c => c.Height);
         obb.Scale(new Vector3(XLength, YLength, gridletHeight));
         obb.Transform(Orientation);
         obb.Translate(new Vector3(X, Y, Z));
         this.OrientedBoundingBox = obb;

         // Setup Grid
         for (var x = 0; x < XLength; x++) {
            for (var y = 0; y < YLength; y++) {
               var cellIndex = x + y * XLength;
               var cellHeight = Cells[cellIndex].Height;
               var cellTransform = Matrix.Scaling(1, 1, cellHeight) * 
                                   Matrix.Translation(-XLength * 0.5f + x + 0.5f, -YLength * 0.5f + y + 0.5f, 0) * 
                                   Orientation * 
                                   Matrix.Translation(X, Y, Z);
               Cells[cellIndex].OrientedBoundingBox = new OrientedBoundingBox { Extents = Vector3.One / 2, Transformation = cellTransform };
            }
         }

         EdgeCells = Cells.Where(x => x.Flags.HasFlag(CellFlags.Edge)).ToArray();
      }

      public void UpdateNavigationMesh() {
         var cps = new ConstrainedPointSet(new List<TriangulationPoint> {
            new TriangulationPoint(-XLength / 2.0f - 5, -YLength / 2.0f - 5),
            new TriangulationPoint(XLength / 2.0f + 5, -YLength / 2.0f - 5),
            new TriangulationPoint(XLength / 2.0f + 5, YLength / 2.0f + 5),
            new TriangulationPoint(-XLength / 2.0f - 5, YLength / 2.0f + 5)
         });
         cps.AddConstraints(new List<TriangulationConstraint> {
            new TriangulationConstraint(new PolygonPoint(-XLength / 2.0f, -YLength / 2.0f), new PolygonPoint(XLength / 2.0f, -YLength / 2.0f)),
            new TriangulationConstraint(new PolygonPoint(XLength / 2.0f, -YLength / 2.0f), new PolygonPoint(XLength / 2.0f, YLength / 2.0f)),
            new TriangulationConstraint(new PolygonPoint(XLength / 2.0f, YLength / 2.0f), new PolygonPoint(-XLength / 2.0f, YLength / 2.0f)),
            new TriangulationConstraint(new PolygonPoint(-XLength / 2.0f, YLength / 2.0f), new PolygonPoint(-XLength / 2.0f, -YLength / 2.0f))

         });
         var handledNeighborCells = new HashSet<NavigationGridletCell>();
         foreach (var edgeCell in EdgeCells.Where(x => x.Flags.HasFlag(CellFlags.Connector))) {
            var thisObb = this.OrientedBoundingBox;
            foreach (var neighbor in edgeCell.Neighbors) {
               if (handledNeighborCells.Contains(neighbor)) {
                  continue;
               } else {
                  handledNeighborCells.Add(neighbor);
               }

               var neighborObb = neighbor.OrientedBoundingBox;
               var neighborToLocal = OrientedBoundingBox.GetBoxToBoxMatrix(ref thisObb, ref neighborObb);
               Vector3 zero = new Vector3(0, 0, 0);
               Vector3 neighborRelativePosition;
               Vector3.Transform(ref zero, ref neighborToLocal, out neighborRelativePosition);
               var p = new PolygonPoint((neighborRelativePosition.X), (neighborRelativePosition.Y));
               var q = new PolygonPoint((neighborRelativePosition.X + 0.05), (neighborRelativePosition.Y + 0.05));
               cps.AddConstraint(new TriangulationConstraint(p, q));
            }
//            var p = new PolygonPoint((edgeCell.X + 0.45) * 0.998f - XLength / 2.0f, (edgeCell.Y + 0.45f) * 0.98f - YLength / 2.0f);
//            var q = new PolygonPoint((edgeCell.X + 0.55) * 0.999f - XLength / 2.0f, (edgeCell.Y + 0.55f) * 0.99f - YLength / 2.0f);
//            cps.AddConstraint(new TriangulationConstraint(p, q));
         }
         //         cps.AddHole(
//            new List<TriangulationPoint> {
//               new TriangulationPoint(-XLength / 2 + 2, -YLength / 2 + 2),
//               new TriangulationPoint(XLength / 2 - 2, -YLength / 2 + 2),
//               new TriangulationPoint(XLength / 2 - 2, YLength / 2 - 2),
//               new TriangulationPoint(-XLength / 2 + 2, YLength / 2 - 2)
//            }, "hole");
         P2T.Triangulate(cps);
         Console.WriteLine(cps.Triangles.Count);
         Mesh = cps.Triangles;
//         for (var x = 0; x < XLength; x++) {
//            for (var y = 0; y < YLength; y++) {
//               var cellIndex = x + y * XLength;
//               if (Cells[cellIndex].Flags.HasFlag(CellFlags.Connector)) {
//
//               }
//            }
//         }
      }

      public void AddNeighbor(NavigationGridlet gridlet) {
         Neighbors.Add(gridlet);
      }

      public IEnumerable<NavigationGridletCell> GetCells(Ray query) {
         for (var cellIndex = 0; cellIndex < XLength * YLength; cellIndex++) {
            var cell = Cells[cellIndex];
            if (cell.OrientedBoundingBox.Intersects(ref query)) {
               yield return cell;
            }
         }
      }

      public IReadOnlyList<int> InteriorSearch(int startIndex, int endIndex) {
         // Performs a BFS on the navgrid. Ideally we'd use a better algorithm but, err, time.
         var q = new Queue<Tuple<int, short>>();
         q.Enqueue(new Tuple<int, short>(startIndex, 1));
         var cellCount = Cells.Length;
         var scores = Util.Generate(cellCount, i => Cells[i].Flags.HasFlag(Shade.CellFlags.Blocked) ? (short)-10 : (short)0);
         scores[startIndex] = 1;

         while (q.Any()) {
            var val = q.Dequeue();
            var index = val.Item1;

            if (index == endIndex) {
               break;
            }

            var step = val.Item2;
            var nextSideStep = (short)(step + 14);
            var nextDiagStep = (short)(step + 20);
            var leftIndex = index - 1;
            var rightIndex = index + 1;
            var upIndex = index - XLength;
            var downIndex = index + XLength;
            var hasLeft = index % XLength > 0;
            var hasRight = rightIndex % XLength > 0;
            var hasUp = upIndex >= 0;
            var hasDown = downIndex < cellCount;

            if (hasLeft && scores[leftIndex] == 0) {
               scores[leftIndex] = nextSideStep;
               q.Enqueue(new Tuple<int, short>(leftIndex, nextSideStep));
            }
            if (hasRight && scores[rightIndex] == 0) {
               scores[rightIndex] = nextSideStep;
               q.Enqueue(new Tuple<int, short>(rightIndex, nextSideStep));
            }
            if (hasUp && scores[upIndex] == 0) {
               scores[upIndex] = nextSideStep;
               q.Enqueue(new Tuple<int, short>(upIndex, nextSideStep));
            }
            if (hasDown && scores[downIndex] == 0) {
               scores[downIndex] = nextSideStep;
               q.Enqueue(new Tuple<int, short>(downIndex, nextSideStep));
            }

            if (hasLeft && hasUp && scores[upIndex - 1] == 0) {
               scores[upIndex - 1] = nextDiagStep;
               q.Enqueue(new Tuple<int, short>(upIndex - 1, nextDiagStep));
            }

            if (hasRight && hasUp && scores[upIndex + 1] == 0) {
               scores[upIndex + 1] = nextDiagStep;
               q.Enqueue(new Tuple<int, short>(upIndex + 1, nextDiagStep));
            }
            if (hasLeft && hasDown && scores[downIndex - 1] == 0) {
               scores[downIndex - 1] = nextDiagStep;
               q.Enqueue(new Tuple<int, short>(downIndex - 1, nextDiagStep));
            }

            if (hasRight && hasDown && scores[downIndex + 1] == 0) {
               scores[downIndex + 1] = nextDiagStep;
               q.Enqueue(new Tuple<int, short>(downIndex + 1, nextDiagStep));
            }
         }


         var result = new Stack<int>();
         int cellIndex = endIndex;
         while (cellIndex != startIndex) {
            result.Push(cellIndex);
            var currentValue = scores[cellIndex];
            var desiredNextSide = currentValue - 14;
            var desiredNextDiag = currentValue - 20;
            var leftIndex = cellIndex - 1;
            var rightIndex = cellIndex + 1;
            var upIndex = cellIndex - XLength;
            var downIndex = cellIndex + XLength;

            var hasLeft = cellIndex % XLength > 0;
            var hasRight = rightIndex % XLength > 0;
            var hasUp = upIndex >= 0;
            var hasDown = downIndex < cellCount;
            if (hasLeft && scores[leftIndex] == desiredNextSide) {
               cellIndex = leftIndex;
            } else if (hasRight && scores[rightIndex] == desiredNextSide) {
               cellIndex = rightIndex;
            } else if (hasUp && scores[upIndex] == desiredNextSide) {
               cellIndex = upIndex;
            } else if (hasDown && scores[downIndex] == desiredNextSide) {
               cellIndex = downIndex;
            } else if (hasLeft && hasUp && scores[upIndex - 1] == desiredNextDiag) {
               cellIndex = upIndex - 1;
            } else if (hasRight && hasUp && scores[upIndex + 1] == desiredNextDiag) {
               cellIndex = upIndex + 1;
            } else if (hasLeft && hasDown && scores[downIndex - 1] == desiredNextDiag) {
               cellIndex = downIndex - 1;
            } else if (hasRight && hasDown && scores[downIndex + 1] == desiredNextDiag) {
               cellIndex = downIndex + 1;
            } else {
               throw new InvalidOperationException("Failed to find path!");
            }
         }
         result.Push(startIndex);
         return Util.Generate(result.Count, i => result.Pop());
      }
   }

   [Flags]
   public enum CellFlags {
      Edge = 0x01,
      Blocked = 0x02,
      Debug = 0x04,
      Connector = 0x08
   }
}
