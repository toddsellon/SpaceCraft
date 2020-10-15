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
//using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace SpaceCraft.Utils {

	public enum Weapons {
		None,
		Welder,
		Grinder,
		Drill,
		Gun
	}

	//[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class Engineer : Controllable {

		protected bool Flying = true;
		public Vector3 Color;

    public IMyCharacter Character;

		public MatrixD WorldMatrix {
			get {
				return Character == null ? MatrixD.Zero : Character.WorldMatrix;
			}
			set {
				if( Character != null ) Character.WorldMatrix = value;
			}
		}

		public float Integrity {
			get {
				return Character == null ? 0.0f : Character.Integrity;
			}
		}

		// Main loop
		public override void UpdateBeforeSimulation() {
			if( Character == null || Character.Integrity == 0 ) {
				MatrixD matrix = Character.WorldMatrix;
				Character = Spawn(Owner.GetSpawnLocation(), Owner );
				Initialize();
			}

			if( CurrentOrder == null || CurrentOrder.Step == Steps.Completed ) Next();
			if( CurrentOrder == null ) return;

			switch( CurrentOrder.Type ) {
				case Orders.Attack:
					Attack();
					break;
				case Orders.Move:
					Move();
					break;
				case Orders.Drill:
					Drill();
					break;
			}


		}

		public void Attack() {
			if( CurrentOrder.Target == null || CurrentOrder.Target.MarkedForClose || CurrentOrder.Target.Closed ) {
				CurrentOrder = null;
				return;
			}

			//Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;
			if( CurrentOrder.Step == Steps.Pending ) {
				SwitchToWeapon( Weapons.Grinder );
				SwitchToWeapon( Weapons.Gun );
				BeginShoot( MyShootActionEnum.PrimaryAction );
				CurrentOrder.Progress();
			}
		}


		// https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/Weapons/Guns/MyHandDrill.cs
		// MyHandDrillDefinition
		public void Drill() {
			if( CurrentOrder.Step == Steps.Pending ) {
				SwitchToWeapon( Weapons.Drill );
				BeginShoot( MyShootActionEnum.PrimaryAction );

				CurrentOrder.Progress();
			} else {
				// Add resources to inventory
				IMyInventory inv = GetInventory()[0];
				if( inv.CurrentVolume < inv.MaxVolume ) {
					inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_Ore(){
		        SubtypeName = CurrentOrder.SubtypeName ?? "Stone"
		      } );
				} else {
					CurrentOrder = null;
				}
			}
		}



		public Engineer( IMyCharacter character ) {
			Character = character;
			//ControlledEntity = Character as Sandbox.Game.Entities.IMyControllableEntity;
			ControlledEntity = Character as IMyControllableEntity;

			//MyAPIGateway.Session.RegisterComponent(this, MyUpdateOrder.BeforeSimulation, 0);
			/*if( data == string.Empty ) return;
			try {
				var npcData = MyAPIGateway.Utilities.SerializeFromBinary<Engineer>(Convert.FromBase64String(data));
			}*/
			Initialize();
		}



		public override void Init( MyObjectBuilder_SessionComponent session ) {

			base.Init(session);

			//ControlledEntity = Character as IMyControllableEntity;
			Initialize();

		}

		public override List<IMyInventory> GetInventory( List<IMySlimBlock> blocks = null ) {
      return new List<IMyInventory>(){
				Character.GetInventory()
			};
    }

		public void Initialize() {
			if( Character == null ) return;

			TakeControl(Character);

			Character.DoDamage(0.0f, MyStringHash.Get(string.Empty), true); // Hack to property init Physics

			if( !Character.EnabledDamping ) {
				Character.SwitchDamping();
			}
			if( Character.EnabledThrusts ) {
				Character.SwitchThrusts();
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



    //public override void UpdateBeforeSimulation100() {
		public void UpdateBeforeSimulation100() {

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
      //MyAPIGateway.Utilities.ShowNotification("Moving: " + Character.Physics.IsMoving );


    }

		public void DrillFor( string SubtypeName = "Stone", int amount = 1 ) {
			MyInventoryBase inv = Character.Components.Get<MyInventoryBase>();

			if( inv != null ) {
				inv.AddItems((VRage.MyFixedPoint)amount, new MyObjectBuilder_Ore(){
					SubtypeName = SubtypeName
				} );
			}
		}




		public static IMyCharacter Spawn( MatrixD matrix, Faction owner ) {
			if( matrix == null ) return null;

			MyObjectBuilder_Character character = new MyObjectBuilder_Character(){
        CharacterModel = "Astronaut",
        AIMode = true,
        //BotDefId = SerializableDefinitionId(MyObjectBuilderType.Parse("MyObjectBuilder_Character"), "MyObjectBuilder_BarbarianBot"),
        JetpackEnabled = true,
        PersistentFlags = MyPersistentEntityFlags2.InScene,
        Name = "NPC",
        DisplayName = "NPC",
				ColorMaskHSV = owner.Color,
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
