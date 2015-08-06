using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Shade {
   public class EventBus<TEventArgs> {
      public event EventHandler<TEventArgs> Event;

      public void Trigger(TEventArgs e) {
         Event?.Invoke(this, e);
      }
   }

   public class MouseEventBus : EventBus<MouseEventInfo> {
      public void Trigger(MouseEventType type, MouseEventArgs e) {
         Trigger(new MouseEventInfo(e.Button, e.Clicks, e.X, e.Y, e.Delta, type));
      }
   }

   public class MouseEventInfo {
      private readonly MouseButtons button;
      private readonly int clicks;
      private readonly int x;
      private readonly int y;
      private readonly int delta;
      private readonly MouseEventType type;

      public MouseEventInfo(MouseButtons button, int clicks, int x, int y, int delta, MouseEventType type) {
         this.button = button;
         this.clicks = clicks;
         this.x = x;
         this.y = y;
         this.delta = delta;
         this.type = type;
      }

      public MouseButtons Button => button;
      public int Clicks => clicks;
      public int X => x;
      public int Y => y;
      public int Delta => delta;
      public MouseEventType Type => type;
   }

   public enum MouseEventType {
      Down,
      Up,
      Move,
      Wheel
   }
}
