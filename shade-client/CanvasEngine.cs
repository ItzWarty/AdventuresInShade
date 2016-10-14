using System;
using System.Drawing;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using Color = SharpDX.Color;
using Point = SharpDX.Point;

namespace Shade {
   public class CanvasEngine : Game {
      private readonly CanvasProgram program;
      private SpriteBatch spriteBatch;

      public CanvasEngine(CanvasProgram program) {
         this.program = program;

         var graphicsDeviceManager = new GraphicsDeviceManager(this) {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags.Debug
         };
      }

      public Canvas RootCanvas { get; private set; }

      protected override void Initialize() {
         base.Initialize();
         spriteBatch = new SpriteBatch(GraphicsDevice);
         
         Canvas.Engine = this;
         RootCanvas = new Canvas(GraphicsDevice.BackBuffer.Width, GraphicsDevice.BackBuffer.Height);

         program.Engine = this;
         program.Setup();
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);
         program.Render(gameTime);
         GraphicsDevice.Clear(Color.Black);
         spriteBatch.Begin();
         spriteBatch.Draw(RootCanvas.RenderTarget, new Vector2(0, 0), Color.White);
         spriteBatch.End();
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);
         program.Step(gameTime);
      }
   }
}
