using System;
using ItzWarty.Collections;
using Shade.Annotations;
using SharpDX;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ItzWarty;
using SharpDX.Toolkit;

namespace Shade {
   public class EntityFactory {
      public Entity CreateUnitCubeEntity() {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(Vector3.UnitY * 10));
         AddAndInitialize(entity, new SizeComponent(Vector3.One * 3, VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.RotationAxis(Vector3.UnitZ, MathUtil.PiOverFour)));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(new Vector4(0, 1, 0, 1)));
         AddAndInitialize(entity, new PhysicsComponent(true));
         AddAndInitialize(entity, new RenderComponent(true));
         return entity;
      }

      public Entity CreateGridletEntity(NavigationGridlet gridlet, Pathfinder pathfinder) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(new Vector3(gridlet.X, gridlet.Y, gridlet.Z)));
         AddAndInitialize(entity, new SizeComponent(new Vector3(gridlet.XLength, gridlet.YLength, 1), VerticalPositioningMode.PositionCenter));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.RotationMatrix(gridlet.Orientation)));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(Vector4.One));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new GridletPathingTargetComponent(gridlet, pathfinder));
         AddAndInitialize(entity, new RenderComponent(true));
         return entity;
      }

      public Entity CreateCharacterEntity() {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(Vector3.Zero));
         AddAndInitialize(entity, new SizeComponent(new Vector3(2, 2, 2.8f), VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(new Vector4(1, 0, 0, 1)));
         AddAndInitialize(entity, new PhysicsComponent(true));
         AddAndInitialize(entity, new CharacterComponent());
         AddAndInitialize(entity, new SpeedComponent(10));
         AddAndInitialize(entity, new PathingComponent());
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new MobaCameraTargetComponent(true));
         return entity;
      }


      public Entity CreateCameraEntity(GraphicsConfiguration graphicsConfiguration, Entity followedEntity) {
         var entity = new Entity();
         AddAndInitialize(entity, new GameStepComponent());
         AddAndInitialize(entity, new PositionComponent(new Vector3()));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new CameraComponent(graphicsConfiguration));
         AddAndInitialize(entity, new MobaCameraComponent(graphicsConfiguration, followedEntity));
         return entity;
      }

      private void AddAndInitialize(Entity entity, EntityComponent component) {
         entity.AddComponent(component);
         component.Initialize();
      }
   }

   public class CameraComponent : EntityComponent {
      private readonly GraphicsConfiguration graphicsConfiguration;

      private ViewportF viewport;
      private Matrix view;
      private Matrix projection;
      private Matrix viewProjection;

      public CameraComponent(GraphicsConfiguration graphicsConfiguration) {
         this.graphicsConfiguration = graphicsConfiguration;
      }

      public ViewportF Viewport { get { return viewport; } set { viewport = value; OnPropertyChanged(); } }
      public Matrix View { get { return view; } set { view = value; OnPropertyChanged(); } }
      public Matrix Projection { get { return projection; } set { projection = value; OnPropertyChanged(); } }
      public Matrix ViewProjection { get { return viewProjection; } set { viewProjection = value; OnPropertyChanged(); } }

      public override void Initialize() {
         base.Initialize();

         Viewport = new ViewportF(0, 0, graphicsConfiguration.Width, graphicsConfiguration.Height);
      }

      public void UpdateViewProjection(Matrix view, Matrix projection) {
         this.view = view;
         this.projection = projection;
         this.viewProjection = view * projection;

         OnPropertyChanged(nameof(View));
         OnPropertyChanged(nameof(Projection));
         OnPropertyChanged(nameof(ViewProjection));
      }

      public Ray GetPickRay(int cursorX, int cursorY) {
         return Ray.GetPickRay(cursorX, cursorY, viewport, viewProjection);
      }
   }

   public class MobaCameraTargetComponent : EntityComponent {
      private bool isCameraFollowEnabled;

      public MobaCameraTargetComponent(bool isCameraFollowEnabled) {
         this.isCameraFollowEnabled = isCameraFollowEnabled;
      }

      public bool IsCameraFollowEnabled { get { return isCameraFollowEnabled; } set { isCameraFollowEnabled = value; OnPropertyChanged(); } }
   }

   public class MobaCameraComponent : EntityComponent {
      private readonly GraphicsConfiguration graphicsConfiguration;
      private GameStepComponent gameStepComponent;
      private MouseHandlerComponent mouseHandlerComponent;
      private CameraComponent cameraComponent;

      private Entity followedEntity;
      private PositionComponent followedEntityPositionComponent;
      private MobaCameraTargetComponent followedEntityCameraTargetComponent;

      private float pitch = -MathUtil.PiOverFour;
      private float yaw = 0;
      private float desiredRadius = 60 * (float)Math.Sqrt(2);
      private float currentRadius = 60 * (float)Math.Sqrt(2);

      private bool isCameraDirty = true;
      private bool isDragRotating = false;
      private int lastMouseX;
      private int lastMouseY;


      public MobaCameraComponent(GraphicsConfiguration graphicsConfiguration, Entity followedEntity) {
         this.graphicsConfiguration = graphicsConfiguration;
         this.followedEntity = followedEntity;
      }

      public Entity FollowedEntity { get { return followedEntity; } set { followedEntity = value; OnPropertyChanged(); } }

      public override void Initialize() {
         base.Initialize();

         gameStepComponent = Entity.GetComponent<GameStepComponent>();
         gameStepComponent.Event += HandleGameStep;

         mouseHandlerComponent = Entity.GetComponentOrNull<MouseHandlerComponent>();
         mouseHandlerComponent.GlobalEvent += HandleGlobalMouseEvent;

         cameraComponent = Entity.GetComponentOrNull<CameraComponent>();

         followedEntityPositionComponent = followedEntity.GetComponent<PositionComponent>();
         followedEntityCameraTargetComponent = followedEntity.GetComponent<MobaCameraTargetComponent>();

         followedEntityPositionComponent.PropertyChanged += (s, e) => isCameraDirty = true;
      }

      private void HandleGameStep(object sender, GameStepEventArgs e) {
         currentRadius = 0.8f * currentRadius + 0.2f * desiredRadius;
      }

      public void UpdateCamera() {
         if (!isCameraDirty) {
            return;
         } else {
            isCameraDirty = false;
            var targetPosition = Vector3.Zero;

            if (followedEntityCameraTargetComponent.IsCameraFollowEnabled) {
               targetPosition = followedEntityPositionComponent.Position;
            }

            var transform = Matrix.RotationX(pitch) * Matrix.RotationZ(yaw);
            Vector4 result = Vector4.Transform(new Vector4(0, -currentRadius, 0, 1.0f), transform);

            var view = Matrix.LookAtRH(new Vector3(result.X, result.Y, result.Z) + targetPosition, targetPosition, Vector3.UnitZ);
            var projection = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(60.0f), (float)graphicsConfiguration.Width / graphicsConfiguration.Height, 0.5f, 200.0f);

            cameraComponent.UpdateViewProjection(view, projection);
         }
      }

      private void HandleGlobalMouseEvent(object sender, SceneMouseEventInfo e) {
         switch (e.Type) {
            case MouseEventType.Wheel:
               Zoom(e.Delta);
               break;
            case MouseEventType.Down:
               if (e.Button.HasFlag(MouseButtons.Middle)) {
                  isDragRotating = true;
                  lastMouseX = e.X;
                  lastMouseY = e.Y;
               }
               break;
            case MouseEventType.Move:
               if (isDragRotating) {
                  var dx = e.X - lastMouseX;
                  var dy = e.Y - lastMouseY;
                  Drag(dx, dy);
                  lastMouseX = e.X;
                  lastMouseY = e.Y;
               }
               break;
            case MouseEventType.Up:
               if (e.Button.HasFlag(MouseButtons.Middle)) {
                  isDragRotating = false;
               }
               break;
         }
      }

      public void Drag(int dx, int dy) {
         yaw += -dx * 0.01f;

         pitch += -dy * 0.01f;
         pitch = Math.Min(pitch, MathUtil.PiOverTwo * 0.9f);
         pitch = Math.Max(pitch, -MathUtil.PiOverTwo * 0.9f);

         isCameraDirty = true;
      }

      public void Zoom(int delta) {
         desiredRadius -= delta * 0.02f;
         desiredRadius = Math.Min(100, Math.Max(10, desiredRadius));

         isCameraDirty = true;
      }
   }

   public class Entity {
      private readonly System.Collections.Generic.ISet<EntityComponent> components = new System.Collections.Generic.HashSet<EntityComponent>();
      private EntitySystem entitySystem;

      public EntitySystem EntitySystem => entitySystem;

      public void AddComponent(EntityComponent component) {
         component.__SetEntity(this);
         components.Add(component);
      }

      public void __SetEntitySystem(EntitySystem entitySystem) {
         this.entitySystem = entitySystem;
      }

      public TComponent GetComponent<TComponent>() where TComponent : EntityComponent => (TComponent)components.First(c => c is TComponent);
      public TComponent GetComponentOrNull<TComponent>() where TComponent : EntityComponent => (TComponent)components.FirstOrDefault(c => c is TComponent);
   }

   public class EntityComponent : INotifyPropertyChanged {
      private Entity entity;
      public event PropertyChangedEventHandler PropertyChanged;

      public Entity Entity => entity;
      public EntitySystem EntitySystem => entity.EntitySystem;

      public void __SetEntity(Entity entity) {
         this.entity = entity;
      }

      public virtual void Initialize() { }

      [NotifyPropertyChangedInvocator]
      protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
   }

   public class PositionComponent : EntityComponent {
      private Vector3 position;

      public PositionComponent(Vector3 position) {
         this.position = position;
      }

      public Vector3 Position { get { return position; } set { position = value; OnPropertyChanged(); } }
   }

   public class SizeComponent : EntityComponent {
      private Vector3 size;
      private VerticalPositioningMode verticalPositioningMode;

      public SizeComponent(Vector3 size, VerticalPositioningMode verticalPositioningMode) {
         this.size = size;
         this.verticalPositioningMode = verticalPositioningMode;
      }

      public Vector3 Size { get { return size; } set { size = value; OnPropertyChanged(); } }
      public VerticalPositioningMode PositioningMode { get { return verticalPositioningMode; } set { verticalPositioningMode = value; OnPropertyChanged(); } }
   }

   public class OrientationComponent : EntityComponent {
      private Quaternion orientation;

      public OrientationComponent(Quaternion orientation) {
         Orientation = orientation;
      }

      public Quaternion Orientation { get { return orientation; } set { orientation = value; OnPropertyChanged(); } }
   }

   public class BoundsComponent : EntityComponent {
      private OrientedBoundingBox boundingBoxCache;
      private bool isBoundingBoxDirty = true;
      private PositionComponent positionComponent;
      private SizeComponent sizeComponent;
      private OrientationComponent orientationComponent;

      public OrientedBoundingBox Bounds => GetBounds();

      public override void Initialize() {
         positionComponent = Entity.GetComponent<PositionComponent>();
         sizeComponent = Entity.GetComponent<SizeComponent>();
         orientationComponent = Entity.GetComponent<OrientationComponent>();
         positionComponent.PropertyChanged += HandleDependencyChanged;
         sizeComponent.PropertyChanged += HandleDependencyChanged;
         orientationComponent.PropertyChanged += HandleDependencyChanged;
      }

      private OrientedBoundingBox GetBounds() {
         if (isBoundingBoxDirty) {
            var result = new OrientedBoundingBox(-Vector3.One / 2, Vector3.One / 2);
            result.Scale(sizeComponent.Size);
            var zNudge = Vector3.Zero;
            if (sizeComponent.PositioningMode == VerticalPositioningMode.PositionBottom) {
               zNudge.Z = sizeComponent.Size.Z / 2;
            }
            result.Translate(zNudge);
            result.Transform(Matrix.RotationQuaternion(orientationComponent.Orientation));
            result.Translate(positionComponent.Position);

            boundingBoxCache = result;
         }
         return boundingBoxCache;
      }

      private void HandleDependencyChanged(object sender, PropertyChangedEventArgs e) {
         isBoundingBoxDirty = true;
      }
   }

   public class ColorComponent : EntityComponent {
      private Vector4 color;

      public ColorComponent(Vector4 color) {
         this.color = color;
      }

      public Vector4 Color { get { return color; } set { color = value; OnPropertyChanged(); } }
   }

   public class PhysicsComponent : EntityComponent {
      private bool isPhysicsEnabled;

      public PhysicsComponent(bool isPhysicsEnabled) {
         this.isPhysicsEnabled = isPhysicsEnabled;
      }

      public bool IsPhysicsEnabled { get { return isPhysicsEnabled; } set { isPhysicsEnabled = value; OnPropertyChanged(); } }
   }

   public class RenderComponent : EntityComponent {
      private bool isVisible;

      public RenderComponent(bool isVisible) {
         this.isVisible = isVisible;
      }

      public bool IsVisible { get { return isVisible; } set { isVisible = value; OnPropertyChanged(); } }
   }

   public class MouseHandlerComponent : EntityComponent {
      public event EventHandler<SceneMouseEventInfo> Event;
      public event EventHandler<SceneMouseEventInfo> GlobalEvent;

      public void HandleMouseEvent(SceneMouseEventInfo e) {
         Event?.Invoke(this, e);
      }

      public void HandleGlobalMouseEvent(SceneMouseEventInfo e) {
         GlobalEvent?.Invoke(this, e);
      }
   }

   public class GameStepComponent : EntityComponent {
      public event EventHandler<GameStepEventArgs> Event;

      public void HandleGameStep(GameTime gameTime) {
         Event?.Invoke(this, new GameStepEventArgs { GameTime = gameTime });
      }
   }

   public class GameStepEventArgs {
      public GameTime GameTime { get; set; }
   }

   public class InteractableComponent : EntityComponent {
      private MouseHandlerComponent mouseHandlerComponent;

      public override void Initialize() {
         base.Initialize();
         mouseHandlerComponent = Entity.GetComponent<MouseHandlerComponent>();
         mouseHandlerComponent.Event += HandleMouseEvent;
      }

      private void HandleMouseEvent(object sender, SceneMouseEventInfo e) {
         Console.WriteLine("Clicked!");
      }
   }

   public class CharacterComponent : EntityComponent { }

   public class SpeedComponent : EntityComponent {
      private float speed;

      public SpeedComponent(float speed) {
         this.speed = speed;
      }

      public float Speed { get { return speed; } set { speed = value; OnPropertyChanged(); } }
   }

   public class PathingComponent : EntityComponent {
      private NavigationPath path;
      private int progress;

      public NavigationPath Path { get { return path; } set { path = value; OnPropertyChanged(); } }
      public int Progress { get { return progress; } set { progress = value; OnPropertyChanged(); } }

      public void BeginPathing(NavigationPath path) {
         this.path = path;
         this.progress = 0;
      }
   }

   public class GridletPathingTargetComponent : EntityComponent {
      private readonly NavigationGridlet gridlet;
      private readonly Pathfinder pathfinder;
      private MouseHandlerComponent mouseHandlerComponent;

      public GridletPathingTargetComponent(NavigationGridlet gridlet, Pathfinder pathfinder) {
         this.gridlet = gridlet;
         this.pathfinder = pathfinder;
      }

      public override void Initialize() {
         base.Initialize();
         mouseHandlerComponent = Entity.GetComponent<MouseHandlerComponent>();
         mouseHandlerComponent.Event += HandleMouseEvent;
      }

      private void HandleMouseEvent(object sender, SceneMouseEventInfo e) {
         if (e.Button == MouseButtons.Right) {
            var characterEntity = EntitySystem.EnumerateComponents<CharacterComponent>().Select(x => x.Entity).First();
            var positionComponent = characterEntity.GetComponent<PositionComponent>();
            var pathingComponent = characterEntity.GetComponent<PathingComponent>();
            var gridletIntersection = e.IntersectionPoint;
            var path = pathfinder.FindPath(positionComponent.Position, gridletIntersection);
            pathingComponent.BeginPathing(path);
            Console.WriteLine(path.Points.Join(", "));
         }
      }
   }

   public class EntitySystem {
      private readonly IConcurrentSet<Entity> entities = new ConcurrentSet<Entity>();
      private readonly IConcurrentSet<Behavior> behaviors = new ConcurrentSet<Behavior>();

      public void AddEntity(Entity entity) {
         entity.__SetEntitySystem(this);
         entities.Add(entity);
      }

      public void RemoveElement(Entity entity) => entities.Remove(entity);

      public IEnumerable<Entity> EnumerateEntities() => entities;
      public IEnumerable<TComponent> EnumerateComponents<TComponent>() where TComponent : EntityComponent => entities.Select(x => x.GetComponentOrNull<TComponent>()).Where(x => x != null);

      public void AddBehavior(Behavior behavior) => behaviors.Add(behavior);
      public void RemoveBehavior(Behavior behavior) => behaviors.Remove(behavior);

      public IEnumerable<Behavior> EnumerateBehaviors() => behaviors;
   }

   public abstract class Behavior {
      public abstract void Step(EntitySystem system, GameTime gameTime);
   }

   public class PhysicsBehavior : Behavior {
      private readonly NavigationGrid grid;

      public PhysicsBehavior(NavigationGrid grid) {
         this.grid = grid;
      }

      public override void Step(EntitySystem system, GameTime gameTime) {
         var physicsComponents = system.EnumerateComponents<PhysicsComponent>().Where(x => x.IsPhysicsEnabled).ToArray();
         foreach (var physicsComponent in physicsComponents) {
            var entity = physicsComponent.Entity;
            var positionComponent = entity.GetComponent<PositionComponent>();
            var sizeComponent = entity.GetComponent<SizeComponent>();
            var boundsComponent = entity.GetComponent<BoundsComponent>();
            var pathingComponent = entity.GetComponentOrNull<PathingComponent>();
            if (pathingComponent?.Path == null) {
               var bounds = boundsComponent.Bounds;
               var boundsBottom = bounds.GetCorners().Min(x => x.Z);
               var boundsCenter = bounds.Center;
               var cell = grid.GetCell(boundsCenter);
               var cellTop = cell.OrientedBoundingBox.GetCorners().Max(x => x.Z);
               var oldPosition = positionComponent.Position;
               var newPositionZ = oldPosition.Z + (cellTop - boundsBottom);
               positionComponent.Position = new Vector3(oldPosition.X, oldPosition.Y, newPositionZ);
            }
         }
      }
   }

   public class PathingBehavior : Behavior {
      public override void Step(EntitySystem system, GameTime gameTime) {
         var pathingComponents = system.EnumerateComponents<PathingComponent>().ToArray();
         foreach (var pathingComponent in pathingComponents) {
            var entity = pathingComponent.Entity;
            var positionComponent = entity.GetComponent<PositionComponent>();
            var speedComponent = entity.GetComponent<SpeedComponent>();

            var speed = speedComponent.Speed;
            var position = positionComponent.Position;
            var path = pathingComponent.Path;
            var progress = pathingComponent.Progress;

            if (path != null) {
               var movementUnitsRemaining = speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
               while (movementUnitsRemaining > 0 && progress < path.Points.Length) {
                  var nextPoint = path.Points[progress];
                  var distanceToNextPoint = Vector3.Distance(position, nextPoint);
                  if (movementUnitsRemaining >= distanceToNextPoint) {
                     movementUnitsRemaining -= distanceToNextPoint;
                     position = nextPoint;
                     progress++;
                  } else {
                     var dir = nextPoint - position;
                     dir.Normalize();
                     position += dir * movementUnitsRemaining;
                     movementUnitsRemaining = 0;
                  }
               }
               if (progress == path.Points.Length) {
                  pathingComponent.Progress = -1;
                  pathingComponent.Path = null;
               } else {
                  pathingComponent.Progress = progress;
               }
               positionComponent.Position = position;
            }
         }
      }
   }
   
   public enum VerticalPositioningMode {
      PositionCenter,
      PositionBottom
   }
}
