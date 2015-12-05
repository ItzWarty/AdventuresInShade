using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ItzWarty;
using ItzWarty.Collections;
using ItzWarty.Geometry;
using Poly2Tri;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.Toolkit.Graphics;
using Bitmap = System.Drawing.Bitmap;
using Buffer = SharpDX.Toolkit.Graphics.Buffer;
using Color = SharpDX.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point2D = ItzWarty.Geometry.Point2D;
using SamplerState = SharpDX.Toolkit.Graphics.SamplerState;
using Texture2D = SharpDX.Toolkit.Graphics.Texture2D;
using VertexBufferBinding = SharpDX.Direct3D11.VertexBufferBinding;
using ICL = ItzWarty.Collections;

namespace Shade {
   public class GridletComponent : EntityComponent {
      private readonly NavigationGrid grid;
      private readonly NavigationGridlet gridlet;
      private bool isGraphicsInitialized = false;
      private VertexBufferBinding x;
      private RenderComponent renderComponent;
      private Buffer<VertexPositionNormalTexture> vertexBuffer;
      private VertexInputLayout vertexInputLayout;
      private Buffer<int> indexBuffer;
      private Texture2D debugTexture;

      public GridletComponent(NavigationGrid grid, NavigationGridlet gridlet) {
         this.grid = grid;
         this.gridlet = gridlet;
      }

      public override void Initialize() {
         base.Initialize();
         renderComponent = Entity.GetComponent<RenderComponent>();
         renderComponent.IsCustomRendered = true;
         renderComponent.Render += HandleRender;
      }

      private void HandleRender(object sender, RenderEventArgs e) {
         var startPoint = new Point2D(15, 5);
         var endPoint = new Point2D(55, 40);
         this.gridlet.Cells[(int)(startPoint.X + startPoint.Y * gridlet.XLength)].Flags |= CellFlags.Debug;
         this.gridlet.Cells[(int)(endPoint.X + endPoint.Y * gridlet.XLength)].Flags |= CellFlags.Debug;

         if (!isGraphicsInitialized) {
            InitializeGraphics(e.GraphicsDevice);
            isGraphicsInitialized = true;
         }

         var graphicsDevice = e.GraphicsDevice;
         var renderer = e.Renderer;
         e.BasicEffect.World = gridlet.Orientation * Matrix.Translation(gridlet.X, gridlet.Y, gridlet.Z);
         e.BasicEffect.Texture = debugTexture;
         e.BasicEffect.TextureEnabled = true;
         e.BasicEffect.DiffuseColor = Vector4.One;
         var samplerStateDescription = SamplerStateDescription.Default();
         samplerStateDescription.Filter = Filter.ComparisonMinMagMipPoint;
         e.BasicEffect.Sampler = SamplerState.New(graphicsDevice, samplerStateDescription);
         e.BasicEffect.CurrentTechnique.Passes[0].Apply();
         graphicsDevice.SetRasterizerState(graphicsDevice.RasterizerStates.CullBack);
         graphicsDevice.SetVertexBuffer(0, vertexBuffer, 0);
         graphicsDevice.SetVertexInputLayout(vertexInputLayout);
         graphicsDevice.SetIndexBuffer(indexBuffer, true);
         graphicsDevice.DrawIndexed(PrimitiveType.TriangleList, indexBuffer.ElementCount);

         var meshTransform = gridlet.Orientation * Matrix.Translation(gridlet.X, gridlet.Y, gridlet.Z);
         foreach (var meshTriangle in gridlet.Mesh) {
            var a = new Vector3((float)meshTriangle.Points[0].X, (float)meshTriangle.Points[0].Y, 0);
            var b = new Vector3((float)meshTriangle.Points[1].X, (float)meshTriangle.Points[1].Y, 0);
            var c = new Vector3((float)meshTriangle.Points[2].X, (float)meshTriangle.Points[2].Y, 0);
            Vector3.Transform(ref a, ref meshTransform, out a);
            Vector3.Transform(ref b, ref meshTransform, out b);
            Vector3.Transform(ref c, ref meshTransform, out c);
            //            var n = meshTriangle. Vector3.Cross((a - b), b - c);
            var color = true ? Color.Cyan : Color.Green;
            renderer.DrawDebugLine(a, b, color);
            ;
            renderer.DrawDebugLine(b, c, color);
            renderer.DrawDebugLine(c, a, color);

            foreach (var neighbor in meshTriangle.Neighbors.Where(n => n != null)) {
               Console.WriteLine(neighbor.IsInterior);
            }

            foreach (var neighbor in meshTriangle.Neighbors.Where(n => n != null && n.IsInterior)) {
               Console.WriteLine("!@#!@#@#");
               var meshCentroid = meshTriangle.Centroid();
               var neighborCentroid = neighbor.Centroid();
               var centroidA = new Vector3(meshCentroid.Xf, meshCentroid.Yf, 0);
               var centroidB = new Vector3(neighborCentroid.Xf, neighborCentroid.Yf, 0);
               Vector3.Transform(ref centroidA, ref meshTransform, out centroidA);
               Vector3.Transform(ref centroidB, ref meshTransform, out centroidB);
               renderer.DrawDebugLine(
                  centroidA,
                  centroidB,
                  Color.Red
               );
            }
         }

         foreach (var neighbor in gridlet.Neighbors) {
            renderer.DrawDebugLine(
               gridlet.OrientedBoundingBox.Center,
               neighbor.OrientedBoundingBox.Center,
               Color.Magenta
            );
         }

         var mesh = gridlet.Mesh;
         var startTriangle = mesh.First(
            x => new Triangle2D(
               new Point2D(x.Points[0].Xf, x.Points[0].Yf), 
               new Point2D(x.Points[1].Xf, x.Points[1].Yf), 
               new Point2D(x.Points[2].Xf, x.Points[2].Yf)).Contains(startPoint - new Point2D(gridlet.XLength / 2.0, gridlet.YLength / 2.0))
         );
         var endTriangle = mesh.First(
            x => new Triangle2D(
               new Point2D(x.Points[0].Xf, x.Points[0].Yf), 
               new Point2D(x.Points[1].Xf, x.Points[1].Yf), 
               new Point2D(x.Points[2].Xf, x.Points[2].Yf)).Contains(endPoint - new Point2D(gridlet.XLength / 2.0, gridlet.YLength / 2.0))
         );
         
         ConcurrentDictionary<DelaunayTriangle, double> distancesByTriangle = new ConcurrentDictionary<DelaunayTriangle, double>();
         distancesByTriangle.TryAdd(startTriangle, 0);
         var lastCount = 0;
         while (lastCount != distancesByTriangle.Count) {
            lastCount = distancesByTriangle.Count;
            foreach (var kvp in distancesByTriangle) {
               var triangle = kvp.Key;
               var distance = kvp.Value;
               foreach (var neighbor in triangle.Neighbors.Where(n => n != null && n.IsInterior).Where(n => n.Points.Union(triangle.Points).Distinct().Count() == 4)) {
                  var triangleCentroid = triangle.Centroid();
                  if (triangle == startTriangle) {
                     triangleCentroid = new TriangulationPoint(startPoint.X - gridlet.XLength / 2.0, startPoint.Y - gridlet.YLength / 2.0);
                  } else if (triangle == endTriangle) {
                     triangleCentroid = new TriangulationPoint(endPoint.X - gridlet.XLength / 2.0, endPoint.Y - gridlet.YLength / 2.0);
                  }
                  var neighborCentorid = neighbor.Centroid();
                  var neighborDistance = distance + Math.Pow(triangleCentroid.X - neighborCentorid.X, 2) + Math.Pow(triangleCentroid.Y - neighborCentorid.Y, 2);
                  distancesByTriangle.AddOrUpdate(
                     neighbor,
                     add => neighborDistance,
                     (update, existing) => Math.Min(existing, neighborDistance)
                  );
               }
            }
         }
         foreach (var kvp in distancesByTriangle) {
            var triangle = kvp.Key;
            var distance = kvp.Value;
            foreach (var neighbor in triangle.Neighbors.Where(n => n != null && n.IsInterior)) {
               var triangleCentroid = triangle.Centroid();
               var neighborCentorid = neighbor.Centroid();
               var neighborDistance = distance + Math.Pow(triangleCentroid.X - neighborCentorid.X, 2) + Math.Pow(triangleCentroid.Y - neighborCentorid.Y, 2);
               distancesByTriangle.AddOrUpdate(
                  neighbor,
                  add => neighborDistance,
                  (update, existing) => Math.Min(existing, neighborDistance)
               );
            }
         }
         var path = new List<DelaunayTriangle>();
         {
            var currentNode = endTriangle;
            while (currentNode != startTriangle) {
               path.Insert(0, currentNode);
               currentNode = currentNode.Neighbors.Where(n => n != null && n.IsInterior).MinBy(distancesByTriangle.Get);
            }
            path.Insert(0, currentNode);
         }

         for (var i = 0; i < path.Count - 1; i++) {
            var startCentroid = new Vector3(path[i].Centroid().Xf, path[i].Centroid().Yf, 0);
            var endCentroid = new Vector3(path[i + 1].Centroid().Xf, path[i + 1].Centroid().Yf, 0);

            Vector3.Transform(ref startCentroid, ref meshTransform, out startCentroid);
            Vector3.Transform(ref endCentroid, ref meshTransform, out endCentroid);
            e.Renderer.DrawDebugLine(startCentroid, endCentroid, Color.White);
         }
//         path = new[] { 5, 6, 13, 12, 11, 0 }.Select(mesh.Get).ToList();
         {

            var finalPath = new List<Point2D>();
            var currentPoint = new Point2D(startPoint.X - Gridlet.XLength / 2.0, startPoint.Y - gridlet.YLength / 2.0);
            var currentSharedPoints = path[0].Points.Intersect(path[1].Points).ToArray();
            finalPath.Add(currentPoint);
            try {
               for (var i = 1; i < path.Count; i++) {
                  TriangulationPoint[] sharedPoints;
                  Point2D commonPoint, newPoint, oldOther;
                  if (i < path.Count - 1) {
                     sharedPoints = path[i].Points.Intersect(path[i + 1].Points).ToArray();
                     var commonPoints = sharedPoints.Intersect(currentSharedPoints).ToArray();
                     Trace.Assert(commonPoints.Length == 1);
                     var commonPoint_ = commonPoints[0];
                     commonPoint = new Point2D(commonPoint_.X, commonPoint_.Y);
                     var newPoint_ = sharedPoints.First(p => !p.Equals(commonPoint_));
                     newPoint = new Point2D(newPoint_.X, newPoint_.Y);
                     var oldOther_ = currentSharedPoints.First(p => !p.Equals(commonPoint_));
                     oldOther = new Point2D(oldOther_.X, oldOther_.Y);
                  } else {
                     sharedPoints = null;
                     var commonPoint_ = currentSharedPoints.First();
                     commonPoint = new Point2D(commonPoint_.X, commonPoint_.Y);
                     var oldOther_ = currentSharedPoints.Skip(1).First();
                     oldOther = new Point2D(oldOther_.X, oldOther_.Y);
                     newPoint = new Point2D(endPoint.X - Gridlet.XLength / 2.0, endPoint.Y - gridlet.YLength / 2.0);

                     var windingOldCurrentNew = GeometryUtilities.GetClockness(oldOther, currentPoint, newPoint);
                     var windingOldCurrentCommon = GeometryUtilities.GetClockness(oldOther, currentPoint, commonPoint);
                     if (windingOldCurrentNew == windingOldCurrentCommon) {
                        var temp = commonPoint;
                        commonPoint = oldOther;
                        oldOther = temp;
                     }
                  }

                  var oldOtherVector = new Vector2D(currentPoint, oldOther).ToUnitVector();
                  var commonVector = new Vector2D(currentPoint, commonPoint).ToUnitVector();
                  var newVector = new Vector2D(currentPoint, newPoint).ToUnitVector();

                  var oldAngle = Math.Acos(oldOtherVector.Dot(commonVector));
                  var newAngle = Math.Acos(newVector.Dot(commonVector));
                  var oldWinding = GeometryUtilities.GetClockness(commonPoint, currentPoint, oldOther);
                  var newWinding = GeometryUtilities.GetClockness(commonPoint, currentPoint, newPoint);

                  if (oldAngle > newAngle && oldWinding == newWinding) {
                     currentSharedPoints = sharedPoints;
                  } else {
                     finalPath.Add(oldOther);
                     currentPoint = oldOther;
                     currentSharedPoints = sharedPoints;
                  }

//                  if (i == 4) {
//                     Console.WriteLine(i + " SDFDFSD " + path.Count);
//                     var currentPointVect = new Vector3((float)currentPoint.X, (float)currentPoint.Y, 0);
//                     var commonPointVect = new Vector3((float)commonPoint.X, (float)commonPoint.Y, 0);
//                     var oldOtherPointVect = new Vector3((float)oldOther.X, (float)oldOther.Y, 0);
//                     var newPointVect = new Vector3((float)newPoint.X, (float)newPoint.Y, 0);
//
//                     Vector3.Transform(ref currentPointVect, ref meshTransform, out currentPointVect);
//                     Vector3.Transform(ref commonPointVect, ref meshTransform, out commonPointVect);
//                     Vector3.Transform(ref oldOtherPointVect, ref meshTransform, out oldOtherPointVect);
//                     Vector3.Transform(ref newPointVect, ref meshTransform, out newPointVect);
//                     e.Renderer.DrawDebugLine(currentPointVect, commonPointVect, Color.Green);
//                     e.Renderer.DrawDebugLine(currentPointVect, oldOtherPointVect, Color.Orange);
//                     e.Renderer.DrawDebugLine(currentPointVect, newPointVect, Color.Chocolate);
//                     renderer.DrawCube(Matrix.Translation((float)currentPointVect.X, (float)currentPointVect.Y, 1), new Vector4(0, 0, 1, 1), false);
//                     renderer.DrawCube(Matrix.Translation((float)commonPointVect.X, (float)commonPointVect.Y, 1), new Vector4(0, 1, 0, 1), false);
//                     renderer.DrawCube(Matrix.Translation((float)oldOtherPointVect.X, (float)oldOtherPointVect.Y, 1), new Vector4(1, 0.5f, 0, 1), false);
//                     renderer.DrawCube(Matrix.Translation((float)newPointVect.X, (float)newPointVect.Y, 1), new Vector4(0.5f, 0.25f, 0, 1), false);
//
//                     foreach (var p in finalPath) {
//                        renderer.DrawCube(Matrix.Translation((float)p.X, (float)p.Y, 1.5f), new Vector4(1, 1, 1, 1), false);
//                     }
//
//                     for (var j = 0; j < finalPath.Count - 1; j++) {
//                        Console.WriteLine("!@#@!###!@ " + finalPath.Count);
//                        var startCentroid = new Vector3((float)finalPath[j].X, (float)finalPath[j].Y, 0);
//                        var endCentroid = new Vector3((float)finalPath[j + 1].X, (float)finalPath[j + 1].Y, 0);
//
//                        Vector3.Transform(ref startCentroid, ref meshTransform, out startCentroid);
//                        Vector3.Transform(ref endCentroid, ref meshTransform, out endCentroid);
//                        e.Renderer.DrawDebugLine(startCentroid, endCentroid, Color.Magenta);
//                     }
//                     break;
//                  }
               }
               finalPath.Add(new Point2D(endPoint.X - Gridlet.XLength / 2.0, endPoint.Y - gridlet.YLength / 2.0));

               var simplePathLine = new Line2D(
                  new Point2D(startPoint.X - Gridlet.XLength / 2.0, startPoint.Y - gridlet.YLength / 2.0), 
                  new Point2D(endPoint.X - Gridlet.XLength / 2.0, endPoint.Y - gridlet.YLength / 2.0)
               );

//               for (var t = 0.0f; t < 1.0f; t+= 0.1f) {
//                  var p = simplePathLine.PointAtT(t);
//                  renderer.DrawCube(Matrix.Scaling(2) * Matrix.Translation((float)p.X, (float)p.Y, 2), new Vector4(1, 1, 1, 1), false);
//               }

//               var triangle = new Triangle2D(
//                  new Point2D(-32.5, 32.5),
//                  new Point2D(-10, 0),
//                  new Point2D(-32.5, -32.5));
//               MessageBox.Show(triangle.FindIntersection(simplePathLine)?.ToString() ?? "null");
//               var abLine = new Line2D(triangle.A, triangle.B);
//               var abIntersection = (Point2D)abLine.FindLineIntersection(simplePathLine);
//               var bcLine = new Line2D(triangle.B, triangle.C);
//               var bcIntersection = (Point2D)bcLine.FindLineIntersection(simplePathLine);
//               Console.WriteLine("AB INTERSECTION: " + abLine.NearestT(abIntersection) + " " + abLine.PointAtT(abLine.NearestT(abIntersection)) + " " + abIntersection);
//               Console.WriteLine("BC INTERSECTION: " + bcLine.NearestT(bcIntersection));

//               var f = new Form();
//               var pb = new PictureBox();
//               var b = new Bitmap(100, 100);
//               using (var g = Graphics.FromImage(b)) {
//                  for (var t = 0.0; t < 1.0f; t += 0.05f) {
//                     var p = abLine.PointAtT(t);
//                     g.DrawRectangle(Pens.Red, (float)(p.X + 50 - 1), (float)(p.Y + 50 - 1), 2, 2);
//                  }
//
//                  g.DrawRectangle(Pens.Orange, (float)(abIntersection.X + 50 - 3), (float)(abIntersection.Y + 50 - 3), 6, 6);
//
//
//                  g.DrawRectangle(Pens.Red, (float)(triangle.A.X + 50 - 1), (float)(triangle.A.Y + 50 - 1), 2, 2);
//                  g.DrawRectangle(Pens.Green, (float)(triangle.B.X + 50 - 1), (float)(triangle.B.Y + 50 - 1), 2, 2);
//                  g.DrawRectangle(Pens.Blue, (float)(triangle.C.X + 50 - 1), (float)(triangle.C.Y + 50 - 1), 2, 2);
//                  g.DrawLine(Pens.Lime, -25 + 50, -25 + 50, -20 + 50, 25 + 50);
//               }
//               pb.Image = b;
//               pb.SizeMode = PictureBoxSizeMode.AutoSize;
//               f.Controls.Add(pb);
//               f.ClientSize = b.Size;
//               f.Show();
//               Application.Run();


               var tritris = mesh.Where(
                  x => new Triangle2D(
                     new Point2D(x.Points[0].Xf, x.Points[0].Yf),
                     new Point2D(x.Points[1].Xf, x.Points[1].Yf),
                     new Point2D(x.Points[2].Xf, x.Points[2].Yf)).FindIntersection(simplePathLine) != null
               ).ToArray();
               if (tritris.Any()) {
                  var sadfaces = new ICL.HashSet<DelaunayTriangle>(tritris);
                  var s = new Stack<DelaunayTriangle>();
                  s.Push(tritris.First());
                  var lastSadfacesCount = 0;
                  while (s.Any() && lastSadfacesCount != sadfaces.Count) {
                     lastSadfacesCount = sadfaces.Count;
                     var tri = s.Pop();
                     sadfaces.Remove(tri);
                     foreach (var other in sadfaces) {
                        if (other.Points.Union(tri.Points).Distinct().Count() <= 4) {
                           s.Push(other);
                        }
                     }
                  }
                  if (sadfaces.None()) {
//                     MessageBox.Show("!@#!#!@#");
                     finalPath.Clear();
                     finalPath.Add(simplePathLine.Start);
                     finalPath.Add(simplePathLine.PointAtT(1));
                  }
               }

               Console.WriteLine("!@#!@#!@ " + tritris.SelectMany(x => x.Points).Distinct().Count() + " " + tritris.Length);

               Console.WriteLine("!@#!@##@@!#@!@!@# " + tritris.Length);
               foreach (var tri in tritris) {
                  var centroid = tri.Centroid();
                  var centroidVector = new Vector3(centroid.Xf, centroid.Yf, 0);
                  renderer.DrawCube(Matrix.Scaling(4) * Matrix.Translation(centroidVector), new Vector4(0, 1, 1, 1), false);
               }

               foreach (var p in finalPath) {
                  renderer.DrawCube(Matrix.Translation((float)p.X, (float)p.Y, 1.5f), new Vector4(1, 1, 1, 1), false);
               }

               for (var j = 0; j < finalPath.Count - 1; j++) {
                  Console.WriteLine("!@#@!###!@ " + finalPath.Count);
                  var startCentroid = new Vector3((float)finalPath[j].X, (float)finalPath[j].Y, 0);
                  var endCentroid = new Vector3((float)finalPath[j + 1].X, (float)finalPath[j + 1].Y, 0);

                  Vector3.Transform(ref startCentroid, ref meshTransform, out startCentroid);
                  Vector3.Transform(ref endCentroid, ref meshTransform, out endCentroid);
                  e.Renderer.DrawDebugLine(startCentroid, endCentroid, Color.Magenta);
               }
            } catch (Exception exsaqsd) {
               Console.WriteLine(exsaqsd);
            }

//            for (var i = 0; i < finalPath.Count - 1; i++) {
//               var startCentroid = new Vector3((float)finalPath[i].X - Gridlet.XLength / 2.0f, (float)finalPath[i].Y - Gridlet.YLength / 2.0f, 0);
//               var endCentroid = new Vector3((float)finalPath[i + 1].X - Gridlet.XLength / 2.0f, (float)finalPath[i + 1].Y - Gridlet.YLength / 2.0f, 0);

//               Vector3.Transform(ref startCentroid, ref meshTransform, out startCentroid);
//               Vector3.Transform(ref endCentroid, ref meshTransform, out endCentroid);
//               e.Renderer.DrawDebugLine(startCentroid, endCentroid, Color.Magenta);
//            }
         }
      }

      private unsafe void InitializeGraphics(GraphicsDevice graphicsDevice) {
         var colorBitmap = new Bitmap(gridlet.XLength, gridlet.YLength, PixelFormat.Format32bppRgb);
         var bitmapData = colorBitmap.LockBits(new System.Drawing.Rectangle(0, 0, gridlet.XLength, gridlet.YLength), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
         var pScan0 = (byte*)bitmapData.Scan0;
         for (var y = 0; y < gridlet.YLength; y++) {
            var pCurrentPixel = pScan0 + bitmapData.Stride * y;
            for (var x = 0; x < gridlet.XLength; x++) {
               var cell = gridlet.Cells[x + gridlet.XLength * y];
               uint color = 0xEFEFEF;
               if (cell.Flags.HasFlag(CellFlags.Debug)) {
                  color = 0x0000FF;
               } else if (cell.Flags.HasFlag(CellFlags.Connector)) {
                  color = 0xFF7F00;
               } else if (cell.Flags.HasFlag(CellFlags.Edge)) {
                  color = 0x00FF00;
               } else if (cell.Flags.HasFlag(CellFlags.Blocked)) {
                  color = 0xFF0000;
               } 
               *(uint*)pCurrentPixel = color;
               pCurrentPixel += 4;
            }
         }
         colorBitmap.UnlockBits(bitmapData);
         var ms = new MemoryStream();
         colorBitmap.Save(ms, ImageFormat.Bmp);
         ms.Position = 0;
         var image = SharpDX.Toolkit.Graphics.Image.Load(ms);
//         debugTexture = Texture2D.Load(graphicsDevice, @"E:\lolmodprojects\Project Master Yi\masteryi_base_r_cas_shockwave.dds");
         debugTexture = Texture2D.New(graphicsDevice, image);
         var vertices = new VertexPositionNormalTexture[gridlet.XLength * gridlet.YLength * 4];
         var indices = new int[gridlet.XLength * gridlet.YLength * 6 + (gridlet.XLength - 1) * (gridlet.YLength - 1) * 12];
         int ibOffsetBase = 0;
         var xOffset = -gridlet.XLength / 2.0f;
         var yOffset = -gridlet.YLength / 2.0f;
         for (var y = 0; y < gridlet.YLength; y++) {
            for (var x = 0; x < gridlet.XLength; x++) {
               var cellIndex = x + y * gridlet.XLength;
               var cell = gridlet.Cells[cellIndex];
               var cellHeight = cell.Height;
               var vbOffset = cellIndex * 4;
               var uv = new Vector2(x / (float)(gridlet.XLength - 1), y / (float)(gridlet.YLength - 1));
               vertices[vbOffset + 0] = new VertexPositionNormalTexture(new Vector3(x + xOffset, y + yOffset, cellHeight), Vector3.UnitZ, uv);
               vertices[vbOffset + 1] = new VertexPositionNormalTexture(new Vector3(x + 1 + xOffset, y + yOffset, cellHeight), Vector3.UnitZ, uv);
               vertices[vbOffset + 2] = new VertexPositionNormalTexture(new Vector3(x + 1 + xOffset, y + 1 + yOffset, cellHeight), Vector3.UnitZ, uv);
               vertices[vbOffset + 3] = new VertexPositionNormalTexture(new Vector3(x + xOffset, y + 1 + yOffset, cellHeight), Vector3.UnitZ, uv);

               var ibOffset = ibOffsetBase + cellIndex * 6;
               indices[ibOffset + 0] = vbOffset;
               indices[ibOffset + 1] = vbOffset + 3;
               indices[ibOffset + 2] = vbOffset + 1;
               indices[ibOffset + 3] = vbOffset + 1;
               indices[ibOffset + 4] = vbOffset + 3;
               indices[ibOffset + 5] = vbOffset + 2;
            }
         }

         ibOffsetBase = gridlet.XLength * gridlet.YLength * 6;
         for (var y = 0; y < gridlet.YLength - 1; y++) {
            for (var x = 0; x < gridlet.XLength - 1; x++) {
               var cellIndex = x + y * gridlet.XLength;
               var cell = gridlet.Cells[cellIndex];
               var cellHeight = cell.Height;
               var vbOffset = cellIndex * 4;
               var rightVbOffset = vbOffset + 4;
               var downVbOffset = vbOffset + gridlet.XLength * 4;

               var ibOffset = ibOffsetBase + (x + y * (gridlet.XLength - 1)) * 12;
               indices[ibOffset + 0] = vbOffset + 1;
               indices[ibOffset + 1] = vbOffset + 2;
               indices[ibOffset + 2] = rightVbOffset;
               indices[ibOffset + 3] = rightVbOffset;
               indices[ibOffset + 4] = vbOffset + 2;
               indices[ibOffset + 5] = rightVbOffset + 3;
               indices[ibOffset + 6] = vbOffset + 2;
               indices[ibOffset + 7] = vbOffset + 3;
               indices[ibOffset + 8] = downVbOffset;
               indices[ibOffset + 9] = downVbOffset;
               indices[ibOffset + 10] = downVbOffset + 1;
               indices[ibOffset + 11] = vbOffset + 2;
            }
         }
         vertexBuffer = Buffer.Vertex.New(graphicsDevice, vertices);
         vertexInputLayout = VertexInputLayout.FromBuffer(0, vertexBuffer);
         indexBuffer = Buffer.Index.New(graphicsDevice, indices);
      }

      public NavigationGridlet Gridlet => gridlet;
      public bool IsPathingEnabled { get { return gridlet.IsEnabled; } set { gridlet.IsEnabled = value; OnPropertyChanged(); } }
   }
}