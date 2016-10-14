using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItzWarty;

namespace Navi {
   public class QuadTree<TItem, TQuery> {
      private const int kSubdivideThreshold = 4;

      private readonly Func<TItem, Rectangle> getRect;
      private readonly Func<TQuery, Point> getPoint;
      private readonly Func<TItem, TQuery, bool> test;
      private readonly Node root;

      public QuadTree(Rectangle bounds, Func<TItem, Rectangle> getRect, Func<TQuery, Point> getPoint, Func<TItem, TQuery, bool> test) {
         this.getRect = getRect;
         this.getPoint = getPoint;
         this.test = test;
         this.root = new Node {
            Bounds = new Rect {
               Left = bounds.Left,
               Right = bounds.Right,
               Bottom = bounds.Bottom,
               Top = bounds.Top
            }
         };
      }

      public void Insert(TItem value) => Insert(value, root);

      private void Insert(TItem value, Node target) {
         var valueRect = getRect(value);
         var s = new Stack<Node>();
         s.Push(target);
         while (s.Any()) {
            var node = s.Pop();
            var nodeRect = node.Bounds;
            if (!nodeRect.Intersects(valueRect)) {
               continue;
            }
            if (node.Items.Count == kSubdivideThreshold - 1) {
               SubdivideNode(nodeRect, node);
            }
            node.Items.Add(value);
            node.EnumerateQuadrants().ForEach(s.Push);
         }
      }

      private void SubdivideNode(Rect nodeRect, Node node) {
         var centerX = (nodeRect.Left + nodeRect.Right) / 2;
         var centerY = (nodeRect.Top + nodeRect.Bottom) / 2;
         node.TopLeft = new Node {
            Bounds = new Rect {
               Left = nodeRect.Left,
               Right = centerX,
               Top = nodeRect.Top,
               Bottom = centerY
            }
         };
         node.TopRight = new Node {
            Bounds = new Rect {
               Left = centerX,
               Right = nodeRect.Right,
               Top = node.Bounds.Top,
               Bottom = centerY
            }
         };
         node.BottomLeft = new Node {
            Bounds = new Rect {
               Left = nodeRect.Left,
               Right = centerX,
               Top = centerY,
               Bottom = nodeRect.Bottom
            }
         };
         node.BottomRight = new Node {
            Bounds = new Rect {
               Left = centerX,
               Right = nodeRect.Right,
               Top = centerY,
               Bottom = nodeRect.Bottom
            }
         };
         foreach (var subnode in node.EnumerateQuadrants()) {
            foreach (var item in node.Items) {
               Insert(item, subnode);
            }
         }
      }

      public IEnumerable<TItem> Find(TQuery point) {
         var results = new HashSet<TItem>();
         var p = getPoint(point);
         var s = new Stack<Node>();
         s.Push(root);
         while (s.Any()) {
            var node = s.Pop();
            if (!node.Bounds.Intersects(p)) {
               continue;
            }
            if (node.EnumerateQuadrants().Any()) {
               node.EnumerateQuadrants().ForEach(s.Push);
            } else {
               foreach (var item in node.Items) {
                  if (test(item, point)) {
                     results.Add(item);
                  }
               }
            }
         }
         return results;
      }

      private class Node {
         public Rect Bounds { get; set; }
         public Node TopLeft { get; set; }
         public Node TopRight { get; set; }
         public Node BottomLeft { get; set; }
         public Node BottomRight { get; set; }
         public List<TItem> Items { get; set; } 

         public IEnumerable<Node> EnumerateQuadrants() {
            // nullness of all quadrants is the same.
            if (TopLeft != null) {
               yield return TopLeft;
               yield return TopRight;
               yield return BottomLeft;
               yield return BottomRight;
            }
         }
      }

      private struct Rect {
         public int Left;
         public int Right;
         public int Top;
         public int Bottom;

         public bool Intersects(Point p) {
            return Left <= p.X &&
                   p.X <= Right &&
                   Top <= p.Y &&
                   p.Y <= Bottom;
         }

         public bool Intersects(Rectangle other) {
            return !(Right <= other.Left ||
                     Left >= other.Right ||
                     Bottom <= other.Top ||
                     Top >= other.Bottom);
         }
      }
   }
}
