using System;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using ItzWarty;
using SharpDX.Direct3D11;
using Point = System.Drawing.Point;
using RasterizerState = SharpDX.Toolkit.Graphics.RasterizerState;

namespace Shade {
   public class Program {
      private static void Main(string[] args) {
         Application.EnableVisualStyles();

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
         var mouseEventBus = new MouseEventBus();
         var character = new Character(mouseEventBus, grid, pathfinder);
         character.Initialize();
         character.Position = new Vector3(45, 0, 10);
         var camera = new Camera(graphicsConfiguration, mouseEventBus, character);
         camera.Initialize();
         character.SetCamera(camera);
         using (var game = new ShadeGame(graphicsConfiguration, grid, character, camera, pathfinder, mouseEventBus)) {
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
      private readonly NavigationGrid navigationGrid;
      private readonly Character character;
      private readonly Camera camera;
      private readonly Pathfinder pathfinder;
      private readonly MouseEventBus mouseEventBus;
      private readonly GraphicsDeviceManager graphicsDeviceManager;
      private RenderMesh cubeMesh;
      private GeometricPrimitive cube;
      private BasicEffect basicEffect;
      private Matrix cubeModelTransform;
      private Matrix characterModelTransform;
      private PrimitiveBatch<VertexPositionColor> debugBatch;
      private Effect debugEffect;

      public ShadeGame(GraphicsConfiguration graphicsConfiguration, NavigationGrid navigationGrid, Character character, Camera camera, Pathfinder pathfinder, MouseEventBus mouseEventBus) {
         this.graphicsConfiguration = graphicsConfiguration;
         this.navigationGrid = navigationGrid;
         this.character = character;
         this.camera = camera;
         this.pathfinder = pathfinder;
         this.mouseEventBus = mouseEventBus;

         this.graphicsDeviceManager = new GraphicsDeviceManager(this);
         this.graphicsDeviceManager.PreferredBackBufferWidth = graphicsConfiguration.Width;
         this.graphicsDeviceManager.PreferredBackBufferHeight = graphicsConfiguration.Height;
         this.graphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;

         Content.RootDirectory = "Content";
      }

      public Form Form => (Form)Window.NativeWindow;

      /// <summary>
      /// Called after the Game and GraphicsDevice are created, but before LoadContent.  Reference page contains code sample.
      /// </summary>
      protected override void Initialize() {
         base.Initialize();

         IsFixedTimeStep = true;
         IsMouseVisible = true;

         cube = ToDisposeContent(GeometricPrimitive.Cube.New(GraphicsDevice));
         var cubeBounds = new OrientedBoundingBox(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
         cubeMesh = new RenderMesh {
            BoundingBox = cubeBounds,
            IndexBuffer = cube.IndexBuffer,
            IsIndex32Bits = cube.IsIndex32Bits,
            InputLayout = VertexInputLayout.New<VertexPositionNormalTexture>(0),
            ModelTransform = Matrix.Identity,
            VertexBuffer = cube.VertexBuffer
         };
         cubeModelTransform = Matrix.Translation(0,0,0);
         characterModelTransform = Matrix.Translation(0, 0, 0.5f);

         basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice));
         basicEffect.EnableDefaultLighting(); // enable default lightning, useful for quick prototyping

         var debugEffectCompilerResult = new EffectCompiler().CompileFromFile("shaders/debug_solid.hlsl", EffectCompilerFlags.Debug);
         debugEffect = new Effect(GraphicsDevice, debugEffectCompilerResult.EffectData, GraphicsDevice.DefaultEffectPool);
         debugBatch = new PrimitiveBatch<VertexPositionColor>(GraphicsDevice);

         Form.MouseUp += (s, e) => {
            mouseEventBus.Trigger(MouseEventType.Up, e);
         };
         Form.MouseMove += (s, e) => {
            mouseEventBus.Trigger(MouseEventType.Move, e);
         };
         Form.MouseWheel += (s, e) => {
            mouseEventBus.Trigger(MouseEventType.Wheel, e);
         };
         Form.MouseDown += (s, e) => {
            mouseEventBus.Trigger(MouseEventType.Down, e);
         };
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);
         character.Step(gameTime);
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);
         GraphicsDevice.Viewport = camera.Viewport;
         GraphicsDevice.Clear(Color4.Black);
         GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.Default);

         camera.UpdatePrerender(GraphicsDevice);
         basicEffect.View = camera.View;
         basicEffect.Projection = camera.Projection;
         foreach (var gridlet in navigationGrid.Gridlets) {
            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.Default);
//            for (var y = 0; y < gridlet.YLength; y++) {
//               for (var x = 0; x < gridlet.XLength; x++) {
//                  var cellIndex = y * gridlet.XLength + x;
//                  var cellHeight = gridlet.Cells[cellIndex].Height;
//                  var cellFlags = gridlet.Cells[cellIndex].Flags;
//                  var transform = gridlet.Cells[cellIndex].OrientedBoundingBox.Transformation;
//                  basicEffect.World = cubeModelTransform * transform;
//                  if (cellFlags.HasFlag(CellFlags.Connector)) {
//                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
//                     basicEffect.DiffuseColor = new Vector4(color, color / 2, 0, 1.0f);
//                  } else if (cellFlags.HasFlag(CellFlags.Debug)) {
//                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
//                     basicEffect.DiffuseColor = new Vector4(0.0f, 0, color, 1.0f);
//                  } else if (cellFlags.HasFlag(CellFlags.Blocked)) {
//                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
//                     basicEffect.DiffuseColor = new Vector4(color, 0.0f, 0, 1.0f);
//                  } else if (cellFlags.HasFlag(CellFlags.Edge)) {
//                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
//                     basicEffect.DiffuseColor = new Vector4(0.0f, color, 0, 1.0f);
//                  } else {
//                     var color = ((x + y) % 2 == 0) ? 0.2f : 0.4f;
//                     basicEffect.DiffuseColor = new Vector4(color, color, color, 1.0f);
//                  }
//                  cube.Draw(GraphicsDevice, basicEffect);
//               }
//            }
         }

         // Draw character
         GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.Default);
         basicEffect.DiffuseColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
         basicEffect.World = characterModelTransform * Matrix.Scaling(2, 2, 2.8f) * Matrix.Translation(character.X, character.Y, character.Z);
         cube.Draw(basicEffect);

         // Draw debug cube
         basicEffect.DiffuseColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
         basicEffect.World = Matrix.Identity;
         cube.Draw(basicEffect);

         // Draw Character's Gridlet OBB
         foreach (var gridlet in navigationGrid.GetGridlets(character.X, character.Y)) {
            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.WireFrame);
            var bb = gridlet.OrientedBoundingBox;
            basicEffect.DiffuseColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            basicEffect.World = bb.Transformation * Matrix.Scaling(1.01f);
            cube.Draw(basicEffect);
         }

         foreach (var gridlet in navigationGrid.Gridlets) {
            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.WireFrame);
            var bb = gridlet.OrientedBoundingBox;
            basicEffect.DiffuseColor = new Vector4(1.0f, 0, 0, 1.0f);
            //            basicEffect.World = Matrix.Scaling(bb.Extents * 2) * gridlet.Orientation * Matrix.Translation(bb.Center);
            basicEffect.World = Matrix.Scaling(bb.Extents * 2) * Matrix.Scaling(1.01f) * bb.Transformation;
            //Matrix.Translation(0, 0, 0.5f) * Matrix.Scaling(gridlet.XLength, gridlet.YLength, gridlet.Cells.Max(x => x.Height)) * gridlet.Orientation * Matrix.Translation(gridlet.X, gridlet.Y, gridlet.Z);
            //Matrix.Scaling(bb.Extents * 2) * bb.Transformation * Matrix.Translation(bb.Center);
            cube.Draw(basicEffect);
         }

         // Draw Picked Gridlet OBBs
         //         var cursor = Form.PointToClient(Cursor.Position);
         //         var ray = GetPickRay(cursor);
         //         foreach (var gridlet in navigationGrid.GetGridlets(ray)) {
         //            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.WireFrame);
         //            var bb = gridlet.OrientedBoundingBox;
         //            basicEffect.DiffuseColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
         //            basicEffect.World = bb.Transformation * Matrix.Scaling(1.02f);
         //            cube.Draw(basicEffect);
         //         }

         // Draw Gridlet Neighbors
         debugEffect.DefaultParameters.WorldParameter.SetValue(Matrix.Identity);
         debugEffect.DefaultParameters.ViewParameter.SetValue(camera.View);
         debugEffect.DefaultParameters.ProjectionParameter.SetValue(camera.Projection);
         debugEffect.CurrentTechnique.Passes[0].Apply();
         debugBatch.Begin();
         GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.None);
         foreach (var gridlet in navigationGrid.Gridlets) {
//            foreach (var neighbor in gridlet.Neighbors) {
//               debugBatch.DrawLine(
//                  new VertexPositionColor(gridlet.OrientedBoundingBox.Center, Color.Cyan),
//                  new VertexPositionColor(neighbor.OrientedBoundingBox.Center, Color.Cyan)
//               );
//            }

//            foreach (var cell in gridlet.EdgeCells.Where(x => x.Flags.HasFlag(CellFlags.Connector))) {
//               foreach (var neighbor in cell.Neighbors) {
//                  debugBatch.DrawLine(
//                     new VertexPositionColor(cell.OrientedBoundingBox.Center, Color.Lime),
//                     new VertexPositionColor(neighbor.OrientedBoundingBox.Center, Color.Lime)
//                  );
//               }
//            }
         }
         debugBatch.End();

         // Draw pathing
         var path = character.path;
         if (path != null) {
            var pathPoints = path.Points.ToArray();
            debugBatch.Begin();
            for (var i = 0; i < pathPoints.Length - 1; i++) {
               debugBatch.DrawLine(
                  new VertexPositionColor(pathPoints[i], Color.Cyan),
                  new VertexPositionColor(pathPoints[i + 1], Color.Cyan)
                  );
            }
            debugBatch.End();
         }

         // Draw Gridlet Navmeshes
         debugEffect.DefaultParameters.WorldParameter.SetValue(Matrix.Identity);
         debugEffect.DefaultParameters.ViewParameter.SetValue(camera.View);
         debugEffect.DefaultParameters.ProjectionParameter.SetValue(camera.Projection);
         GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.None);
         var zzzz = 0;
         foreach (var gridlet in navigationGrid.Gridlets) {
//            debugEffect.DefaultParameters.WorldParameter.SetValue(gridlet.OrientedBoundingBox.Transformation);
//            debugBatch.Begin();
//            debugEffect.CurrentTechnique.Passes[0].Apply();
//            foreach (var triangle in gridlet.Mesh) {
//               for (var i = 0; i < 3; i++) {
//                  var a = triangle.Points[i];
//                  var b = triangle.Points[(i + 1) % 3];
//                  debugBatch.DrawLine(
//                     new VertexPositionColor(new Vector3(a.Xf, a.Yf, 1), new [] { Color.Lime, Color.Red, Color.White }[zzzz]),
//                     new VertexPositionColor(new Vector3(b.Xf, b.Yf, 1), new[] { Color.Lime, Color.Red, Color.White }[zzzz])
//                  );
//               }
//            }
//            debugBatch.End();
//            zzzz++;
         }
      }
   }
}
