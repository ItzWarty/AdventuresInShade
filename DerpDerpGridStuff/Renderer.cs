using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ItzWarty.Collections;

namespace DerpDerpGridStuff {
   public class Renderer {
      private readonly Grid grid;

      private readonly IReadOnlyDictionary<ConnectorState, Pen> kConnectorStatePens = ImmutableDictionary.Of<ConnectorState, Pen>(
         ConnectorState.Banned, new Pen(Color.FromArgb(80, 0, 0)),
         ConnectorState.Unlinked, new Pen(Color.FromArgb(0, 0, 80)),
         ConnectorState.Linked, new Pen(Color.FromArgb(0, 160, 0))
      );

      private readonly IReadOnlyDictionary<CellType, Brush> kCellTypeBrushes = ImmutableDictionary.Of<CellType, Brush>(
         CellType.Undefined, Brushes.Gray,
         CellType.Entrance, Brushes.Lime,
         CellType.Exit, Brushes.Red,
         CellType.Corridor, Brushes.White,
         CellType.Arena, Brushes.Yellow
      );

      private const float scale = 20;
      private const float padding = 20;
      private Bitmap nextBitmap;

      public Renderer(Grid grid) {
         this.grid = grid;
         new Thread(UiThreadStart) { ApartmentState = ApartmentState.STA }.Start();
      }

      public void UiThreadStart() {
         var f = new Form();
         f.Show();
         var pb = new PictureBox { Image = null, SizeMode = PictureBoxSizeMode.AutoSize };
         f.Controls.Add(pb);
         while (true) {
            while (nextBitmap == null) {
               Application.DoEvents();
            }
            var capture = nextBitmap;
            nextBitmap = null;
            pb.Image = capture;
            f.ClientSize = capture.Size;
         }
      }

      public void RenderGrid(Grid grid) {
         var cellSize = 0.2f;

         if (nextBitmap != null) {
            return;
         }

         var width = grid.Cells.Max(x => x.X);
         var height = grid.Cells.Max(x => x.Y);
         var bitmap = new Bitmap((int)(width * scale + padding * 2), (int)(height * scale + padding * 2));
         using (var g = Graphics.FromImage(bitmap)) {
            g.Clear(Color.Black);
            foreach (var cell in grid.Cells) {
               using (var keyPen = new Pen(cell.KeyColor)) {
                  g.DrawEllipse(keyPen, (cell.X - cellSize * 2) * scale + padding, (cell.Y - cellSize * 2) * scale + padding, 4 * cellSize * scale, 4 * cellSize * scale);
               }

               using (var lockPen = new Pen(cell.LockColor)) {
                  g.DrawRectangle(lockPen, (cell.X - cellSize) * scale + padding, (cell.Y - cellSize) * scale + padding, 2 * cellSize * scale, 2 * cellSize * scale);
               }

               g.FillRectangle(kCellTypeBrushes[cell.Type], (cell.X - cellSize / 2) * scale + padding, (cell.Y - cellSize / 2) * scale + padding, cellSize * scale, cellSize * scale);
            }

            foreach (var connector in grid.Cells.SelectMany(x => x.Connectors).Distinct()) {
               var connectorStatePen = kConnectorStatePens[connector.State];
               g.DrawLine(connectorStatePen, connector.First.X * scale + padding, connector.First.Y * scale + padding, connector.Second.X * scale + padding, connector.Second.Y * scale + padding);
            }
         }
         nextBitmap = bitmap;
      }
   }
}
