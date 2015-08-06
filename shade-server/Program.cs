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

         var gridletFactory = new GridletFactory();
         IReadOnlyList<NavigationGridlet> gridlets = new List<NavigationGridlet> {
//            gridletFactory.Quad(3, 0, 0, -0.3f, 0, 0, 15, 7),
            gridletFactory.Quad(0, 0, 0, 0, 0, 0, 60, 60),
            gridletFactory.Quad(37, 0, 2, -0.3f, 0, 0, 15, 7),
            gridletFactory.Quad(47.5f, -4.0f, 4.25f, 0, 0, 0, 7, 15),
            gridletFactory.Quad(58, -8.0f, 4.25f, 0, 0, 0, 15, 7)
         };
         var grid = new NavigationGrid(gridlets);
         grid.Initialize();
         var pathfinder = new Pathfinder(grid);
         var character = new Character(grid, pathfinder);
         character.X = 0;
         var camera = new Camera(character);
         using (var game = new ShadeGame(grid, character, camera, pathfinder)) {
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
      private readonly NavigationGrid navigationGrid;
      private readonly Character character;
      private readonly Camera camera;
      private readonly Pathfinder pathfinder;
      private readonly GraphicsDeviceManager graphicsDeviceManager;
      private RenderMesh cubeMesh;
      private GeometricPrimitive cube;
      private BasicEffect basicEffect;
      private Matrix cubeModelTransform;
      private Matrix characterModelTransform;
      private PrimitiveBatch<VertexPositionColor> debugBatch;
      private Effect debugEffect;

      public ShadeGame(NavigationGrid navigationGrid, Character character, Camera camera, Pathfinder pathfinder) {
         this.navigationGrid = navigationGrid;
         this.character = character;
         this.camera = camera;
         this.pathfinder = pathfinder;

         this.graphicsDeviceManager = new GraphicsDeviceManager(this);
         this.graphicsDeviceManager.PreferredBackBufferWidth = 1600;
         this.graphicsDeviceManager.PreferredBackBufferHeight = 900;
         this.graphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;

         Content.RootDirectory = "Content";
      }

      public Form Form => (Form)Window.NativeWindow;

      /// <summary>
      /// Called after the Game and GraphicsDevice are created, but before LoadContent.  Reference page contains code sample.
      /// </summary>
      protected override void Initialize() {
         base.Initialize();

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
         Console.WriteLine("SUCCESS? " + !debugEffectCompilerResult.HasErrors);
         foreach (var message in debugEffectCompilerResult.Logger.Messages) {
            Console.WriteLine(message.Text);
         }
         foreach (var shader in debugEffectCompilerResult.EffectData.Shaders) {
            Console.WriteLine("HAVE SHADER " + shader.Name);
         }
         debugEffect = new Effect(GraphicsDevice, debugEffectCompilerResult.EffectData, GraphicsDevice.DefaultEffectPool);
         foreach (var x in debugEffect.Parameters) {
            Console.WriteLine("PARAMETER " + x.Name);
         }


         debugBatch = new PrimitiveBatch<VertexPositionColor>(GraphicsDevice);

         // Mouse Stuff:
         this.IsMouseVisible = true;

         int lastMouseX = -1, lastMouseY = -1;
         Form.MouseUp += (s, e) => {
            lastMouseX = -1;
            lastMouseY = -1;
         };
         Form.MouseMove += (s, e) => {
            if (e.Button.HasFlag(MouseButtons.Right)) {
               if (lastMouseX != -1) {
                  var dx = e.X - lastMouseX;
                  var dy = e.Y - lastMouseY;
                  camera.Drag(dx, dy);
               }
               lastMouseX = e.X;
               lastMouseY = e.Y;
            }
         };
         Form.MouseWheel += (s, e) => {
            camera.Zoom(e.Delta);
         };
         Form.MouseDown += (s, e) => {
            Console.WriteLine(e.Location);
            character.HandlePathingClick(GetPickRay(e.Location));
         };
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);
         character.Step(gameTime);
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);
         GraphicsDevice.Clear(Color4.Black);
         GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.Default);

         camera.UpdatePrerender(GraphicsDevice);
         basicEffect.View = camera.View;
         basicEffect.Projection = camera.Projection;
         foreach (var gridlet in navigationGrid.Gridlets) {
            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.Default);
            for (var y = 0; y < gridlet.YLength; y++) {
               for (var x = 0; x < gridlet.XLength; x++) {
                  var cellIndex = y * gridlet.XLength + x;
                  var cellHeight = gridlet.Cells[cellIndex].Height;
                  var cellFlags = gridlet.Cells[cellIndex].Flags;
                  var transform = gridlet.Cells[cellIndex].OrientedBoundingBox.Transformation;
                  basicEffect.World = cubeModelTransform * transform;
                  if (cellFlags.HasFlag(CellFlags.Connector)) {
                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                     basicEffect.DiffuseColor = new Vector4(color, color / 2, 0, 1.0f);
                  } else if (cellFlags.HasFlag(CellFlags.Debug)) {
                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                     basicEffect.DiffuseColor = new Vector4(0.0f, 0, color, 1.0f);
                  } else if (cellFlags.HasFlag(CellFlags.Blocked)) {
                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                     basicEffect.DiffuseColor = new Vector4(color, 0.0f, 0, 1.0f);
                  } else if (cellFlags.HasFlag(CellFlags.Edge)) {
                     var color = ((x + y) % 2 == 0) ? 0.6f : 0.8f;
                     basicEffect.DiffuseColor = new Vector4(0.0f, color, 0, 1.0f);
                  } else {
                     var color = ((x + y) % 2 == 0) ? 0.2f : 0.4f;
                     basicEffect.DiffuseColor = new Vector4(color, color, color, 1.0f);
                  }
                  cube.Draw(GraphicsDevice, basicEffect);
               }
            }
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
//         foreach (var gridlet in navigationGrid.GetGridlets(character.X, character.Y)) {
//            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.WireFrame);
//            var bb = gridlet.OrientedBoundingBox;
//            basicEffect.DiffuseColor = new Vector4(1.0f, 0, 0, 1.0f);
//            basicEffect.World = bb.Transformation * Matrix.Scaling(1.01f);
//            cube.Draw(basicEffect);
//         }

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
         var path = pathfinder.FindPath(new Vector3(0, 0, 0), new Vector3(58, -8, 4.25f));
         if (path != null) {
            var pathPoints = path.Points.ToArray();
            debugBatch.Begin();
            for (var i = 0; i < pathPoints.Length - 1; i++) {
               debugBatch.DrawLine(
                  new VertexPositionColor(pathPoints[i], Color.Lime),
                  new VertexPositionColor(pathPoints[i + 1], Color.Lime)
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

      private Ray GetPickRay(Point cursor) {
         return Ray.GetPickRay(cursor.X, cursor.Y, GraphicsDevice.Viewport, camera.View * camera.Projection);
      }
   }
}
