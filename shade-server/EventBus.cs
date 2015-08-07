using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;

namespace Shade {
   public class EventBus<TEventArgs> {
      public event EventHandler<TEventArgs> Event;

      public void Trigger(TEventArgs e) {
         Event?.Invoke(this, e);
      }
   }

   public class MouseEventBus : EventBus<MouseEventInfo> {
      public void Trigger(MouseEventType type, MouseEventArgs e, Ray pickRay) {
         Trigger(new MouseEventInfo(e.Button, e.Clicks, e.X, e.Y, e.Delta, type, pickRay));
      }
   }

   public class MouseEventInfo {
      private readonly MouseButtons button;
      private readonly int clicks;
      private readonly int x;
      private readonly int y;
      private readonly int delta;
      private readonly MouseEventType type;
      private readonly Ray pickRay;

      public MouseEventInfo(MouseButtons button, int clicks, int x, int y, int delta, MouseEventType type, Ray pickRay) {
         this.button = button;
         this.clicks = clicks;
         this.x = x;
         this.y = y;
         this.delta = delta;
         this.type = type;
         this.pickRay = pickRay;
      }

      public MouseButtons Button => button;
      public int Clicks => clicks;
      public int X => x;
      public int Y => y;
      public int Delta => delta;
      public MouseEventType Type => type;
      public Ray PickRay => pickRay;
   }

   public class SceneMouseEventInfo : MouseEventInfo {
      private readonly int rank;
      private Vector3 intersectionPoint;

      public SceneMouseEventInfo(MouseButtons button, int clicks, int x, int y, int delta, MouseEventType type, Ray pickRay, int rank, Vector3 intersectionPoint) : base(button, clicks, x, y, delta, type, pickRay) {
         this.rank = rank;
         this.intersectionPoint = intersectionPoint;
      }

      public int Rank => rank;
      public Vector3 IntersectionPoint => intersectionPoint;
   }

   public enum MouseEventType {
      Down,
      Up,
      Move,
      Wheel
   }
}
