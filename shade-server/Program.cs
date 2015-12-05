using DerpDerpGridStuff;
using ItzWarty;
using ItzWarty.Geometry;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Color = System.Drawing.Color;

namespace Shade {
   public class Program {
      private static void Main(string[] args) {
         Application.EnableVisualStyles();

         // Implement the Gruenes Schaf
         new Thread(() => {
            while (true) {
               GC.Collect();
               Thread.Sleep(5000);
            }
         }).Start();

         var gridWidth = 2;
         var gridHeight = 2;
         
         var grid = new GridFactory().Create(gridWidth, gridHeight);
         var manipulator = new GridManipulator(grid, new Random(0));
         var spiral = new SpiralParametricFunction(1, 10, 3, (float)gridWidth / 2, (float)gridHeight / 2, 0);
         manipulator.CutParametric(spiral.TInitial, spiral.TFinal, 20f, spiral.PointAt);
         var lastSpiralPoint = spiral.PointAt(spiral.TFinal - 30);
         var v = new Vector2D(lastSpiralPoint, new Point2D(gridWidth / 2.0f, gridHeight / 2.0f));
         v = v.ToUnitVector();
         var cutDestination = lastSpiralPoint + v * 3;
         manipulator.CutLine(new Line2D(lastSpiralPoint, cutDestination));
         var entranceCell = grid.Cells[(gridHeight / 2) * grid.Width + (gridWidth / 2)];
         var cells = manipulator.FillRegion(entranceCell);

         var graphicsConfiguration = new GraphicsConfiguration { Width = 1600, Height = 900 };
         var gridletFactory = new GridletFactory();
//         IReadOnlyList<NavigationGridlet> gridlets = CreateGridletsFromDungeonGrid(grid, gridletFactory);
         IReadOnlyList<NavigationGridlet> gridlets = new List<NavigationGridlet> {
            gridletFactory.Quad(0, 0, 0, 0, 0, 0, 60, 60),
//            gridletFactory.Quad(37, 0, 2, -0.3f, 0, 0, 15, 7),
//            gridletFactory.Quad(47.60f, -9.0f, 4.22f, 0, 0, 0, 7, 25),
//            gridletFactory.Quad(58.05f, -18.0f, 4.22f, 0, 0, 0, 15, 7)
         };
         var navigationGrid = new NavigationGrid(gridlets);
         navigationGrid.Initialize();
         var pathfinder = new Pathfinder(navigationGrid);
         var renderer = new Renderer();

         CommandFactory commandFactory = new CommandFactory(pathfinder);
         var entityFactory = new EntityFactory();
         var entitySystem = new EntitySystem();
         entitySystem.AddEntity(entityFactory.CreateUnitCubeEntity());
         navigationGrid.Gridlets.ForEach(gridlet => entitySystem.AddEntity(entityFactory.CreateAndAssociateGridletEntity(navigationGrid, gridlet, pathfinder, commandFactory)));
         var characterEntity = entityFactory.CreateCharacterEntity(pathfinder);
         entitySystem.AddEntity(characterEntity);
         entitySystem.AddEntity(entityFactory.CreateCameraEntity(graphicsConfiguration, characterEntity));
         entitySystem.AddBehavior(new PhysicsBehavior(navigationGrid));
         entitySystem.AddBehavior(new CommandQueueBehavior());

         // Dungeon Stuff
         DungeonKeyInventory dungeonKeyInventory = new DungeonKeyInventory();
//         entitySystem.AddEntity(entityFactory.CreateDungeonKeyEntity(new Vector3(5, 10, 0), new Vector4(1, 0, 0, 1), commandFactory, dungeonKeyInventory));
//         entitySystem.AddEntity(entityFactory.CreateDungeonLockEntity(new Vector3(0, 35, 0), new Vector4(1, 0, 0, 1), commandFactory, dungeonKeyInventory));
//         entitySystem.AddBehavior(new DungeonLockDisablesGroundPathingBehavior(navigationGrid));
         foreach (var cell in cells) {
            if (cell.KeyColor != Color.Empty) {
               var color = new Vector4(cell.KeyColor.R / 255f, cell.KeyColor.G / 255f, cell.KeyColor.B / 255f, 1);
               entitySystem.AddEntity(entityFactory.CreateDungeonKeyEntity(new Vector3(cell.X * 70 + 5, cell.Y * 70 + 10, 0), color, commandFactory, dungeonKeyInventory));
            }
            if (cell.LockColor != Color.Empty) {
               var color = new Vector4(cell.LockColor.R / 255f, cell.LockColor.G / 255f, cell.LockColor.B / 255f, 1);
               entitySystem.AddEntity(entityFactory.CreateDungeonLockEntity(new Vector3(cell.X * 70, cell.Y * 70, 0), color, commandFactory, dungeonKeyInventory, navigationGrid));
            }
         }
         
         using (var game = new ShadeGame(graphicsConfiguration, renderer, entitySystem)) {
            game.Run();
         }
      }

      private static IReadOnlyList<NavigationGridlet> CreateGridletsFromDungeonGrid(Grid grid, GridletFactory gridletFactory) {
         var cellSpacing = 70;
         var cellSize = 60;
         var connectorSize = 11;
         var connectorWidth = 5;

         var results = new List<NavigationGridlet>();
         foreach (var cell in grid.Cells) {
            results.Add(gridletFactory.Quad(cell.X * cellSpacing, cell.Y * cellSpacing, 0, 0, 0, 0, cellSize, cellSize));
         }
         var connectors = grid.Cells.SelectMany(x => x.Connectors).Distinct().Where(x => x.State == ConnectorState.Linked).ToArray();
//         foreach (var connector in connectors) {
         for (var i = 0; i < connectors.Length; i++) {
            Console.WriteLine("C: " + i + " / " + connectors.Length);
            var connector = connectors[i];
            var first = connector.First;
            var second = connector.Second;
            var segment = connector.Segment;
            var theta = Math.Atan2(segment.Vector.Y, segment.Vector.X);
            var dx = Math.Cos(theta) * cellSpacing / 2;
            var dy = Math.Sin(theta) * cellSpacing / 2;
            results.Add(gridletFactory.Quad((float)(first.X * cellSpacing + dx), (float)(first.Y * cellSpacing + dy), 0, 0, (float)theta, 0, connectorSize, connectorWidth));
         }
         return results;
      }
   }

   public class GridletFactory {
      public NavigationGridlet Quad(float cx, float cy, float cz, float pitch, float yaw, float roll, int xLength, int yLength) {
         var cells = Util.Generate(xLength * yLength, i => new NavigationGridletCell(i, i % xLength, i / xLength));
         for (var i = 0; i < cells.Length; i++) {
            var zzx = i % xLength - xLength / 2;
            var zzy = i / xLength - yLength / 2;
                        cells[i].Height = 1;
//            cells[i].Height = (float)(Math.Sqrt(zzx * zzx + zzy * zzy) / 2); //(float)StaticRandom.NextDouble(2);
         }
         for (var y = 0; y < yLength; y++) {
            cells[y * xLength].Flags = CellFlags.Edge;
            cells[(y + 1) * xLength - 1].Flags = CellFlags.Edge;
         }
         for (var x = 1; x < xLength - 1; x++) {
            cells[x].Flags = CellFlags.Edge;
            cells[x + xLength * (yLength - 1)].Flags = CellFlags.Edge;
         }
         var gridlet = new NavigationGridlet { X = cx, Y = cy, Z = cz, XLength = xLength, YLength = yLength, Cells = cells, IsEnabled = true };
         gridlet.Orientation = Matrix.RotationZ(yaw) * Matrix.RotationY(pitch) * Matrix.RotationZ(roll);
         foreach (var cell in cells) {
            cell.Gridlet = gridlet;
         }
         gridlet.Initialize();

         for (var x = 20; x < 30; x++) {
            for (var y = 20; y < 30; y++) {
               cells[x + y * xLength].Flags |= CellFlags.Blocked;
            }
         }
         for (var x = 40; x < 50; x++) {
            for (var y = 20; y < 30; y++) {
               cells[x + y * xLength].Flags |= CellFlags.Blocked;
            }
         }
         return gridlet;
      }
   }

   public class ShadeGame : Game {
      private readonly GraphicsConfiguration graphicsConfiguration;
      private readonly Renderer renderer;
      private readonly EntitySystem entitySystem;
      private readonly GraphicsDeviceManager graphicsDeviceManager;

      public ShadeGame(GraphicsConfiguration graphicsConfiguration, Renderer renderer, EntitySystem entitySystem) {
         this.graphicsConfiguration = graphicsConfiguration;
         this.renderer = renderer;
         this.entitySystem = entitySystem;

         this.graphicsDeviceManager = new GraphicsDeviceManager(this);
         this.graphicsDeviceManager.PreferredBackBufferWidth = graphicsConfiguration.Width;
         this.graphicsDeviceManager.PreferredBackBufferHeight = graphicsConfiguration.Height;
//         this.graphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;

         Content.RootDirectory = "Content";
      }

      public Form Form => (Form)Window.NativeWindow;
      public Entity Camera => entitySystem.EnumerateComponents<CameraComponent>().First().Entity;

      /// <summary>
      /// Called after the Game and GraphicsDevice are created, but before LoadContent.  Reference page contains code sample.
      /// </summary>
      protected override void Initialize() {
         base.Initialize();

         IsFixedTimeStep = true;
         IsMouseVisible = true;

         Form.MouseUp += HandledMouseEvent(MouseEventType.Up);
         Form.MouseMove += HandledMouseEvent(MouseEventType.Move);
         Form.MouseWheel += HandledMouseEvent(MouseEventType.Wheel);
         Form.MouseDown += HandledMouseEvent(MouseEventType.Down);

         renderer.SetGraphicsDevice(GraphicsDevice);
         renderer.Initialize();
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);
         foreach (var behavior in entitySystem.EnumerateBehaviors()) {
            behavior.Step(entitySystem, gameTime);
         }

         foreach (var gameStepComponent in entitySystem.EnumerateComponents<GameStepComponent>()) {
            gameStepComponent.HandleGameStep(gameTime);
         }
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);
         var camera = Camera;

         GraphicsDevice.Clear(Color4.Black);
         camera.GetComponent<MobaCameraComponent>().UpdateCamera();
         renderer.BeginRender(camera);

         renderer.DrawCube(Matrix.Identity, new Vector4(0, 0, 1, 0), false);

         foreach (var renderComponent in entitySystem.EnumerateComponents<RenderComponent>()) {
            renderer.RenderEntity(renderComponent.Entity, renderComponent);
         }

         renderer.EndRender(camera);
      }

      private MouseEventHandler HandledMouseEvent(MouseEventType type) {
         return (s, e) => {
            var cameraComponent = Camera.GetComponentOrNull<CameraComponent>();
            var pickRay = cameraComponent.GetPickRay(e.X, e.Y);

            var intersectedElements = new List<Tuple<Vector3, Entity>>();
            foreach (var entity in entitySystem.EnumerateEntities()) {
               var boundsComponent = entity.GetComponentOrNull<BoundsComponent>();
               if (boundsComponent != null) {
                  Vector3 intersectionPoint;
                  if (boundsComponent.Bounds.Intersects(ref pickRay, out intersectionPoint)) {
                     intersectedElements.Add(Tuple.Create(intersectionPoint, entity));
                  }
               }
               var mouseHandlerComponent = entity.GetComponentOrNull<MouseHandlerComponent>();
               if (mouseHandlerComponent != null) {
                  mouseHandlerComponent.HandleGlobalMouseEvent(
                     new SceneMouseEventInfo(
                        e.Button,
                        e.Clicks,
                        e.X,
                        e.Y,
                        e.Delta,
                        type,
                        pickRay,
                        -1,
                        Vector3.Zero));
               }
            }
            intersectedElements = intersectedElements.OrderBy(x => Vector3.Distance(x.Item1, pickRay.Position)).ToList();
            for (var rank = 0; rank < intersectedElements.Count; rank++) {
               var entity = intersectedElements[rank].Item2;
               var mouseHandlerComponent = entity.GetComponentOrNull<MouseHandlerComponent>();
               if (mouseHandlerComponent != null) {
                  mouseHandlerComponent.HandleMouseEvent(
                     new SceneMouseEventInfo(
                        e.Button,
                        e.Clicks,
                        e.X,
                        e.Y,
                        e.Delta,
                        type,
                        pickRay,
                        rank,
                        intersectedElements[rank].Item1));
               }
               Console.WriteLine(rank + " " + intersectedElements[rank].Item2);
            }
         };
      }
   }
}
