using System;
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
		protected MyCharacterStatComponent Stats = null;
		protected MyEntityStat Health = null;
		protected MyCharacterOxygenComponent Oxygen = null;
		protected MyResourceDistributorComponent Energy = null;
		private static Color Blue = new Color(0,153,255);

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
			if( Character == null || Character.Integrity == 0 || Character.Closed || Character.MarkedForClose ) {
				CurrentOrder = null;
				MatrixD matrix = Character.WorldMatrix;
				Character = Spawn();
				Initialize();
				return;
			}

			Jetpack.FuelDefinition.EnergyDensity = 1.0f;
			if( Oxygen != null ) {
				Oxygen.SuitOxygenAmount = 1.0f;
				Oxygen.CharacterGasSink.SetInputFromDistributor(OBTypes.Electricity,10f,true,true);
			}
			// if( Energy != null ) Energy.Value = 1f;


			if( CurrentOrder == null || CurrentOrder.Step == Steps.Completed ) Next();
			if( CurrentOrder == null ) return;

			Tick++;

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

			if( Tick == 99 ) {
				Tick = 0;
			}

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
			Fighter = false;
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
			Oxygen = Character.Components.Get<MyCharacterOxygenComponent>();
			Energy = Character.Components.Get<MyResourceDistributorComponent>();

			// Stats = Character.Components.Get<MyCharacterStatComponent>();
			//
			// if( Stats != null ) {
			// 	// Stats.TryGetStat(MyStringHash.Get("Health"), out Health);
			// 	Stats.TryGetStat(MyStringHash.Get("Oxygen"), out Oxygen);
			// 	Stats.TryGetStat(MyStringHash.Get("Energy"), out Energy);
			// }
			//MyAPIGateway.Players.SetControlledEntity(MyAPIGateway.Session.Player.SteamUserId, Character as IMyEntity);

			Character.DoDamage(0.0f, MyStringHash.Get(string.Empty), true); // Hack to properly init Physics

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
						// SwitchToWeapon( Weapons.Grinder );
						// SwitchToWeapon( Weapons.Drill );
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
			if( Character == null || Jetpack == null || Character.Integrity == 0 || (CurrentOrder.Destination == Vector3D.Zero && CurrentOrder.Target == null) ) {
				CurrentOrder.Complete();
				return false;
			}
			// Simple timeout
			CurrentOrder.Tick++;
			if( CurrentOrder.Tick == 500 ) {
				return false;
			}
			Vector2 rotation = Vector2.Zero;
			// Vector3 destination = Vector3.Normalize(MyAPIGateway.Session.Player.GetPosition() - Character.WorldMatrix.Translation);
			// double distance = Vector3D.Distance(MyAPIGateway.Session.Player.GetPosition(),Character.WorldMatrix.Translation);
			//Vector3 destination = Vector3.Normalize(CurrentOrder.Destination - Character.WorldMatrix.Translation);

			Vector3 destination = (CurrentOrder.Target == null ? CurrentOrder.Destination : CurrentOrder.Target.WorldMatrix.Translation);
			CurrentOrder.Planet =  CurrentOrder.Planet ?? SpaceCraftSession.GetClosestPlanet(destination);

			//Vector3 destination = (CurrentOrder.Destination == Vector3D.Zero ? CurrentOrder.Target.WorldMatrix.Translation : CurrentOrder.Destination);
			double distance = Vector3D.Distance(destination,Character.WorldMatrix.Translation);
			Vector3D closestPoint = CurrentOrder.Planet.GetClosestSurfacePointGlobal(destination);
			// double altitude = Vector3D.Distance(destination,closestPoint);

			// LineD line = new LineD(Character.WorldMatrix.Translation,destination);
			// Vector3D? hit = null;

			if( distance < CurrentOrder.Range ) {
				destination = Vector3.Zero;
				Character.Physics.ClearSpeed();
				return false;
			} else {
				if( !Character.EnabledThrusts ) {
					Character.SwitchThrusts();
				}

				// if( CurrentOrder.Planet.GetIntersectionWithLine(ref line,out hit, true) && hit.HasValue ) {
					Vector3D up = Vector3D.Normalize(closestPoint - CurrentOrder.Planet.WorldMatrix.Translation);
					destination = destination + (up*10f);
				// }

				// if( altitude < 2f ) {
				// 	Vector3D up = closestPoint - CurrentOrder.Planet.WorldMatrix.Translation;
				// 	up.Normalize();
				// 	destination = destination + (up*2f);
				// }
				//destination.Normalize();
				// MatrixD matrix = MatrixD.CreateWorld(destination);
				// MySimpleObjectDraw.DrawTransparentSphere(ref matrix, 5f, ref Blue, MySimpleObjectRasterizer.SolidAndWireframe, 20);

				Vector3 targetDelta = Vector3.Normalize(destination - Character.WorldMatrix.Translation);
				try {
					MatrixD invWorldRot = Character.PositionComp.WorldMatrixNormalizedInv.GetOrientation();
					Vector3 thrust = Vector3D.Transform(targetDelta, invWorldRot);// - Vector3D.Transform(velocityToCancel, invWorldRot);
          thrust.Normalize();
					// Jetpack.MoveAndRotate( ref targetDelta, ref rotation, 0.0f, false );
					Jetpack.MoveAndRotate( ref thrust, ref rotation, 0.0f, false );
				} catch( NullReferenceException e ) {
					// Sometimes the character is dead and this is the only way I could prevent a crash
				}

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
			if( Character == null || Character.Integrity == 0 || CurrentOrder == null ) {
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
						//BeginShoot( MyShootActionEnum.PrimaryAction );
						SwitchToWeapon( Weapons.Drill );
					}
					if( CurrentOrder != null ) {
						CurrentOrder.Progress();
					}
				}
			} else {
				// Add resources to inventory
				IMyInventory inv = GetInventory()[0];
				if( inv == null ) return;
				if( inv.CurrentVolume > inv.MaxVolume * (VRage.MyFixedPoint)0.9f ) {
					// MyAPIGateway.Utilities.ShowMessage( "Drill", "Inventory Full: " + ToString() );
					Execute( new Order{
						Type = Orders.Deposit,
						// Range = 5000f,
						Range = 10f,
						//Entity = Owner.MainBase ?? Owner.GetBestRefinery(this)
						Entity = Owner.GetClosestRefinery(Character.WorldMatrix.Translation)
					}, true );
					return;
				} else if( CurrentOrder.Resources != null ) {
					if( Tick == 99 )
						foreach( string ore in CurrentOrder.Resources.Keys ) {
							inv.AddItems(CurrentOrder.Resources[ore], new MyObjectBuilder_Ore(){
				        SubtypeName = ore
				      } );
						}
					// inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_Ore(){
		      //   SubtypeName = CurrentOrder.SubtypeName ?? "Stone"
		      // } );
				} else {
					CurrentOrder = null;
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
				// MyAPIGateway.Utilities.ShowMessage( "SwitchToWeapon", weapon.ToString() );
				MyDefinitionId? id = null;

				Sandbox.Game.Entities.IMyControllableEntity e = Character as Sandbox.Game.Entities.IMyControllableEntity;


				// if( e == null ) return;

				e.EndShoot( MyShootActionEnum.PrimaryAction );

				switch( weapon ) {
					case Weapons.Welder:
						id = MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/WelderItem" );
						break;
					case Weapons.Drill:
						id = MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/HandDrillItem" );
						//e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/HandDrillItem" ) );
						break;
					case Weapons.Grinder:
						id = MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AngleGrinderItem" );
						// e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AngleGrinderItem" ) );
						break;
					case Weapons.Gun:
						id = MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem" );
						// e.SwitchToWeapon( MyDefinitionId.Parse( "MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem" ) );
						break;
				}

				if( id.HasValue && e.CanSwitchToWeapon(id) ) {
					e.SwitchToWeapon(id.Value);
				} else {
					e.SwitchToWeapon( null );
				}
			}
		}

		// public void DoDetect() {
		// 	MyCharacterDetectorComponent detector = Character.Components.Get<MyCharacterDetectorComponent>();
		// 	if( detector == null ) return;
		// 	//detector.DoDetection();
		// 	//MyAPIGateway.Physics.CalculateNaturalGravityAt()
		// 	//MyAPIGateway.Physics.CalculateArtificialGravityAt()
		// }


		public IMyCharacter Spawn() {
			return Spawn(Owner.GetSpawnLocation());
		}

		public IMyCharacter Spawn( MatrixD matrix ) {
			if( matrix == null ) return null;

			MyObjectBuilder_Character character = new MyObjectBuilder_Character(){
        CharacterModel = Owner == null ? "Astronaut" : Owner.GetCharacterModel(),
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
				// PlayerSteamId = Owner.MyFaction == null ? 0 : MyAPIGateway.Players.TryGetSteamId(Owner.MyFaction.FounderId),
				// PlayerSerialId = (int)(Owner == null || Owner.MyFaction == null ? 0 : Owner.MyFaction.FounderId),
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
						},
						new MyObjectBuilder_InventoryItem() {
							Amount = (VRage.MyFixedPoint)1,
							PhysicalContent = new MyObjectBuilder_PhysicalGunObject(){
								SubtypeName = "AutomaticRifleItem"
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
			if( Owner != null && Owner.Founder != null ) {
				//MyAPIGateway.Players.SetControlledEntity((ulong)Owner.Founder.PlayerId, ent);
			}


			//MyAPIGateway.Session.Factions.AddPlayerToFaction (long playerId, long factionId)
			//MyAPIGateway.Players.TryExtendControl (IMyControllableEntity entityWithControl, IMyEntity entityGettingControl)

			if( ent != null ) {
        ent.Flags &= ~EntityFlags.Save;
				ent.Flags &= ~EntityFlags.Sync;
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
