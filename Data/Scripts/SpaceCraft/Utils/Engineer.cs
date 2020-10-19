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
using SpaceCraft;
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

		public Vector3 Color;
		protected MyCharacterJetpackComponent Jetpack = null;
		protected MyCharacterRagdollComponent Ragdoll = null;
		protected MyAnimationControllerComponent Animation = null;

    public IMyCharacter Character;
		//public MyGhostCharacter Ghost = null;

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

		protected float Speed = 0f;
		protected int Tick = 0;

		// Main loop
		public override void UpdateBeforeSimulation() {
			if( (Character == null || Character.Integrity == 0) ) {
				CurrentOrder = null;
				MatrixD matrix = Character.WorldMatrix;
				Character = Spawn(Owner.GetSpawnLocation(), Owner );
				Initialize();
			}

			Jetpack.FuelDefinition.EnergyDensity = 1.0f;

			if( CurrentOrder == null || CurrentOrder.Step == Steps.Completed ) Next();
			if( CurrentOrder == null ) return;

			switch( CurrentOrder.Type ) {
				case Orders.Attack:
					Attack();
					break;
				case Orders.Drill:
					Drill();
					break;
				case Orders.Move:
					Move();
					break;
				case Orders.Deposit:
					Deposit();
					break;
				case Orders.Withdraw:
					Withdraw();
					break;
			}
			Tick++;
			if( Tick == 99 ) {
				Tick = 0;
			}

		}

		public override bool Execute( Order order, bool force = false ) {

			if( base.Execute( order, force ) ) {
				switch( CurrentOrder.Type ) {
					case Orders.Drill:
						SwitchToWeapon( Weapons.Drill );
						break;
					case Orders.Attack:
						SwitchToWeapon( Weapons.Grinder );
						SwitchToWeapon( Weapons.Gun );
						break;
					default:
						SwitchToWeapon();
						break;
				}
				return true;
			}

			return false;
		}

		public override bool Move() {
			if( CurrentOrder == null ) return false;
			if( Character == null || Character.Integrity == 0 || (CurrentOrder.Destination == Vector3D.Zero && CurrentOrder.Target == null) ) {
				CurrentOrder.Complete();
				return false;
			}
			Vector2 rotation = Vector2.Zero;
			// Vector3 destination = Vector3.Normalize(MyAPIGateway.Session.Player.GetPosition() - Character.WorldMatrix.Translation);
			// double distance = Vector3D.Distance(MyAPIGateway.Session.Player.GetPosition(),Character.WorldMatrix.Translation);
			//Vector3 destination = Vector3.Normalize(CurrentOrder.Destination - Character.WorldMatrix.Translation);
			Vector3 destination = (CurrentOrder.Target == null ? CurrentOrder.Destination : CurrentOrder.Target.WorldMatrix.Translation);
			double distance = Vector3D.Distance(destination,Character.WorldMatrix.Translation);

			if( distance < CurrentOrder.Range ) {
				destination = Vector3.Zero;
				Character.Physics.ClearSpeed();
				return false;
			} else {
				if( !Character.EnabledThrusts ) {
					Character.SwitchThrusts();
				}
				destination.Normalize();
				Jetpack.MoveAndRotate( ref destination, ref rotation, 0.0f, false );
			}

			return true;
		}

		public void Attack() {
			if( CurrentOrder.Target == null || CurrentOrder.Target.MarkedForClose || CurrentOrder.Target.Closed ) {
				CurrentOrder = null;
				return;
			}

			//Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;
			if( CurrentOrder.Step == Steps.Pending ) {
				BeginShoot( MyShootActionEnum.PrimaryAction );
				CurrentOrder.Progress();
			}
		}


		// https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/Weapons/Guns/MyHandDrill.cs
		// MyHandDrillDefinition
		public void Drill() {
			if( Character == null || Character.Integrity == 0 ) {
				CurrentOrder = null;
				return;
			}
			//if( CurrentOrder.Destination == null ) {
				//Vector3D position = Entity.WorldMatrix.Translation;
				//MyPlanet planet = SpaceCraftSession.GetClosestPlanet( position );
				//MyPlanet planet = Owner.Homeworld;
				//CurrentOrder.Destination = planet.GetClosestSurfacePointGlobal(position);
				//Vector3D up = Vector3D.Normalize(CurrentOrder.Destination - (planet as IMyEntity).LocalVolume.Center);
				//CurrentOrder.Destination = CurrentOrder.Destination + (up * 100);
				//Vector3D forward = Vector3D.Normalize(CurrentOrder.Destination - position);
				//Vector3D forward = Vector3D.Normalize(CurrentOrder.Destination - (planet as IMyEntity).LocalVolume.Center);
				//CurrentOrder.Destination = CurrentOrder.Destination - (forward * 100);
			//}

			if( CurrentOrder.Step == Steps.Pending ) {

				if( !Move() ) {
					if( Character == null ) {
						CurrentOrder = null;
						return;
					} else {
						Character.SwitchThrusts();
						//SwitchToWeapon( Weapons.Drill );
						//BeginShoot( MyShootActionEnum.PrimaryAction );
					}
					if( CurrentOrder != null )
						CurrentOrder.Progress();
				}
			} else {
				// Add resources to inventory
				IMyInventory inv = GetInventory()[0];
				if( inv.IsFull ) {
					//MyAPIGateway.Utilities.ShowMessage( "Drill", "Inventory Full: " + ToString() );
					Execute( new Order{
						Type = Orders.Deposit,
						Range = 50f
					}, true );
					return;
				} else {
					inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_Ore(){
		        //SubtypeName = CurrentOrder.SubtypeName ?? "Stone"
						SubtypeName = "Stone"
		      } );
				}

			}
		}



		public Engineer( IMyCharacter character ) {
			Character = character;
			Entity = Character;
			Flying = true;
	    Spacecraft = true;
	    Drills = true;
	    Welders = true;
	    Griders = true;
			Cargo = true;
			//MyAPIGateway.Session.RegisterComponent(this, MyUpdateOrder.BeforeSimulation, 0);
			/*if( data == string.Empty ) return;
			try {
				var npcData = MyAPIGateway.Utilities.SerializeFromBinary<Engineer>(Convert.FromBase64String(data));
			}*/
			Initialize();
		}



		// public override void Init( MyObjectBuilder_SessionComponent session ) {
		//
		// 	base.Init(session);
		// 	Initialize();
		//
		// }

		public override List<IMyInventory> GetInventory( List<IMySlimBlock> blocks = null ) {
      return new List<IMyInventory>(){
				Character.GetInventory()
			};
    }

		public void Initialize() {
			if( Character == null ) return;

			Jetpack = Character.Components.Get<MyCharacterJetpackComponent>();
			Ragdoll = Character.Components.Get<MyCharacterRagdollComponent>();
			Animation = Character.Components.Get<MyAnimationControllerComponent>();

			//MyAPIGateway.Players.SetControlledEntity(MyAPIGateway.Session.Player.SteamUserId, Character as IMyEntity);

			Character.DoDamage(0.0f, MyStringHash.Get(string.Empty), true); // Hack to property init Physics

			if( !Character.EnabledDamping ) {
				Character.SwitchDamping();
			}
			if( !Character.EnabledThrusts ) {
				Character.SwitchThrusts();
			}

			Character.Physics.Activate();


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
						//e.SwitchToWeapon( MyDefinitionId.Parse( string.Empty ) );
						e.SwitchToWeapon( null );
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
			MyEntity ent = null;

			// if( Ghost == null ) {
			// 	MyObjectBuilder_GhostCharacter ob = new MyObjectBuilder_GhostCharacter();
			// 	Ghost = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(ob);
			// 	MyAPIGateway.Entities.AddEntity(Ghost);
			// }

			MyObjectBuilder_Character character = new MyObjectBuilder_Character(){
        CharacterModel = "Astronaut",
        AIMode = true,
        //BotDefId = SerializableDefinitionId(MyObjectBuilderType.Parse("MyObjectBuilder_Character"), "MyObjectBuilder_BarbarianBot"),
        JetpackEnabled = true,
        PersistentFlags = MyPersistentEntityFlags2.InScene,
        Name = owner.Name,
        DisplayName = owner.Name,
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
        //Battery = new MyObjectBuilder_Battery(),
        PositionAndOrientation = new MyPositionAndOrientation(matrix),
        Health = 1.0f,
        MovementState = MyCharacterMovementEnum.Standing,//Walking,
        SubtypeName = "Default_Astronaut"
      };

      ent = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(character);

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

		public override string ToString() {
      return Owner.Name + " " + base.ToString();
    }


	}

}
