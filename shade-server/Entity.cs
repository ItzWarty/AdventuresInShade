using ItzWarty;
using ItzWarty.Collections;
using Shade.Annotations;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using SharpDX.Toolkit.Graphics;
using ICL = ItzWarty.Collections;

namespace Shade {
   public class EntityFactory {
      public Entity CreateUnitCubeEntity() {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(Vector3.UnitY));
         AddAndInitialize(entity, new SizeComponent(Vector3.One, VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(new Vector4(0, 1, 0, 1)));
         AddAndInitialize(entity, new PhysicsComponent());
         AddAndInitialize(entity, new RenderComponent(true));
         return entity;
      }

      public Entity CreateAndAssociateGridletEntity(NavigationGrid navigationGrid, NavigationGridlet gridlet, Pathfinder pathfinder, CommandFactory commandFactory) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(new Vector3(gridlet.X, gridlet.Y, gridlet.Z)));
         AddAndInitialize(entity, new SizeComponent(new Vector3(gridlet.XLength, gridlet.YLength, 1), VerticalPositioningMode.PositionCenter));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.RotationMatrix(gridlet.Orientation)));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(Vector4.One));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new GridletPathingTargetComponent(gridlet, pathfinder, commandFactory));
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new GridletComponent(navigationGrid, gridlet));
         gridlet.Entity = entity;
         return entity;
      }

      public Entity CreateCharacterEntity(Pathfinder pathfinder) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(new Vector3(0, 0, 0)));
         AddAndInitialize(entity, new SizeComponent(new Vector3(2, 2, 2.8f), VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(new Vector4(1, 0, 0, 1)));
         AddAndInitialize(entity, new PhysicsComponent());
         AddAndInitialize(entity, new CharacterComponent());
         AddAndInitialize(entity, new SpeedComponent(10 * 20));
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new MobaCameraTargetComponent(true));
         AddAndInitialize(entity, new CommandQueueComponent());
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

      public Entity CreateDungeonKeyEntity(Vector3 position, Vector4 color, CommandFactory commandFactory, DungeonKeyInventory dungeonKeyInventory) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(position));
         AddAndInitialize(entity, new SizeComponent(new Vector3(0.8f, 1.28f, 0.5f) * 30, VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.RotationAxis(Vector3.UnitZ, StaticRandom.NextFloat(1))));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(color));
         AddAndInitialize(entity, new PhysicsComponent());
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new InteractableComponent(commandFactory));
         AddAndInitialize(entity, new DungeonKeyComponent(dungeonKeyInventory));
         return entity;
      }

      public Entity CreateDungeonLockEntity(Vector3 position, Vector4 color, CommandFactory commandFactory, DungeonKeyInventory dungeonKeyInventory, NavigationGrid navigationGrid) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(position));
         AddAndInitialize(entity, new SizeComponent(new Vector3(6), VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(color));
         AddAndInitialize(entity, new PhysicsComponent());
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new InteractableComponent(commandFactory));
         AddAndInitialize(entity, new CommandQueueComponent());
         AddAndInitialize(entity, new DungeonLockComponent(this, commandFactory, dungeonKeyInventory, navigationGrid));
         return entity;
      }

      public Entity CreateDungeonDoorEntity(Entity lockEntity, Vector3 position, Vector4 color, CommandFactory commandFactory, DungeonKeyInventory dungeonKeyInventory) {
         var entity = new Entity();
         AddAndInitialize(entity, new PositionComponent(position));
         AddAndInitialize(entity, new SizeComponent(new Vector3(6), VerticalPositioningMode.PositionBottom));
         AddAndInitialize(entity, new OrientationComponent(Quaternion.Identity));
         AddAndInitialize(entity, new BoundsComponent());
         AddAndInitialize(entity, new ColorComponent(color));
//         AddAndInitialize(entity, new PhysicsComponent());
         AddAndInitialize(entity, new RenderComponent(true));
         AddAndInitialize(entity, new MouseHandlerComponent());
         AddAndInitialize(entity, new InteractableComponent(commandFactory));
         AddAndInitialize(entity, new DungeonDoorComponent(lockEntity, dungeonKeyInventory));
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
            var projection = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(60.0f), (float)graphicsConfiguration.Width / graphicsConfiguration.Height, 0.5f, 20000.0f);

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
      private int disableCounter = 0;

      public bool IsPhysicsEnabled => disableCounter == 0;

      public void Disable() {
         disableCounter++;
      }

      public void Enable() {
         disableCounter--;
         Console.WriteLine("DISABLE COUNTER AT " + disableCounter);
      }
   }

   public class RenderEventArgs {
      public Renderer Renderer { get; set; }
      public GraphicsDevice GraphicsDevice { get; set; }
      public BasicEffect BasicEffect { get; set; }
   }

   public class RenderComponent : EntityComponent {
      private bool isVisible;
      private bool isCustomRendered = false;
      public event EventHandler<RenderEventArgs> Render;

      public RenderComponent(bool isVisible) {
         this.isVisible = isVisible;
      }

      public void HandleOnRender(RenderEventArgs e) => Render?.Invoke(this, e);
      public bool IsVisible { get { return isVisible; } set { isVisible = value; OnPropertyChanged(); } }
      public bool IsCustomRendered { get { return isCustomRendered; } set { isCustomRendered = value; OnPropertyChanged(); } }
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

   public class EntityInteractionEventArgs : EventArgs {
      private readonly Entity interactor;
      private readonly Entity target;

      public EntityInteractionEventArgs(Entity interactor, Entity target) {
         this.interactor = interactor;
         this.target = target;
      }

      public Entity Interactor => interactor;
      public Entity Target => target;
   }

   public class InteractableComponent : EntityComponent {
      private readonly CommandFactory commandFactory;
      private MouseHandlerComponent mouseHandlerComponent;
      public event EventHandler<EntityInteractionEventArgs> Interaction;

      public InteractableComponent(CommandFactory commandFactory) {
         this.commandFactory = commandFactory;
      }

      public override void Initialize() {
         base.Initialize();
         mouseHandlerComponent = Entity.GetComponent<MouseHandlerComponent>();
         mouseHandlerComponent.Event += HandleMouseEvent;
      }

      private void HandleMouseEvent(object sender, SceneMouseEventInfo e) {
         if (e.Button.HasFlag(MouseButtons.Left)) {
            Console.WriteLine("!!!! PATHING ");
            var character = EntitySystem.EnumerateComponents<CharacterComponent>().First().Entity;
            var commandQueueComponent = character.GetComponent<CommandQueueComponent>();
            commandQueueComponent.AddCommand(commandFactory.PathingCommand(character, Entity));
            commandQueueComponent.AddCommand(commandFactory.ActionCommand(OnInteractionHandler(character)), false);
         }
      }

      private Action OnInteractionHandler(Entity interactor) {
         return () => {
            Interaction?.Invoke(this, new EntityInteractionEventArgs(interactor, Entity));
         };
      }
   }

   public class CharacterComponent : EntityComponent { }

   public class DungeonKeyComponent : EntityComponent {
      private readonly DungeonKeyInventory dungeonKeyInventory;

      public DungeonKeyComponent(DungeonKeyInventory dungeonKeyInventory) {
         this.dungeonKeyInventory = dungeonKeyInventory;
      }

      public override void Initialize() {
         base.Initialize();
         var interactableComponent = Entity.GetComponent<InteractableComponent>();
         interactableComponent.Interaction += HandleInteraction;
      }

      private void HandleInteraction(object sender, EntityInteractionEventArgs e) {
         var colorComponent = Entity.GetComponent<ColorComponent>();
         dungeonKeyInventory.Keys.Add(colorComponent.Color);
         EntitySystem.RemoveEntity(Entity);
      }
   }

   public class DungeonLockComponent : EntityComponent {
      private readonly List<Entity> childEntities = new List<Entity>();
      private readonly EntityFactory entityFactory;
      private readonly CommandFactory commandFactory;
      private readonly DungeonKeyInventory dungeonKeyInventory;
      private readonly NavigationGrid navigationGrid;

      public DungeonLockComponent(EntityFactory entityFactory, CommandFactory commandFactory, DungeonKeyInventory dungeonKeyInventory, NavigationGrid navigationGrid) {
         this.entityFactory = entityFactory;
         this.commandFactory = commandFactory;
         this.dungeonKeyInventory = dungeonKeyInventory;
         this.navigationGrid = navigationGrid;
      }

      public override void Initialize() {
         base.Initialize();
         var colorComponent = Entity.GetComponent<ColorComponent>();
         var positionComponent = Entity.GetComponent<PositionComponent>();
         var commandComponent = Entity.GetComponent<CommandQueueComponent>();
         commandComponent.AddCommand(commandFactory.ActionCommand(() => {
            var r = 40;
            childEntities.Add(entityFactory.CreateDungeonDoorEntity(Entity, positionComponent.Position + new Vector3(r, 0, 0), colorComponent.Color, commandFactory, dungeonKeyInventory));
            childEntities.Add(entityFactory.CreateDungeonDoorEntity(Entity, positionComponent.Position + new Vector3(-r, 0, 0), colorComponent.Color, commandFactory, dungeonKeyInventory));
            childEntities.Add(entityFactory.CreateDungeonDoorEntity(Entity, positionComponent.Position + new Vector3(0, r, 0), colorComponent.Color, commandFactory, dungeonKeyInventory));
            childEntities.Add(entityFactory.CreateDungeonDoorEntity(Entity, positionComponent.Position + new Vector3(0, -r, 0), colorComponent.Color, commandFactory, dungeonKeyInventory));
            childEntities.ForEach(EntitySystem.AddEntity);
         }));

         var position = positionComponent.Position;
         var gridlet = navigationGrid.GetGridlets(position.X, position.Y).First();
         var gridletEntity = gridlet.Entity;
         gridletEntity.GetComponent<GridletComponent>().IsPathingEnabled = false;
      }

      public void HandleInteraction(object sender, EntityInteractionEventArgs e) {
         var colorComponent = Entity.GetComponent<ColorComponent>();
         if (dungeonKeyInventory.Keys.Remove(colorComponent.Color)) {
            childEntities.ForEach(EntitySystem.RemoveEntity);
            EntitySystem.RemoveEntity(Entity);

            var positionComponent = Entity.GetComponent<PositionComponent>();
            var position = positionComponent.Position;
            var gridlet = navigationGrid.GetGridlets(position.X, position.Y).First();
            var gridletEntity = gridlet.Entity;
            gridletEntity.GetComponent<GridletComponent>().IsPathingEnabled = true;
         }
      }
   }

   public class DungeonDoorComponent : EntityComponent {
      private readonly Entity lockEntity;
      private readonly DungeonKeyInventory dungeonKeyInventory;

      public DungeonDoorComponent(Entity lockEntity, DungeonKeyInventory dungeonKeyInventory) {
         this.lockEntity = lockEntity;
         this.dungeonKeyInventory = dungeonKeyInventory;
      }

      public override void Initialize() {
         base.Initialize();
         var interactableComponent = Entity.GetComponent<InteractableComponent>();
         interactableComponent.Interaction += HandleInteraction;
      }

      private void HandleInteraction(object sender, EntityInteractionEventArgs e) {
         var lockComponent = lockEntity.GetComponent<DungeonLockComponent>();
         lockComponent.HandleInteraction(sender, e);
      }
   }

   public class SpeedComponent : EntityComponent {
      private float speed;

      public SpeedComponent(float speed) {
         this.speed = speed;
      }

      public float Speed { get { return speed; } set { speed = value; OnPropertyChanged(); } }
   }

   public class CommandFactory {
      private readonly Pathfinder pathfinder;

      public CommandFactory(Pathfinder pathfinder) {
         this.pathfinder = pathfinder;
      }

      public Command PathingCommand(Entity entity, Vector3 destination) {
         var positionComponent = entity.GetComponent<PositionComponent>();
         var path = pathfinder.FindPath(positionComponent.Position, destination);
         return new PathingCommand(entity, path);
      }

      public Command PathingCommand(Entity entity, Entity otherEntity) {
         var thisPosition = entity.GetComponent<PositionComponent>().Position;
         var thisBounds = entity.GetComponent<BoundsComponent>().Bounds;
         var thisCylinderRadius = Math.Sqrt(thisBounds.Extents.X * thisBounds.Extents.X + thisBounds.Extents.Y * thisBounds.Extents.Y);
         var otherPosition = otherEntity.GetComponent<PositionComponent>().Position;
         var otherBounds = otherEntity.GetComponent<BoundsComponent>().Bounds;
         var otherCylinderRadius = Math.Sqrt(otherBounds.Extents.X * otherBounds.Extents.X + otherBounds.Extents.Y * otherBounds.Extents.Y);
         var navigationPath = pathfinder.FindPath(thisPosition, otherPosition);
         var distanceToEat = otherCylinderRadius + thisCylinderRadius;
         if (navigationPath.Length < distanceToEat) {
            Console.WriteLine("PATH TOO SMALL");
            return new PathingCommand(entity, null);
         } else {
            Console.WriteLine("PATH NOT TO SMALL");
            var points = navigationPath.Points;
            var pointsToKeep = points.Length;
            Console.WriteLine(points.Join(","));
            while (distanceToEat > 0) {
               Console.WriteLine("DISTANCE TO EAT " + distanceToEat);
               var a = points[pointsToKeep - 2];
               var b = points[pointsToKeep - 1];
               var abDistance = Vector3.Distance(a, b);
               Console.WriteLine("AB DIST " + abDistance);
               if (abDistance < distanceToEat) {
                  distanceToEat -= abDistance;
                  pointsToKeep--;
               } else {
                  var baVect = b - a;
                  baVect.Normalize();
                  b = a + baVect * (float)(abDistance - distanceToEat);
                  points[pointsToKeep - 1] = b;
                  distanceToEat = -1;
               }
            }
            points = points.SubArray(0, pointsToKeep);
            return new PathingCommand(entity, new NavigationPath(points));
         }
      }

      public Command ActionCommand(Action action) {
         return new ActionCommand(action);
      }
   }

   public abstract class Command {
      public abstract void HandleEnter(Entity entity);
      public abstract void HandleTick(Entity entity, GameTime gameTime);
      public abstract void HandleLeave(Entity entity);
      public abstract bool IsCompleted { get; }
   }

   public class PathingCommand : Command {
      private readonly Entity entity;
      private readonly PositionComponent positionComponent;
      private readonly SpeedComponent speedComponent;
      private readonly PhysicsComponent physicsComponent;
      private NavigationPath path;
      private int progress;

      public PathingCommand(Entity entity, NavigationPath path) {
         this.entity = entity;
         this.path = path;
         this.progress = 0;
         this.positionComponent = entity.GetComponent<PositionComponent>();
         this.speedComponent = entity.GetComponent<SpeedComponent>();
         this.physicsComponent = entity.GetComponent<PhysicsComponent>();
      }

      public NavigationPath Path => path;
      public override bool IsCompleted => path == null;

      public override void HandleEnter(Entity entity) {
         Console.WriteLine("PATHING ENTER");
         physicsComponent.Disable();
      }

      public override void HandleTick(Entity entity, GameTime gameTime) {
         Console.WriteLine("PATHING TICK");
         var speed = speedComponent.Speed;
         var position = positionComponent.Position;

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
               this.progress = -1;
               this.path = null;
            }
            positionComponent.Position = position;
         }
      }

      public override void HandleLeave(Entity entity) {
         Console.WriteLine("PATHING LEAVE");
         physicsComponent.Enable();
      }
   }

   public class ActionCommand : Command {
      private readonly Action action; 
      private bool hasRun = false;

      public ActionCommand(Action action) {
         this.action = action;
      }

      public override bool IsCompleted => hasRun;

      public override void HandleEnter(Entity entity) {
         action();
         hasRun = false;
      }

      public override void HandleTick(Entity entity, GameTime gameTime) { }

      public override void HandleLeave(Entity entity) { }
   }

   public class CommandQueueComponent : EntityComponent {
      private readonly ConcurrentQueue<Command> commandQueue = new ConcurrentQueue<Command>();
      private Command currentCommand;

      public void AddCommand(Command command, bool force = true) {
         if (force) {
            CurrentCommand?.HandleLeave(Entity);
            CurrentCommand = null;
            Command throwaway;
            while (commandQueue.TryDequeue(out throwaway)) ;
         }
         commandQueue.Enqueue(command);
      }

      public IConcurrentQueue<Command> CommandQueue => commandQueue;
      public Command CurrentCommand { get { return currentCommand; } set { currentCommand = value; OnPropertyChanged(); } }
   }

   public class GridletPathingTargetComponent : EntityComponent {
      private readonly NavigationGridlet gridlet;
      private readonly Pathfinder pathfinder;
      private readonly CommandFactory commandFactory;
      private MouseHandlerComponent mouseHandlerComponent;

      public GridletPathingTargetComponent(NavigationGridlet gridlet, Pathfinder pathfinder, CommandFactory commandFactory) {
         this.gridlet = gridlet;
         this.pathfinder = pathfinder;
         this.commandFactory = commandFactory;
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
            var commandQueueComponent = characterEntity.GetComponent<CommandQueueComponent>();
            commandQueueComponent.AddCommand(commandFactory.PathingCommand(characterEntity, e.IntersectionPoint));
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

      public void RemoveEntity(Entity entity) => entities.Remove(entity);

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

            var bounds = boundsComponent.Bounds;
            var boundsBottom = bounds.GetCorners().Min(x => x.Z);
            var boundsCenter = bounds.Center;
            var cell = grid.GetCell(boundsCenter);
            var cellTop = cell.OrientedBoundingBox.GetCorners().Max(x => x.Z);
            var oldPosition = positionComponent.Position;
            var newPositionZ = oldPosition.Z + (cellTop - boundsBottom);
//            Console.WriteLine(oldPosition.Z);
            positionComponent.Position = new Vector3(oldPosition.X, oldPosition.Y, newPositionZ);
         }
      }
   }

   public class CommandQueueBehavior : Behavior {
      public override void Step(EntitySystem system, GameTime gameTime) {
         var commandQueueComponents = system.EnumerateComponents<CommandQueueComponent>();
         foreach (var cqc in commandQueueComponents) {
            var entity = cqc.Entity;
            if (cqc.CurrentCommand?.IsCompleted ?? false) {
               cqc.CurrentCommand.HandleLeave(entity);
            }

            if (cqc.CommandQueue.Any() && (cqc.CurrentCommand?.IsCompleted ?? true)) {
               Command nextCommand;
               cqc.CommandQueue.TryDequeue(out nextCommand);
               nextCommand.HandleEnter(entity);
               nextCommand.HandleTick(entity, gameTime);
               cqc.CurrentCommand = nextCommand;
            } else {
               cqc.CurrentCommand?.HandleTick(entity, gameTime);
            }

            if (cqc.CurrentCommand?.IsCompleted ?? false) {
               cqc.CurrentCommand.HandleLeave(entity);
               cqc.CurrentCommand = null;
            }
         }
      }
   }

   public enum VerticalPositioningMode {
      PositionCenter,
      PositionBottom
   }
}
