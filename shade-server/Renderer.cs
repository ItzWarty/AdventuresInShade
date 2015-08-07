using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace Shade {
   public class Renderer {
      private readonly List<Tuple<Vector3, Vector3, Color>> lines = new List<Tuple<Vector3, Vector3, Color>>();
      private GraphicsDevice graphicsDevice;
      private GeometricPrimitive cube;
      private RenderMesh cubeMesh;
      private BasicEffect basicEffect;
      private Effect debugEffect;
      private PrimitiveBatch<VertexPositionColor> debugBatch;

      public string Name => "Renderer";

      public void SetGraphicsDevice(GraphicsDevice graphicsDevice) {
         this.graphicsDevice = graphicsDevice;
      }

      public void Initialize() {
         cube = GeometricPrimitive.Cube.New(graphicsDevice);
         var cubeBounds = new OrientedBoundingBox(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
         cubeMesh = new RenderMesh {
            BoundingBox = cubeBounds,
            IndexBuffer = cube.IndexBuffer,
            IsIndex32Bits = cube.IsIndex32Bits,
            InputLayout = VertexInputLayout.New<VertexPositionNormalTexture>(0),
            ModelTransform = Matrix.Identity,
            VertexBuffer = cube.VertexBuffer
         };

         basicEffect = new BasicEffect(graphicsDevice);
         basicEffect.EnableDefaultLighting(); // enable default lightning, useful for quick prototyping

         var debugEffectCompilerResult = new EffectCompiler().CompileFromFile("shaders/debug_solid.hlsl", EffectCompilerFlags.Debug);
         debugEffect = new Effect(graphicsDevice, debugEffectCompilerResult.EffectData, graphicsDevice.DefaultEffectPool);
         debugBatch = new PrimitiveBatch<VertexPositionColor>(graphicsDevice);
      }

      public void BeginRender(Entity cameraEntity) {
         var cameraComponent = cameraEntity.GetComponent<CameraComponent>();

         basicEffect.View = cameraComponent.View;
         basicEffect.Projection = cameraComponent.Projection;

         graphicsDevice.SetDepthStencilState(graphicsDevice.DepthStencilStates.Default);

         lines.Clear();
      }

      public void EndRender(Entity cameraEntity) {
         var cameraComponent = cameraEntity.GetComponent<CameraComponent>();

         debugEffect.DefaultParameters.WorldParameter.SetValue(Matrix.Identity);
         debugEffect.DefaultParameters.ViewParameter.SetValue(cameraComponent.View);
         debugEffect.DefaultParameters.ProjectionParameter.SetValue(cameraComponent.Projection);
         debugEffect.CurrentTechnique.Passes[0].Apply();

         graphicsDevice.SetDepthStencilState(graphicsDevice.DepthStencilStates.None);

         debugBatch.Begin();
         foreach (var line in lines) {
            debugBatch.DrawLine(
               new VertexPositionColor(line.Item1, line.Item3),
               new VertexPositionColor(line.Item2, line.Item3)
            );
         }
         debugBatch.End();
      }

      public void DrawCube(Matrix worldMatrix, Vector4 color, bool wireframe) {
         if (wireframe) {
            graphicsDevice.SetRasterizerState(graphicsDevice.RasterizerStates.WireFrame);
         } else {
            graphicsDevice.SetRasterizerState(graphicsDevice.RasterizerStates.Default);
         }
         basicEffect.DiffuseColor = color;
         basicEffect.World = worldMatrix;
         cube.Draw(basicEffect);
      }

      public void DrawOrientedBoundingBox(OrientedBoundingBox obb, Vector4 color, float scale = 1.01f) {
         var worldMatrix = Matrix.Scaling(obb.Extents * 2) * Matrix.Scaling(scale) * obb.Transformation;
         DrawCube(worldMatrix, color, true);
      }

      public void DrawDebugLine(Vector3 start, Vector3 end, Color? color = null) {
         lines.Add(new Tuple<Vector3, Vector3, Color>(start, end, color ?? Color.Cyan));
      }

      public void RenderEntity(Entity entity) {
         var positionComponent = entity.GetComponent<PositionComponent>();
         var sizeComponent = entity.GetComponent<SizeComponent>();
         var orientationComponent = entity.GetComponent<OrientationComponent>();
         var boundsComponent = entity.GetComponent<BoundsComponent>();
         var colorComponent = entity.GetComponent<ColorComponent>();
         var pathingComponent = entity.GetComponentOrNull<PathingComponent>();
         var zNudge = Vector3.Zero;
         if (sizeComponent.PositioningMode == VerticalPositioningMode.PositionBottom) {
            zNudge.Z = 0.5f;
         }

         var worldMatrix = Matrix.Translation(zNudge) *
                           Matrix.Scaling(sizeComponent.Size) * 
                           Matrix.RotationQuaternion(orientationComponent.Orientation) * 
                           Matrix.Translation(positionComponent.Position);

         DrawCube(worldMatrix, colorComponent.Color, false);
         DrawOrientedBoundingBox(boundsComponent.Bounds, new Vector4(1, 1, 1, 1));

         if (pathingComponent != null && pathingComponent.Path != null) {
            var pathPoints = pathingComponent.Path.Points.ToArray();
            for (var i = 0; i < pathPoints.Length - 1; i++) {
               DrawDebugLine(pathPoints[i], pathPoints[i + 1]);
            }
         }
      }
   }
}
