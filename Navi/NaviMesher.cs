using Poly2Tri;
using System.Collections.Generic;
using System.Linq;
using ItzWarty;

namespace Navi {
   public class NavMeshPrimitiveCollection : List<IReadOnlyList<Vec3>>  {

   }

   public class NavMeshNode {
      public NavMeshNode(NavMeshUnit unit) {
         Unit = unit;
      }

      public NavMeshUnit Unit { get; }
      public HashSet<NavMeshNode> Neighbors { get; set; }  = new HashSet<NavMeshNode>();

      public void AddNeighbor(NavMeshNode other) {
         Neighbors.Add(other);
         other.Neighbors.Add(this);
      }
   }

   public class NavMeshUnit {
      public NavMeshUnit(NavMeshPrimitiveCollection land, NavMeshPrimitiveCollection holes) {
         Land = land;
         Holes = holes;
      }

      public NavMeshPrimitiveCollection Land { get; private set; }
      public NavMeshPrimitiveCollection Holes { get; private set; }
      public TriangulationQuadTree QuadTree { get; set; }
   }

   public class NavMeshUnion {
      public NavMeshUnion(NavMeshNode first, NavMeshNode second) {
         First = first;
         Second = second;
      }

      public NavMeshNode First { get; }
      public NavMeshNode Second { get; }

      public override int GetHashCode() {
         return First.GetHashCode() ^ Second.GetHashCode();
      }

      public override bool Equals(object obj) {
         var other = obj as NavMeshUnion;
         return other != null && (
            (other.First == this.First && other.Second == this.Second) ||
            (other.First == this.Second && other.Second == this.First));
      }
   }

   public struct Vec3 {
      public double X;
      public double Y;
      public double Z;

      public Vec3(double x, double y, double z) {
         this.X = x;
         this.Y = y;
         this.Z = z;
      }
   }

   public struct Vec2 {
      public double X;
      public double Y;

      public Vec2(double x, double y) {
         this.X = x;
         this.Y = y;
      }
   }
}
