using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Toolkit;

namespace Shade {
   public class DungeonCrate : SceneElement {
      private static readonly Vector3 kCharacterSize = new Vector3(1.6f, 2.56f, 1);
      private static readonly Matrix kModelTransform = Matrix.Translation(0, 0, 0.5f);
      private readonly NavigationGrid navigationGrid;
      private readonly Character character;
      private readonly SceneRoot sceneRoot;
      private readonly DungeonKeyInventory keyInventory;
      private readonly Vector4 color;
      private Vector3 position = new Vector3(20, 0, 0);

      public DungeonCrate(NavigationGrid navigationGrid, Character character, SceneRoot sceneRoot, DungeonKeyInventory keyInventory, Vector4 color) {
         this.navigationGrid = navigationGrid;
         this.character = character;
         this.sceneRoot = sceneRoot;
         this.keyInventory = keyInventory;
         this.color = color;
      }

      public override OrientedBoundingBox Bounds => GetBounds();

      private OrientedBoundingBox GetBounds() {
         var obb = new OrientedBoundingBox(-Vector3.One / 2, Vector3.One / 2);
         obb.Scale(kCharacterSize);
         obb.Translate(position + kCharacterSize.Z * Vector3.UnitZ / 2);
         return obb;
      }

      public override void Step(GameTime gameTime) {
         position.Z = navigationGrid.GetCell(position).OrientedBoundingBox.GetCorners().Max(x => x.Z);
      }

      public override void Render(Renderer renderer) {
         var worldMatrix = kModelTransform * Matrix.Scaling(kCharacterSize) * Matrix.Translation(position);
         renderer.DrawCube(worldMatrix, color, false);
         renderer.DrawOrientedBoundingBox(Bounds, Color4.White);
      }

      public override void HandlePickRay(SceneMouseEventInfo e) {
         if (e.Rank == 0 && e.Button.HasFlag(MouseButtons.Left) && Vector3.Distance(character.Position, position) < 3 && keyInventory.Keys.Contains(color)) {
            keyInventory.Keys.Remove(color);
            sceneRoot.RemoveElement(this);
         }
      }
   }

   public class DungeonKey : SceneElement {
      private static readonly Vector3 kCharacterSize = new Vector3(0.8f, 1.28f, 0.5f);
      private static readonly Matrix kModelTransform = Matrix.Translation(0, 0, 0.5f);
      private readonly NavigationGrid navigationGrid;
      private readonly Character character;
      private readonly SceneRoot sceneRoot;
      private readonly DungeonKeyInventory keyInventory;
      private readonly Color4 color;
      private Vector3 position = new Vector3(0, 0, 20);

      public DungeonKey(NavigationGrid navigationGrid, Character character, SceneRoot sceneRoot, DungeonKeyInventory keyInventory, Color4 color) {
         this.navigationGrid = navigationGrid;
         this.character = character;
         this.sceneRoot = sceneRoot;
         this.keyInventory = keyInventory;
         this.color = color;
      }

      public override OrientedBoundingBox Bounds => GetBounds();
      public Color4 Color => color;

      private OrientedBoundingBox GetBounds() {
         var obb = new OrientedBoundingBox(-Vector3.One / 2, Vector3.One / 2);
         obb.Scale(kCharacterSize);
         obb.Translate(position + kCharacterSize.Z * Vector3.UnitZ / 2);
         return obb;
      }

      public override void Step(GameTime gameTime) {
         position.Z = navigationGrid.GetCell(position).OrientedBoundingBox.GetCorners().Max(x => x.Z);

         if (Vector3.Distance(character.Position, position) < 3) {
            sceneRoot.RemoveElement(this);
            keyInventory.Keys.Add(color);
         }
      }

      public override void Render(Renderer renderer) {
         var worldMatrix = kModelTransform * Matrix.Scaling(kCharacterSize) * Matrix.Translation(position);
         renderer.DrawCube(worldMatrix, color, false);
         renderer.DrawOrientedBoundingBox(Bounds, Color4.White);
      }
   }
}
