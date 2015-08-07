using System;
using ItzWarty;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Shade {
   public class Program {
      private static void Main(string[] args) {
         Application.EnableVisualStyles();

         // Implement the Gruenes Schaf
         new Thread(() => {
            while(true) {
               GC.Collect();
               Thread.Sleep(5000);
            }
         }).Start();

         var graphicsConfiguration = new GraphicsConfiguration { Width = 1600, Height = 900 };
         var gridletFactory = new GridletFactory();
         IReadOnlyList<NavigationGridlet> gridlets = new List<NavigationGridlet> {
            gridletFactory.Quad(0, 0, 0, 0, 0, 0, 60, 60),
            gridletFactory.Quad(37, 0, 2, -0.3f, 0, 0, 15, 7),
            gridletFactory.Quad(47.60f, -9.0f, 4.22f, 0, 0, 0, 7, 25),
            gridletFactory.Quad(58.05f, -18.0f, 4.22f, 0, 0, 0, 15, 7)
         };
         var grid = new NavigationGrid(gridlets);
         grid.Initialize();
         var pathfinder = new Pathfinder(grid);
         var renderer = new Renderer();

         var entityFactory = new EntityFactory();
         var entitySystem = new EntitySystem();
         entitySystem.AddEntity(entityFactory.CreateUnitCubeEntity());
         grid.Gridlets.ForEach(gridlet => entitySystem.AddEntity(entityFactory.CreateGridletEntity(gridlet, pathfinder)));
         var characterEntity = entityFactory.CreateCharacterEntity();
         entitySystem.AddEntity(characterEntity);
         entitySystem.AddEntity(entityFactory.CreateCameraEntity(graphicsConfiguration, characterEntity));
         entitySystem.AddBehavior(new PhysicsBehavior(grid));
         entitySystem.AddBehavior(new PathingBehavior());

         using (var game = new ShadeGame(graphicsConfiguration, renderer, entitySystem)) {
            game.Run();
         }
      }
   }

   public class GridletFactory {
      public NavigationGridlet Quad(float cx, float cy, float cz, float pitch, float yaw, float roll, int xLength, int yLength) {
         var cells = Util.Generate(xLength * yLength, i => new NavigationGridletCell(i, i % xLength, i / xLength));
         for (var i = 0; i < cells.Length; i++) {
            cells[i].Height = 1;
         }
         for (var y = 0; y < yLength; y++) {
            cells[y * xLength].Flags = CellFlags.Edge;
            cells[(y + 1) * xLength - 1].Flags = CellFlags.Edge;
         }
         for (var x = 1; x < xLength - 1; x++) {
            cells[x].Flags = CellFlags.Edge;
            cells[x + xLength * (yLength - 1)].Flags = CellFlags.Edge;
         }
         var gridlet = new NavigationGridlet { X = cx, Y = cy, Z = cz, XLength = xLength, YLength = yLength, Cells = cells };
         gridlet.Orientation = Matrix.RotationZ(yaw) * Matrix.RotationY(pitch) * Matrix.RotationZ(roll);
         foreach (var cell in cells) {
            cell.Gridlet = gridlet;
         }
         gridlet.Initialize();
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
         this.graphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;

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

         foreach (var renderComponent in entitySystem.EnumerateComponents<RenderComponent>()) {
            if (renderComponent.IsVisible) {
               renderer.RenderEntity(renderComponent.Entity);
            }
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
