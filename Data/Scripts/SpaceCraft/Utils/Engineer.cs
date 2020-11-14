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

		protected MyCharacterJetpackComponent Jetpack = null;
		protected MyCharacterRagdollComponent Ragdoll = null;
		protected MyAnimationControllerComponent Animation = null;

		public IMyIdentity Identity;
    public IMyCharacter Character;
		//public MyGhostCharacter Ghost = new MyGhostCharacter();

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
				Character = Spawn();
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
				case Orders.Scout:
					Scout();
					break;
				case Orders.Follow:
					Follow();
					break;
			}
			// Tick++;
			// if( Tick == 99 ) {
			// 	Tick = 0;
			// }

		}

		public Engineer( Faction owner, IMyCharacter character = null ) {
			Owner = owner;

			Character = character ?? Spawn();
			Entity = Character;

			Flying = true;
	    Spacecraft = true;
	    Drills = true;
	    Welders = true;
	    Griders = true;
			Cargo = true;
			Fighter = true;
			//MyAPIGateway.Session.RegisterComponent(this, MyUpdateOrder.BeforeSimulation, 0);
			/*if( data == string.Empty ) return;
			try {
				var npcData = MyAPIGateway.Utilities.SerializeFromBinary<Engineer>(Convert.FromBase64String(data));
			}*/
			Initialize();
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

		public override bool Execute( Order order, bool force = false ) {
			//MyAPIGateway.Utilities.ShowMessage( Owner.Name, order.ToString() );
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

		public void Follow() {
			if( CurrentOrder == null || CurrentOrder.Player == null ) return;
			CurrentOrder.Destination = CurrentOrder.Player.GetPosition();
			Move();
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
			if( CurrentOrder.Planet == null ) {
				CurrentOrder.Planet = SpaceCraftSession.GetClosestPlanet(destination);
			}
			//Vector3 destination = (CurrentOrder.Destination == Vector3D.Zero ? CurrentOrder.Target.WorldMatrix.Translation : CurrentOrder.Destination);
			double distance = Vector3D.Distance(destination,Character.WorldMatrix.Translation);
			Vector3D closestPoint = CurrentOrder.Planet.GetClosestSurfacePointGlobal(destination);
			double altitude = Vector3D.Distance(destination,closestPoint);

			if( distance < CurrentOrder.Range ) {
				destination = Vector3.Zero;
				Character.Physics.ClearSpeed();
				return false;
			} else {
				if( !Character.EnabledThrusts ) {
					Character.SwitchThrusts();
				}
				if( altitude < 2f ) {
					Vector3D up = closestPoint - CurrentOrder.Planet.WorldMatrix.Translation;
					up.Normalize();
					destination = destination + (up*2f);
				}
				//destination.Normalize();
				Vector3 targetDelta = Vector3.Normalize(destination - Character.WorldMatrix.Translation);
				Jetpack.MoveAndRotate( ref targetDelta, ref rotation, 0.0f, false );
				//Jetpack.MoveAndRotate( ref destination, ref rotation, 0.0f, false );
			}

			return true;
		}

		// internal double EstimateDistanceToGround(Vector3D worldPoint)
    // {
    //     Vector3D localPoint;
    //     MyVoxelCoordSystems.WorldPositionToLocalPosition(Planet.PositionLeftBottomCorner, ref worldPoint, out localPoint);
    //     return Math.Max(0.0f, Planet.Storage.DataProvider.GetDistanceToPoint(ref localPoint));
    // }

		public void Attack() {
			if( CurrentOrder.Target == null || CurrentOrder.Target.MarkedForClose || CurrentOrder.Target.Closed ) {
				CurrentOrder = null;
				return;
			}

			//Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;
			if( CurrentOrder.Step == Steps.Pending ) {
				//BeginShoot( MyShootActionEnum.PrimaryAction );
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
						Range = 50f,
						Entity = Owner.GetBestRefinery(this)
					}, true );
					return;
				} else {
					foreach( string ore in CurrentOrder.Resources.Keys ) {
						inv.AddItems(CurrentOrder.Resources[ore], new MyObjectBuilder_Ore(){
			        SubtypeName = ore
			      } );
					}
					// inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_Ore(){
		      //   SubtypeName = CurrentOrder.SubtypeName ?? "Stone"
		      // } );
				}

			}
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


		public IMyCharacter Spawn() {
			return Spawn(Owner.GetSpawnLocation());
		}

		public IMyCharacter Spawn( MatrixD matrix ) {
			if( matrix == null ) return null;

			MyObjectBuilder_Character character = new MyObjectBuilder_Character(){
        CharacterModel = "Astronaut",
        AIMode = true,
        //BotDefId = SerializableDefinitionId(MyObjectBuilderType.Parse("MyObjectBuilder_Character"), "MyObjectBuilder_BarbarianBot"),
        JetpackEnabled = true,
        PersistentFlags = MyPersistentEntityFlags2.InScene,
				// Name = Owner.Name,
				// DisplayName = Owner.Name,
        Name = Owner.Founder == null ? Owner.Name : Owner.Founder.DisplayName,
        DisplayName = Owner.Founder == null ? Owner.Name : Owner.Founder.DisplayName,
				ColorMaskHSV = Color.Gold.ToVector3(),
				//PlayerSteamId = (ulong)(Owner == null || Owner.MyFaction == null ? 0 : Owner.MyFaction.FounderId),
				PlayerSteamId = Owner.MyFaction == null ? 0 : MyAPIGateway.Players.TryGetSteamId(Owner.MyFaction.FounderId),
				PlayerSerialId = (int)(Owner == null || Owner.MyFaction == null ? 0 : Owner.MyFaction.FounderId),
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

      MyEntity ent = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(character);
			if( Owner != null && Owner.Founder != null ) {
				//MyAPIGateway.Players.SetControlledEntity((ulong)Owner.MyFaction.FounderId, ent);
				//MyAPIGateway.Players.SetControlledEntity(MyAPIGateway.Players.TryGetSteamId(Owner.MyFaction.FounderId), ent);
				//																																											PlayerId
				MyAPIGateway.Players.SetControlledEntity((ulong)Owner.Founder.PlayerId, ent);
				//MyAPIGateway.Players.ExtendControl(Ghost, ent);
				//AddPlayerToFaction (long playerId, long factionId)

			}


			//MyAPIGateway.Session.Factions.AddPlayerToFaction (long playerId, long factionId)
			//MyAPIGateway.Players.TryExtendControl (IMyControllableEntity entityWithControl, IMyEntity entityGettingControl)

			if( ent != null ) {
        ent.Flags &= ~EntityFlags.Save;
        //ent.Flags &= ~EntityFlags.NeedsUpdate;
        ent.Render.Visible = true;
        ent.NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        MyAPIGateway.Entities.AddEntity(ent);
				IMyCharacter c = ent as IMyCharacter;
				// IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
				// if( player != null && Owner.MyFaction != null )
				// 	MyAPIGateway.Session.Factions.AddPlayerToFaction(player.PlayerID, Owner.MyFaction.FactionId);


        return c;

      }

			return null;
		}

		public override string ToString() {
      return Owner.Name + " " + base.ToString();
    }


	}

}
