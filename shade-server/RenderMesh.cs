using SharpDX;
using SharpDX.Toolkit.Graphics;

namespace Shade {
   public class RenderMesh {
      public VertexInputLayout InputLayout { get; set; }
      public bool IsIndex32Bits { get; set; }
      public Buffer IndexBuffer { get; set; }
      public Buffer<VertexPositionNormalTexture> VertexBuffer { get; set; }
      public OrientedBoundingBox BoundingBox { get; set; }
      public Matrix ModelTransform { get; set; }
   }
}
