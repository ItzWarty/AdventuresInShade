using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ItzWarty;
using ItzWarty.Geometry;
using ICL = ItzWarty.Collections;

namespace DerpDerpGridStuff {
   public static class Program {
      public static void Main(string[] args) {
         var width = 45;
         var height = 30;
         var grid = new GridFactory().Create(width, height);
         var manipulator = new GridManipulator(grid, new Random());
//         manipulator.CutLine(new Line2D(new Point2D(0, 0), new Point2D(20, 40)));

         Renderer renderer = null;//new Renderer(grid);//.RenderGrid(grid);
         var spiral = new SpiralParametricFunction(1, 9, 3, width / 2.0f, height / 2.0f, 0);
         manipulator.CutParametric(spiral.TInitial, spiral.TFinal, 20f, spiral.PointAt, renderer);
         var lastSpiralPoint = spiral.PointAt(spiral.TFinal - 30);
         var v = new Vector2D(lastSpiralPoint, new Point2D(width / 2.0f, height / 2.0f));
         v = v.ToUnitVector();
         var cutDestination = lastSpiralPoint + v * 3;
         manipulator.CutLine(new Line2D(lastSpiralPoint, cutDestination), renderer);

         Application.DoEvents();
         var entranceCell = grid.Cells[(height / 2) * grid.Width + (width / 2)];
         var cells = manipulator.FillRegion(entranceCell, renderer);

         renderer = new Renderer(grid);//.RenderGrid(grid);

         manipulator.PlaceLocksAndKeys(entranceCell, cells, new[] {
            Color.Red,
            Color.Blue,
            Color.DarkGreen,
            Color.DeepPink,
            Color.Magenta,
            Color.Cyan,
            Color.Yellow,
            Color.Gold,
            Color.Black,
            Color.DarkOrange,
            Color.Gray,
            Color.Aquamarine,
            Color.AliceBlue,
            Color.Azure,
            Color.DodgerBlue,
            Color.Indigo,
            Color.DeepSkyBlue,
            Color.PaleGreen,
            Color.PaleGoldenrod,
            Color.PaleTurquoise});

         Console.WriteLine("Cell Count: " + cells.Count);
         entranceCell.Type = CellType.Entrance; 
         while (true) {
            renderer.RenderGrid(grid);
            Application.DoEvents();
         }
      }
   }

   public class SpiralParametricFunction {
      private const float pi = (float)Math.PI;
      private const float kStepsPerRevolutionPerUnitRadius = 100f;

      private readonly float tInitial, tFinal;
      private readonly float rInitial, drdt;
      private readonly float thetaInitial, d0dt;
      private readonly float xCenter, yCenter;

      public SpiralParametricFunction(float rInner, float rOuter, float drPerRevolution, float xCenter, float yCenter, float thetaInitial = 0.0f) {
         float revolutions = (rOuter - rInner) / drPerRevolution;
         float kStepsPerRevolution = rOuter * kStepsPerRevolutionPerUnitRadius;

         this.tInitial = 0.0f;
         this.tFinal = revolutions * kStepsPerRevolution;

         float thetaFinal = thetaInitial + revolutions * 2 * pi;
         this.rInitial = rInner;
         this.drdt = (rOuter - rInner) / (tFinal - tInitial);
         this.thetaInitial = thetaInitial;
         this.d0dt = (thetaFinal - thetaInitial) / (tFinal - tInitial);
         this.xCenter = xCenter;
         this.yCenter = yCenter;
      }

      public float TInitial => tInitial;
      public float TFinal => tFinal;

      public Point2D PointAt(double t) {
         var r = rInitial + drdt * t;
         var theta = thetaInitial + d0dt * t;
         return new Point2D(xCenter + r * Math.Sin(theta), yCenter + r * Math.Cos(theta));
      }
   }

   public class GridManipulator {
      private readonly Grid grid;
      private readonly Random random;

      public GridManipulator(Grid grid, Random random) {
         this.grid = grid;
         this.random = random;
      }

      public List<Cell> FillRegion(Cell initialSeed, Renderer renderer = null) {
         var seeds = new List<Cell>();
         seeds.Add(initialSeed);

         var cells = new List<Cell>(); // includes all seeds - any room which has a clearly defined entrance
         cells.Add(initialSeed);

         while (seeds.Any()) {
            var index = (int)(seeds.Count * Math.Pow(random.NextDouble(), 0.25));
            var seed = seeds.ElementAt(index); //GetRandomIntegerWeightedTowardEnd(seeds.Count, random));
            var candidates = seed.Connectors.Where((c) => c.State == ConnectorState.Unlinked && !cells.Contains(c.Other(seed))).ToArray();

            if (!candidates.Any()) {
               seeds.Remove(seed);
               continue;
            }

            CellConnector connector;
            if (candidates.Count() == 1) {
               seeds.Remove(seed);
               connector = candidates.First();
            } else {
               connector = candidates.SelectRandom();
            }

            connector.Connect();
            var cell = connector.Other(seed);
            seeds.Add(cell);
            cells.Add(cell);
            renderer?.RenderGrid(grid);
         }

         return cells;
      }

      public void CutLine(Line2D line, Renderer renderer = null) {
         foreach (var connector in grid.Cells.SelectMany(x => x.Connectors).Distinct()) {
            var intersection = connector.Segment.FindLineIntersection(line);
            if (intersection == line) {
               // wrong
               connector.connectorState = ConnectorState.Banned;
            } else if (intersection is Point2D) {
               var t1 = connector.Segment.NearestT((Point2D)intersection);
               var t2 = line.NearestT((Point2D)intersection);
               if (t1 >= 0 && t1 <= 1 && t2 >= 0 && t2 <= 1) {
                  connector.connectorState = ConnectorState.Banned;
               }
            }
         }
         renderer?.RenderGrid(grid);
      }

      public void CutParametric(float tInitial, float tFinal, float tStep, Func<double, Point2D> pointAt, Renderer renderer = null) {
         var steps = (int)Math.Ceiling((tFinal - tInitial) / tStep);
         var points = Util.Generate(steps, step => pointAt(tInitial + step * tStep));
         for (var i = 0; i < points.Length - 1; i++) {
            CutLine(new Line2D(points[i], points[i + 1]), renderer);
         }
      }

      public void PlaceLocksAndKeys(Cell entrance, List<Cell> cells, Color[] keyColors) {
         if (keyColors.Length > cells.Count - 2) {
            throw new InvalidOperationException("The operation would require placing more than one key per cell or placing a key on the entrance.");
         }

         var lockedCells = cells.Where(x => x != entrance).Shuffle(random).Take(keyColors.Length).ToArray();
         for (var i = 0 ; i < keyColors.Length; i++) {
            lockedCells[i].LockColor = keyColors[i];
         }
         
         // Handle case where all entrance neighbors have a lock: Redistribute one lock elsewhere.
         var entranceNeighborCells = entrance.Connectors.Where(x => x.State == ConnectorState.Linked).Select(x => x.Other(entrance));
         if (entranceNeighborCells.All(x => x.LockColor != Color.Empty)) {
            // Remove lock from random entrance neighbor and place in any other cell.
            var recipientCell = cells.Where(x => x.LockColor == Color.Empty).Shuffle().First();
            var entranceNeighborCell = entranceNeighborCells.Shuffle().First();
            recipientCell.LockColor = entranceNeighborCell.LockColor;
            entranceNeighborCell.LockColor = Color.Empty;
         }

         var visited = new HashSet<Cell>();
         var frontline = new List<Cell>();
         var keyable = new List<Cell>();

         visited.Add(entrance);
         entrance.Connectors.Where(x => x.State == ConnectorState.Linked).Select(x => x.Other(entrance)).ForEach(frontline.Add);

         int keysRemaining = keyColors.Length;

         while (frontline.Any()) {
            var frontlineCellIndex = (int)(random.NextDouble() * frontline.Count);
            var frontlineCell = frontline[frontlineCellIndex];
            frontline.RemoveAt(frontlineCellIndex);

            Console.WriteLine("!" + frontlineCell);

            if (frontlineCell.LockColor != Color.Empty) {
               var keyableCellIndex = (int)(random.NextDouble() * keyable.Count);
               var keyableCell = keyable[keyableCellIndex];
               keyable.RemoveAt(keyableCellIndex);
               keyableCell.KeyColor = frontlineCell.LockColor;
               keysRemaining--;
            }

            keyable.Add(frontlineCell);
            if (keyable.Count > keysRemaining + keyColors.Length / 5) {
               keyable.RemoveAt(0);
            }
            visited.Add(frontlineCell);
            foreach (var neighbor in frontlineCell.Connectors.Where(x => x.State == ConnectorState.Linked).Select(x => x.Other(frontlineCell))) {
               if (!visited.Contains(neighbor)) {
                  frontline.Add(neighbor);
               }
            }
         }
      }
   }

   public class GridFactory {
      public Grid Create(int width, int height) {
         var cells = Util.Generate(width * height, i => new Cell(i, i % width, i / width));
         for (var y = 0; y < height; y++) {
            for (var x = 0; x < width - 1; x++) {
               var index = x + y * width;
               cells[index].ConnectWith(cells[index + 1]);
            }
         }
         for (var x = 0; x < width; x++) {
            for (var y = 0; y < height - 1; y++) {
               var index = x + y * width;
               cells[index].ConnectWith(cells[index + width]);
            }
         }
         return new Grid(cells, width, height);
      }
   }

   public class Grid {
      private readonly Cell[] cells;
      private readonly int width;
      private readonly int height;

      public Grid(Cell[] cells, int width, int height) {
         this.cells = cells;
         this.width = width;
         this.height = height;
      }

      public Cell[] Cells => cells;
      public int Width => width;
      public int Height => height;
   }

   public class Cell {
      private readonly List<CellConnector> connectors = new List<CellConnector>(); 
      private readonly int index;
      private readonly int x;
      private readonly int y;

      public Cell(int index, int x, int y) {
         this.index = index;
         this.x = x;
         this.y = y;
      }

      public int Index => index;
      public int X => x;
      public int Y => y;

      public IReadOnlyCollection<CellConnector> Connectors => connectors;
      public CellType Type { get; set; }
      public Color KeyColor { get; set; }
      public Color LockColor { get; set; }

      public void ConnectWith(Cell other) {
         var segment = new Line2D(new Point2D(x, y), new Point2D(other.X, other.Y));
         var connector = new CellConnector(this, other, segment, ConnectorState.Unlinked);  
         connectors.Add(connector);
         other.connectors.Add(connector);
      }
   }

   public enum CellType {
      Undefined,
      Entrance,
      Exit,
      Arena,
      Corridor
   }

   public class DebugGraphicsContext {
      private List<PointF> points = new List<PointF>();
      private List<PointF[]> lines = new List<PointF[]>();
      public void Clear() { this.points.Clear(); this.lines.Clear(); }
      public void PlotPoint(PointF point) { this.points.Add(point); }
      public void Line(PointF start, PointF end) { this.lines.Add(new PointF[] { start, end }); }

      public IReadOnlyList<PointF> Points { get { return points; } }
      public IReadOnlyList<PointF[]> Lines { get { return lines; } }
   }

   public class CellConnector {
      private readonly Cell first;
      private readonly Cell second;
      private readonly Line2D segment;
      public ConnectorState connectorState;

      public CellConnector(Cell first, Cell second, Line2D segment,  ConnectorState connectorState = ConnectorState.Unlinked) {
         this.first = first;
         this.second = second;
         this.segment = segment;
         this.connectorState = connectorState;
      }

      public void Connect() { connectorState = ConnectorState.Linked; }
      public void Disconnect() { connectorState = ConnectorState.Unlinked; }
      public void Break() { connectorState = ConnectorState.Banned; }

      public Cell First => first;
      public Cell Second => second;
      public Line2D Segment => segment;
      public ConnectorState State { get { return connectorState; } set { connectorState = value; } }

      public Cell Other(Cell cell) {
         if (cell == first) {
            return second;
         } else if (cell == second) {
            return first;
         } else {
            throw new InvalidOperationException();
         }
      }
   }

   public enum ConnectorState {
      Banned,
      Unlinked,
      Linked
   }
}
