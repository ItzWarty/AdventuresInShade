using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ItzWarty;
using SharpDX;
using SharpDX.Toolkit;

namespace Shade {
   public class Character {
      private readonly MouseEventBus mouseEventBus;
      private readonly NavigationGrid grid;
      private readonly Pathfinder pathfinder;
      private Camera camera;
      private Vector3 position;
      internal Pathfinder.Pathlet path;
      internal int pathProgress = 0;

      public Character(MouseEventBus mouseEventBus, NavigationGrid grid, Pathfinder pathfinder) {
         this.mouseEventBus = mouseEventBus;
         this.grid = grid;
         this.pathfinder = pathfinder;
      }

      public float X { get { return position.X; } set { position.X = value; } }
      public float Y { get { return position.Y; } set { position.Y = value; } }
      public float Z { get { return position.Z; } set { position.Z = value; } }
      public Vector3 Position { get { return position; } set { position = value; } }

      public void SetCamera(Camera camera) {
         this.camera = camera;
      }

      public void Initialize() {
         mouseEventBus.Event += HandleMouseEvent;
      }

      private void HandleMouseEvent(object sender, MouseEventInfo e) {
         if (e.Button.HasFlag(MouseButtons.Right)) {
            HandlePathingClick(camera.GetPickRay(e.X, e.Y));
         }
      }

      public void Step(GameTime gameTime) {
         if (path != null) {
            var ms = 10;
            var d = ms * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Console.WriteLine(path.Points.Join(", "));
            Console.WriteLine(d);
            while (d > 0 && pathProgress < path.Points.Length) {
               var nextPoint = path.Points[pathProgress];
               var dR = Vector3.Distance(Position, nextPoint);
               Console.WriteLine("DR: " + dR);
               if (dR > d) {
                  var dir = (nextPoint - position);
                  dir.Normalize();
                  Console.WriteLine(dir);
                  position += dir * d;
                  d = 0;
               } else {
                  d -= dR;
                  Position = nextPoint;
                  pathProgress++;
               }
            }
            if (pathProgress == path.Points.Length) {
               pathProgress = -1;
               path = null;
            }
         }
         try {
            if (path == null) {
               var highestCell = GetCurrentCellPair();
               position.Z = highestCell.Value.OrientedBoundingBox.Center.Z + 0.5f;
            }
         } catch (Exception e) {
            Console.WriteLine(e);
         }
      }

      public void HandlePathingClick(Ray pickRay) {
         try {
            var currentGridlet = GetCurrentCellPair().Key;
            NavigationGridlet destinationGridlet;
            int destinationCellIndex;
            Vector3 intersection;
            if (TryIntersect(pickRay, out destinationGridlet, out destinationCellIndex, out intersection)) {
               path = pathfinder.FindPath(position, intersection);
               pathProgress = 1;
            }
         } catch (Exception e) {

         }
         //         var destinationGridlet = grid.GetGridlets(pickRay);

         //         NavigationGridlet gridlet;
         //         int cellIndex;
         //         Vector3 intersection;
         //         if (TryIntersect(pickRay, out gridlet, out cellIndex, out intersection)) {
         //            var currentPair = GetCurrentCellPair();
         //            Console.WriteLine("Path from " + currentPair.Value + " to " + cellIndex);
         //            var searchResult = gridlet.InteriorSearch(currentPair.Value.Index, cellIndex);
         //            searchResult.ForEach(i => gridlet.Cells[i].Flags = CellFlags.Debug);
         //         }
      }

      private bool TryIntersect(Ray pickRay, out NavigationGridlet gridlet, out int cellIndex, out Vector3 intersection) {
         var gridlets = grid.GetGridlets(pickRay).ToArray();
         var cells = gridlets.SelectMany(x => x.GetCells(pickRay).Select(x.PairValue)).ToArray();
         if (cells.None()) {
            gridlet = null;
            cellIndex = -1;
            intersection = Vector3.Zero;
            return false;
         } else {
            var intersectionPoints = new Vector3[cells.Length];
            for (var i = 0; i < cells.Length; i++) {
               cells[i].Value.OrientedBoundingBox.Intersects(ref pickRay, out intersectionPoints[i]);
            }
            int nearestIntersectionIndex = -1;
            float nearestIntersectionDistance = float.PositiveInfinity;
            for (var i = 0; i < intersectionPoints.Length; i++) {
               var distance = Vector3.Distance(pickRay.Position, intersectionPoints[i]);
               if (distance < nearestIntersectionDistance) {
                  nearestIntersectionIndex = i;
                  nearestIntersectionDistance = distance;
               }
            }
            gridlet = cells[nearestIntersectionIndex].Key;
            cellIndex = cells[nearestIntersectionIndex].Value.Index;
            intersection = intersectionPoints[nearestIntersectionIndex];
            return true;
         }
      }

      private KeyValuePair<NavigationGridlet, NavigationGridletCell> GetCurrentCellPair() {
         var query = new Ray(new Vector3(X, Y, Z - 10000f), new Vector3(0, 0, 1));
         var gridlets = grid.GetGridlets(X, Y);
         var cells = gridlets.SelectMany(x => x.GetCells(query).Select(x.PairValue));
         var highestCell = cells.MaxBy(x => x.Value.Height);
         return highestCell;
      }
   }
}
