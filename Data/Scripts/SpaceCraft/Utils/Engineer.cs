using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRage.Game.Components;
using Sandbox.ModAPI;
//using Sandbox.ModAPI.Ingame;
using Sandbox.Game.AI;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Components;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Entities.Character.Components;
using SpaceEngineers.Game.EntityComponents.GameLogic.Discovery;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using SpaceCraft.Utils;

namespace SpaceCraft.Utils {

	public enum Weapons {
		None,
		Welder,
		Grinder,
		Drill,
		Gun
	}

	public class Engineer : Controllable {

		protected bool Flying = true;
    protected bool Drill = true;
    protected bool Welder = true;
    protected bool Grider = true;

    public IMyCharacter Character;

		public MatrixD WorldMatrix
		{
			get {
				return Character == null ? MatrixD.Zero : Character.WorldMatrix;
			}
			set {
				if( Character != null ) Character.WorldMatrix = value;
			}
		}

		public float Integrity
		{
			get {
				return Character == null ? 0.0f : Character.Integrity;
			}
		}

		public Engineer( string data = "" ) {
			/*if( data == string.Empty ) return;
			try {
				var npcData = MyAPIGateway.Utilities.SerializeFromBinary<Engineer>(Convert.FromBase64String(data));
			}*/
		}



		public override void Init() {
			if( Character == null ) return;
			//ControlledEntity = Character as IMyControllableEntity;

			TakeControl(Character);

			Character.DoDamage(0.0f, MyStringHash.Get(string.Empty), true); // Hack to property init Physics

			if( !Character.EnabledDamping ) {
				Character.SwitchDamping();
			}
			if( !Character.EnabledThrusts ) {
				Character.SwitchThrusts();
			}
		}

		public void CheckOrder() {
			if( CurrentOrder == null ) return;

			if( CurrentOrder.Target != null || CurrentOrder.Destination != null ) {
				Vector3D destination = CurrentOrder.Target == null ? CurrentOrder.Destination : CurrentOrder.Target.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
				ControlledEntity.MoveAndRotate( Vector3.Normalize(destination), Vector2.Zero, 0.0f );
			}


		}

		public override void StartOrder() {
			if( CurrentOrder == null ) return;

			MyAPIGateway.Utilities.ShowNotification("Starting Order " + CurrentOrder.ToString() );

			Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;
			e.EndShoot( MyShootActionEnum.PrimaryAction );

			switch( CurrentOrder.Type ) {
				case Orders.Move:
					SwitchToWeapon();
					break;
				case Orders.Drill:
					SwitchToWeapon( Weapons.Drill );
					e.BeginShoot( MyShootActionEnum.PrimaryAction );
					break;
				case Orders.Grind:
					SwitchToWeapon( Weapons.Grinder );
					e.BeginShoot( MyShootActionEnum.PrimaryAction );
					break;
				case Orders.Weld:
					SwitchToWeapon( Weapons.Welder );
					e.BeginShoot( MyShootActionEnum.PrimaryAction );
					break;
			}
		}

		public void SwitchToWeapon( Weapons weapon = Weapons.None ) {
			if( Character != null ) {
				Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;
				switch( weapon ) {
					case Weapons.Welder:
						e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/WelderItem" ) );
						break;
					case Weapons.Drill:
						e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/HandDrillItem" ) );
						break;
					case Weapons.Grinder:
						e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AngleGrinderItem" ) );
						break;
					case Weapons.Gun:
						e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem" ) );
						break;
					default:
						e.SwitchToWeapon( MyDefinitionId.Parse( string.Empty ) );
						break;
				}
			}
		}

		public void DoDetect() {
			MyCharacterDetectorComponent detector = Character.Components.Get<MyCharacterDetectorComponent>();
			if( detector == null ) return;
			//detector.DoDetection();
			//MyAPIGateway.Physics.CalculateNaturalGravityAt()
			//MyAPIGateway.Physics.CalculateArtificialGravityAt()
		}

		public override void UpdateBeforeSimulation() {
			//Character.Physics.SetSpeeds( new Vector3(0,1,0), Vector3.Zero );
			if( Character == null || Character.Integrity == 0 ) {
				MatrixD matrix = Character.WorldMatrix;
				Character = Spawn(Owner.GetSpawnLocation());
				Init();
			}


			//MyAPIGateway.Utilities.ShowNotification("Engineer Position " + Character.WorldMatrix.ToString() );
			CheckOrder();


		}

    public override void UpdateBeforeSimulation100() {



      MyCharacterJetpackComponent jetpack = Character.Components.Get<MyCharacterJetpackComponent>();

      /*if( destination == null ) {
        destination = MyAPIGateway.Session.Player.GetPosition();
      }

      //ControllerInfo.Controller.Player.IsRealPlayer
      Vector3 d = Vector3.Normalize(destination);

      if( jetpack != null ) {
        jetpack.TurnOnJetpack(true);
        jetpack.UpdateFall();
        jetpack.UpdatePhysicalMovement();
        jetpack.EnableDampeners(true);
        jetpack.ClearMovement();

        jetpack.MoveAndRotate(ref d, ref Vector2.Zero, 0.0f, true);
      }*/

      //Character.MoveAndRotate(d, Vector2.Zero, 0.0f);

      /*Character.Physics.SetSpeeds( new Vector3(0,1,0), Vector3.Zero );

      MyAPIGateway.Utilities.ShowNotification("Static: " + Character.Physics.IsStatic );
      MyAPIGateway.Utilities.ShowNotification("Kinematic: " + Character.Physics.IsKinematic );*/
      MyAPIGateway.Utilities.ShowNotification("Moving: " + Character.Physics.IsMoving );


    }

		public void DrillFor( string SubtypeName = "Stone", int amount = 1 ) {
			MyInventoryBase inv = Character.Components.Get<MyInventoryBase>();

			if( inv != null ) {
				inv.AddItems((VRage.MyFixedPoint)amount, new MyObjectBuilder_Ore(){
					SubtypeName = SubtypeName
				} );
			}
		}




		public static IMyCharacter Spawn( MatrixD matrix ) {
			if( matrix == null ) return null;

			MyObjectBuilder_Character character = new MyObjectBuilder_Character(){
        CharacterModel = "Astronaut",
        AIMode = true,
        //BotDefId = SerializableDefinitionId(MyObjectBuilderType.Parse("MyObjectBuilder_Character"), "MyObjectBuilder_BarbarianBot"),
        JetpackEnabled = true,
        PersistentFlags = MyPersistentEntityFlags2.InScene,
        Name = "NPC",
        DisplayName = "NPC",
        Inventory = new MyObjectBuilder_Inventory(){
					Items = new List<MyObjectBuilder_InventoryItem>(){
						new MyObjectBuilder_InventoryItem() {
							Amount = (VRage.MyFixedPoint)1,
							PhysicalContent = new MyObjectBuilder_PhysicalGunObject(){
								SubtypeName = "WelderItem"
							}
						},
						new MyObjectBuilder_InventoryItem() {
							Amount = (VRage.MyFixedPoint)1,
							PhysicalContent = new MyObjectBuilder_PhysicalGunObject(){
								SubtypeName = "AngleGrinderItem"
							}
						},
						new MyObjectBuilder_InventoryItem() {
							Amount = (VRage.MyFixedPoint)1,
							PhysicalContent = new MyObjectBuilder_PhysicalGunObject(){
								SubtypeName = "HandDrillItem"
							}
						}
					}
				},
        HandWeapon = new MyObjectBuilder_HandDrill(),
        //Battery = new MyObjectBuilder_Battery(),
        PositionAndOrientation = new MyPositionAndOrientation(matrix),
        Health = 1.0f,
        MovementState = MyCharacterMovementEnum.Standing,//Walking,
        SubtypeName = "Default_Astronaut"
      };

      MyEntity ent = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(character);

			if( ent != null ) {
        ent.Flags &= ~EntityFlags.Save;
        //ent.Flags &= ~EntityFlags.NeedsUpdate;
        ent.Render.Visible = true;
        ent.NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        MyAPIGateway.Entities.AddEntity(ent);

        return ent as IMyCharacter;

      }

			return null;
		}


	}

}
