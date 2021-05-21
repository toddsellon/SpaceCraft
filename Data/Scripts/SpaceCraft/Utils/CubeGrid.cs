using System;
using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.Models;
using Sandbox.ModAPI;
// using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Definitions;
using Sandbox.ModAPI.Physics;
//using Sandbox.ModAPI.Ingame;
using Sandbox.Game;
using Sandbox.Game.Screens.Terminal.Controls;
using Sandbox.Game.AI;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities.Cube;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Components;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using VRage.Game.Components;
using VRageMath;
using SpaceEngineers.Game.Entities;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.EntityComponents.GameLogic.Discovery;
using Sandbox.Common.ObjectBuilders;
using SpaceCraft.Utils;

namespace SpaceCraft.Utils {

	public enum Needs {
		None,
		Power,
		Components,
		Storage,
		Production,
		Refinery,
		Drills
	};



	//[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class CubeGrid : Controllable {

		public class Work {
			public List<IMySlimBlock> Blocks;
			public Dictionary<IMyCubeBlock,CubeGrid.Item> Needs;
			public float Integrity = 0f;
		}

		public class Item {
			public MyDefinitionId Id;
			public VRage.MyFixedPoint Amount;

			public Item() {

			}

			public Item( MyBlueprintDefinitionBase.Item item ) {
				Id = item.Id;
				Amount = item.Amount;
			}

			public override string ToString()
      {
          return string.Format("{0}x {1}", Amount, Id);
      }
		}

		public CubeGrid.Work Job;
		// private static float PlayerDistance = 10000;
		private static float PlayerDistance = float.MaxValue;
		private static SerializableVector3 DefaultColor = new SerializableVector3(0.575f,0.150000036f,0.199999958f);
		private static readonly uint DrillLimit = 3;
		public IMySlimBlock ConstructionSite;
		public Needs Need = Needs.None;
    public IMyCubeGrid Grid;
		public List<IMyCubeGrid> Subgrids = new List<IMyCubeGrid>();
		public string Prefab;
		public IMyCubeGrid SuperGrid;
		protected static int NumGrids = 0;
		private static int LastTick = 0;
		public int Tick = 0;
		// private int TargetTick = 0;
		public IMyRemoteControl Remote;
		public Dictionary<string,int> Balance = null;
		private bool Drone = false;
		public IMyEntity Target = null;

		public CubeGrid DockedTo;
    public List<CubeGrid> Docked = new List<CubeGrid>();

    public void ToggleDocked( CubeGrid grid ) {

			if( DockedTo != null ) {
				DockedTo.ToggleDocked(grid);
				return;
			}

			// List<IMySlimBlock> mine = GetBlocks<IMyFunctionalBlock>(null,true);
			// List<IMySlimBlock> theirs = grid.GetBlocks<IMyFunctionalBlock>(null,true);
			bool disconnect = true;
      if( grid.DockedTo == null ) {
				Docked.Add( grid );
				grid.DockedTo = this;
				//grid.ConstructionSite = null;
				disconnect = false;
			} else {
				Docked.Remove( grid );
				grid.DockedTo = null;
				//grid.FindConstructionSite();
			}
			// foreach( IMySlimBlock i in mine ) {
			// 	foreach( IMySlimBlock j in theirs ) {
			// 		ConnectBlocks(i.FatBlock as IMyFunctionalBlock, j.FatBlock as IMyFunctionalBlock, disconnect);
			// 	}
			// }

    }

		public void ConnectBlocks( IMyFunctionalBlock connector, IMyFunctionalBlock connectee, bool disconnect = false ) {
			MyResourceDistributorComponent a = connector.Components.Get<MyResourceDistributorComponent>();
			MyResourceDistributorComponent b = connectee.Components.Get<MyResourceDistributorComponent>();
			if( a == null || b == null ) return;
			if( disconnect ) {
				a.RemoveSink(connectee.Components.Get<MyResourceSinkComponent>());
				b.RemoveSink(connector.Components.Get<MyResourceSinkComponent>());
				a.RemoveSource(connectee.Components.Get<MyResourceSourceComponent>());
				b.RemoveSource(connector.Components.Get<MyResourceSourceComponent>());
			} else {
				a.AddSink(connectee.Components.Get<MyResourceSinkComponent>());
				b.AddSink(connector.Components.Get<MyResourceSinkComponent>());
				a.AddSource(connectee.Components.Get<MyResourceSourceComponent>());
				b.AddSource(connector.Components.Get<MyResourceSourceComponent>());
			}
			// MyResourceSourceComponent source = connector.Components.Get<MyResourceSourceComponent>();
			// MyResourceSourceComponent sink = connectee.Components.Get<MyResourceSourceComponent>();
			// source.TemporaryConnectedEntity = connectee as MyEntity;
			// sink.TemporaryConnectedEntity = connector as MyEntity;
		}

		public MatrixD WorldMatrix
		{
			get
			{
				 return Grid == null ? MatrixD.Zero : Grid.WorldMatrix;
			}
			set
			{
				 if( Grid != null ) Grid.WorldMatrix = value;
			}
		}

		public bool IsProducing {
			get {
				IMyAssembler ass = GetAssembler();
				return ass == null ? false : ass.IsQueueEmpty;
			}
		}


		public CubeGrid( IMyCubeGrid grid ) {
			Grid = grid;
			Tick = LastTick;
			LastTick += 3;
			if( LastTick >= 96 ) {
				LastTick = 0;
			}
			Entity = Grid;
			if( grid != null )
				CheckFlags();
		}

		// Main loop
		public override void UpdateBeforeSimulation() {
			if( Grid == null ) return;

			Tick++;
			CheckOrder();
			if( (DockedTo != null && ConstructionSite != null) || Drills ) {
				Grid.Physics.ClearSpeed();
			}

			// if( Job != null ) {
			// 	try {
			// 		UpdateInventory();
			// 	} catch( Exception e ) {
			// 		Job = null;
			// 	}
			// }

			if( Tick == 99 ) {
				//AssessInventory();
				if( Drone || (Fighter && Owner.Following != null) ) CheckAutopilot();

				// if( DockedTo == null )
				// 	UpdateInventory();
				// if( Grid.IsStatic )
				// 	Drill();
				Tick = 0;
			}
		}

		protected void CheckAutopilot() {
			if( Grid == null ) return;

			Remote = Remote ?? FindRemoteControl();
			if( Remote == null || !Remote.IsFunctional )
				Remote = FindRemoteControl();
			// if( Remote == null || (Remote.IsAutoPilotEnabled && Grid.Physics.IsMoving) ) return;
			if( Remote == null ) return;

			Vector3D position = Vector3D.Zero;

			if( Owner.Following == null ) {
				IMyEntity enemy = null;

				//TargetTick++;
				// if( TargetTick == 180 || (Target != null && (Target.Closed || Target.MarkedForClose) ) ) {
				if( Target != null && (Target.Closed || Target.MarkedForClose) ) {
					Target = null;
					//TargetTick = 0;
				}
				bool wasNull = Target == null;
				Target = Target ?? Owner.ResolveTarget(Grid.WorldMatrix.Translation);
				if( wasNull && Convars.Static.Debug )
					MyAPIGateway.Utilities.ShowMessage( Grid.DisplayName, "Target -> " + Target.DisplayName );
				enemy = Target;
				if( enemy == null ) return;
				position = enemy.WorldMatrix.Translation;
			} else {
				position = Owner.Following.GetPosition();
			}

			// Previous Behavior:
			// IMyPlayer player = Owner.Following == null ? Owner.GetClosestEnemy(Grid.WorldMatrix.Translation) : Owner.Following;
			//
			// if( player == null ) return;
			//
			// Vector3D position = player.GetPosition();

			MyPlanet planet = SpaceCraftSession.GetClosestPlanet(Grid.WorldMatrix.Translation);

			Remote.ClearWaypoints();

			LineD line = new LineD(Grid.WorldMatrix.Translation,position);
			Vector3D? hit = null;

			if( planet != null && planet.GetIntersectionWithLine(ref line,out hit, true) && hit.HasValue ) {
				//Vector3D direction = Grid.WorldMatrix.Translation - position;
				Vector3D direction = Vector3D.Normalize(hit.Value - Grid.WorldMatrix.Translation);
				Vector3D up = Vector3D.Normalize(Grid.WorldMatrix.Translation - planet.WorldMatrix.Translation);
				// Remote.AddWaypoint( hit+(Grid.WorldMatrix.Forward*1000), "Detour" );
				Remote.AddWaypoint( Grid.WorldMatrix.Translation+(direction*1000)+(up*2000), "Detour" );
			}

			if( Owner.Following != null && Owner.FollowDistance > 0 ) {
				Vector3D dir = Vector3D.Normalize(position - Grid.WorldMatrix.Translation);
				Remote.AddWaypoint( position - (dir*Owner.FollowDistance), "Offset Player Location" );
			} else
				Remote.AddWaypoint( position, Target.DisplayName );



			if( Remote.IsAutoPilotEnabled ) return;


			Remote.FlightMode = Sandbox.ModAPI.Ingame.FlightMode.OneWay;
			Remote.SetCollisionAvoidance(true);
			Remote.SetAutoPilotEnabled(true);
		}

		public void CheckOrder() {
			if( CurrentOrder == null || CurrentOrder.Step == Steps.Completed ) Next();
			if( CurrentOrder == null ) return;

			switch( CurrentOrder.Type ) {
				case Orders.Move:
					Move();
					break;
				case Orders.Deposit:
					Deposit();
					break;
				case Orders.Withdraw:
					Withdraw();
					break;
				case Orders.Drill:
					if( Tick == 99 )
						Drill();
					break;
			}
		}

		public override bool Move() {
			if( CurrentOrder == null || Grid == null ) return false;
			Vector3D destination = (CurrentOrder.Target == null ? CurrentOrder.Destination : CurrentOrder.Target.WorldMatrix.Translation);
			if( destination == Vector3D.Zero ) return false;

			if( Remote == null ) Remote = FindRemoteControl();
			if( Remote == null ) {
				 CurrentOrder = null;
				 return false;
			}

			if( CurrentOrder.Step == Steps.Pending ) {
				Remote.ClearWaypoints();
				Remote.AddWaypoint(destination, "Destination");
				Remote.SetAutoPilotEnabled(true);
				CurrentOrder.Progress();
			} else {
				if( !Remote.IsAutoPilotEnabled )
					Grid.Physics.ClearSpeed();
				return Remote.IsAutoPilotEnabled;
			}

			return true;
		}

		protected IMyRemoteControl FindRemoteControl() {
			List<IMySlimBlock> remotes = GetBlocks<IMyRemoteControl>();
			foreach( IMySlimBlock remote in remotes ) {
				if( remote.FatBlock.IsFunctional )
					return remote.FatBlock as IMyRemoteControl;
			}
			return null;
		}

		public override bool IsStatic
		{
			get
			{
				 return Grid == null ? false : Grid.IsStatic;
			}
			// set
			// {
			// 	 if( Grid == null ) return;
			// 	 if( value && !Grid.IsStatic ) Grid.Physics.ConvertToStatic();
			// 	 else if( !value && Grid.IsStatic ) Grid.ConvertToDynamic();
			// }
		}



		public void Drill() {
			int max = 3;
			List<IMySlimBlock> drills = GetBlocks<IMyShipDrill>(null,true);
			foreach( IMySlimBlock slim in drills ) {
				if( !slim.FatBlock.IsFunctional ) continue;
				IMyInventory inv = slim.FatBlock.GetInventory(0);
				if( inv == null ) continue;

				//if( inv.IsFull ) {
				//if( inv.CurrentVolume.ToIntSafe() > inv.MaxVolume.ToIntSafe()/8 ) {
				/*if( inv.CurrentVolume > inv.MaxVolume * (VRage.MyFixedPoint)0.0625f  ) {
					// Execute( new Order{
					// 	Type = Orders.Deposit,
					// 	Range = 50000f,
					// 	Entity = Owner.GetBestRefinery(this)
					// }, true );
					CubeGrid destination = Owner.MainBase ?? Owner.GetBestRefinery(this);
					if( destination == null ) {
						CurrentOrder.Complete();
						return;
					}
					List<IMyInventoryItem> items = null;
					List<IMyInventory> theirs = destination.GetInventory();
					foreach(IMyInventory inventory in theirs) {
						items = inv.GetItems();
						if( items.Count == 0 ) break;
  					for( int i = items.Count-1; i >= 0; i-- ) {
              IMyInventoryItem item = items[i];
              if( item == null ) continue;
              inv.TransferItemTo(inventory, i, null, true, item.Amount, false );
  					}
					}

					items = inv.GetItems();
					VRage.MyFixedPoint amount = (VRage.MyFixedPoint)100000;
					for( int i = items.Count-1; i >= 0; i-- ) {
						inv.RemoveItemsAt(i, amount);
					}
				} else*/
					foreach( string ore in CurrentOrder.Resources.Keys ) {
						inv.AddItems(CurrentOrder.Resources[ore], new MyObjectBuilder_Ore(){
							SubtypeName = ore
						} );
					}


				max--;

				if( max == 0 ) break;
			}
		}


		// public override void Init( MyObjectBuilder_SessionComponent session ) {
		// 	base.Init(session);
		// }

		public override List<IMyInventory> GetInventory( List<IMySlimBlock> blocks = null ) {
			List<IMyInventory> list = new List<IMyInventory>();
			if( blocks == null ) {
				blocks = GetBlocks<IMySlimBlock>();
			}

			foreach( IMySlimBlock slim in blocks ) {
				IMyCubeBlock block = slim.FatBlock;
				if( block == null || !block.IsFunctional ) continue;
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.GetInventory(i);
					if( inv != null )
						list.Add( inv );
				}
			}

      return list;
    }

		public static List<IMySlimBlock> Filter<t>( List<IMySlimBlock> blocks ) {
			List<IMySlimBlock> ret = new List<IMySlimBlock>();

			foreach(IMySlimBlock block in blocks) {
				if( block.FatBlock == null || !(block.FatBlock is t) ) continue;
				ret.Add(block);
			}
			return ret;
		}

		public void CheckFlags() {

			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>(null,true, x => x.FatBlock != null && x.FatBlock is IMyFunctionalBlock);

			Flying = false;
			Spacecraft = false;
			// Wheels = GetBlocks<IMyMotorSuspension>(null,true).Count > 0;
			Wheels = Filter<IMyMotorSuspension>(blocks).Count > 0;
	    // Drills = GetBlocks<IMyShipDrill>(null,true).Count > 0;
			Drills = Filter<IMyShipDrill>(blocks).Count > 0;
	    //Welders = GetBlocks<IMyShipWelder>().Count > 0;
	    //Griders = GetBlocks<IMyShipGrinder>().Count > 0;
			//List<IMySlimBlock> blocks = GetBlocks<IMyAssembler>(null,true);
			List<IMySlimBlock> subset = Filter<IMyAssembler>(blocks);

			foreach( IMySlimBlock block in subset ) {
				if( !block.FatBlock.IsFunctional ) continue;
				if( block.FatBlock.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					FactoryTier = (uint)Math.Max(FactoryTier,1);
					RefineryTier = (uint)Math.Max(RefineryTier,1);
				} else {
					string subtype = block.FatBlock.BlockDefinition.SubtypeId;
					switch( subtype ) {
						case "ZergSurvivalKit":
	          case "ProtossSurvivalKitLarge":
	          case "ProtossSurvivalKit":
							FactoryTier = (uint)Math.Max(FactoryTier,1);
							RefineryTier = (uint)Math.Max(RefineryTier,1);
							break;
						default:
							FactoryTier = (uint)Math.Max(FactoryTier,subtype == "LargeAssembler" || subtype == "LargeProtossAssembler" || subtype == "LargeZergAssembler" ? 3 : 2);
							break;
					}

				}
			}
			// blocks = GetBlocks<IMyRefinery>(null,true);
			subset = Filter<IMyRefinery>(blocks);
			foreach( IMySlimBlock block in subset ) {
				if( !block.FatBlock.IsFunctional ) continue;
				string subtype = block.FatBlock.BlockDefinition.SubtypeId;
				RefineryTier = (uint)Math.Max(RefineryTier,subtype == "LargeRefinery" || subtype == "LargeProtossRefinery" || subtype == "LargeZergRefinery" ? 3 : 2);
			}
			//IMySmallMissileLauncher IMyUserControllableGun
			if( Drills || IsStatic )
				Fighter = false;
			else
				// Fighter = GetBlocks<IMyUserControllableGun>(null,true).Count > 0 || GetBlocks<IMySmallGatlingGun>(null,true).Count > 0 || GetBlocks<IMySmallMissileLauncher>(null,true).Count > 0;
				Fighter = Filter<IMyUserControllableGun>(blocks).Count > 0 || Filter<IMySmallGatlingGun>(blocks).Count > 0 || Filter<IMySmallMissileLauncher>(blocks).Count > 0;

			// List<IMySlimBlock> thrusters = GetBlocks<IMyThrust>(null,true);
			subset = Filter<IMyThrust>(blocks);
			foreach( IMySlimBlock block in subset ) {
				switch( block.FatBlock.BlockDefinition.SubtypeId ) {
					case "LargeBlockLargeAtmosphericThrust":
					case "LargeBlockSmallAtmosphericThrust":
					case "SmallBlockLargeAtmosphericThrust":
					case "SmallBlockSmallAtmosphericThrust":
						Flying = true;
						break;
					case "SmallBlockSmallThrust":
					case "SmallBlockLargeThrust":
					case "LargeBlockSmallThrust":
					case "LargeBlockLargeThrust":
						Spacecraft = true;
						break;
					default:
						Flying = true;
						Spacecraft = true;
						break;
				}
			}
		}

		public void SetBehaviour( string presetName = "Default" ) {
			// MyVisualScriptLogicProvider.SetName(cubeGrid.EntityId, cubeGrid.EntityId.ToString());
			if( !Drone && Grid != null && !Grid.MarkedForClose && !Grid.Closed ) {
				MyVisualScriptLogicProvider.SetName(Grid.EntityId, Grid.EntityId.ToString());

				//MyVisualScriptLogicProvider.MusicPlayMusicCue(MusicId);
				Drone = true;
				// MyVisualScriptLogicProvider.SetDroneBehaviourFull(Grid.EntityId.ToString(), presetName, true, false, null, false, null, 10, PlayerDistance);
				//
				//
				// IMyEntity enemy = Owner.GetClosestEnemy(Grid.WorldMatrix.Translation);
				// if( enemy != null ) {
				// 	MyVisualScriptLogicProvider.DroneTargetClear(Grid.EntityId.ToString());
				// 	MyVisualScriptLogicProvider.DroneTargetAdd(Grid.EntityId.ToString(), enemy as MyEntity);
				// }
			}

		}

		public List<IMySlimBlock> GetBlocks<t>( List<IMySlimBlock> blocks = null, bool excludeDocked = false, Func<IMySlimBlock,bool> collect = null ) {
			//x => x.FatBlock != null
			List<IMySlimBlock> list = new List<IMySlimBlock>();
			if( Grid == null || Grid.Closed ) return list;
			Grid.GetBlocks( list, collect );


			if( SuperGrid != null && !SuperGrid.Closed ) {
				SuperGrid.GetBlocks( list, collect );
			}

			foreach( IMyCubeGrid grid in Subgrids ) {
				if( grid == null ) continue;
				grid.GetBlocks( list, collect );
			}

			if( !excludeDocked ) {
				foreach( CubeGrid grid in Docked ) {
					if( grid.Grid == null || grid.Grid.Closed ) continue;
					grid.Grid.GetBlocks( list, collect: collect );
					foreach( IMyCubeGrid sub in grid.Subgrids ) {
						if( sub == null || sub.Closed ) continue;
						sub.GetBlocks( list, collect );
					}
				}
			}

			if( list.Count > 0 && !(list[0] is t) ) {
				List<IMySlimBlock> ret = new List<IMySlimBlock>();

				if( blocks != null ) ret.AddRange( blocks );

				foreach( IMySlimBlock block in list ) {
					if( block.FatBlock == null || !(block.FatBlock is t) ) continue;
					ret.Add(block);
				}

				return ret;
			}

			if( blocks != null )
				list.AddRange( blocks );

			return list;
		}

		public CubeGrid.Work GetJob() {
			if( Balance == null )
				if( ConstructionSite != null && ConstructionSite.FatBlock != null && (ConstructionSite.FatBlock.MarkedForClose || ConstructionSite.FatBlock.Closed) ) {
					FindConstructionSite();
				}
			//if(ConstructionSite != null) ConstructionSite.SpawnFirstItemInConstructionStockpile(); // Hack to fix world reload bug (might not be necessary)
			// float old = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity;

			CubeGrid.Work job = new CubeGrid.Work{
				Blocks = GetBlocks<IMySlimBlock>( collect: x => x.FatBlock != null && (x.FatBlock is IMyBatteryBlock || x.FatBlock.GetInventory() != null) ),
				Integrity = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity
			};

			// List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			List<IMySlimBlock> blocks = job.Blocks;

			IMyAssembler main = GetAssembler(blocks);

			if( ConstructionSite != null && ConstructionSite.FatBlock != null && main != null && main.GetQueue().Count == 0 ) { // Not enough components
				MyAPIGateway.Utilities.InvokeOnGameThread( () => AddQueueItems(ConstructionSite.FatBlock,true,main) );
				// AddQueueItems(ConstructionSite.FatBlock,true,main);// Try again
			} else if( Balance != null && Balance.Count > 0 && main != null && main.GetQueue().Count == 0 ) {
				MyAPIGateway.Utilities.InvokeOnGameThread( () => AddQueueItems(Balance,true,main) );
			}

			Dictionary<IMyCubeBlock,CubeGrid.Item> needs = new Dictionary<IMyCubeBlock,CubeGrid.Item>();

			job.Needs = needs;
			CubeGrid.Item bp = null;
			// Assess needs
			foreach( IMySlimBlock slim in blocks ) {
				if( slim == null ) continue;

				IMyCubeBlock block = slim.FatBlock;
				if( block == null || !block.IsFunctional ) continue;
				CubeGrid.Item need = AssessNeed(block);
				if( need != null ) {
					if( need.Id.TypeId == OBTypes.Magazine )
						bp = need;

					if( !needs.ContainsKey(block)) // This is a fix for bug on reload, should get removed eventually
						needs.Add(block,need);
				}
				else if( block is IMyAssembler && block != main ) {
					IMyAssembler factory = block as IMyAssembler;
					//string SubtypeName = factory.SlimBlock.BlockDefinition.Id.SubtypeName;
					List<MyProductionQueueItem> queue = main.GetQueue();

					if( factory.IsQueueEmpty && queue.Count > 0 ) {
						// Cooperative Mode
						if( factory.CanUseBlueprint(queue[0].Blueprint) ) {
							MyAPIGateway.Utilities.InvokeOnGameThread( () => main.RemoveQueueItem(0, (VRage.MyFixedPoint)1) );
							// main.RemoveQueueItem(0, (VRage.MyFixedPoint)1);
							MyAPIGateway.Utilities.InvokeOnGameThread( () => factory.AddQueueItem(queue[0].Blueprint, (VRage.MyFixedPoint)1) );
							// factory.AddQueueItem(queue[0].Blueprint, (VRage.MyFixedPoint)1);
						}
					}
				}
				// if( slim != ConstructionSite )
					AllocateResources(block);
			}

			// See if ammo needs queue
			if( main != null && bp != null ) {

				MyBlueprintDefinitionBase bpd =	MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(bp.Id);
				if( bpd != null ) {

					List<MyProductionQueueItem> queue = main.GetQueue();
					if( queue.Count == 0 || queue[0].Blueprint != bpd )
						MyAPIGateway.Utilities.InvokeOnGameThread( () => main.InsertQueueItem(0,bpd, bp.Amount) );
						//main.InsertQueueItem(0,bpd, bp.Amount);
						//main.AddQueueItem(bpd, bp.Amount);
				}
			}

			return job;

		}

		public void FulfillNeeds( CubeGrid.Work job ) {
			if( job == null ) return;
			// Fulfill needs
			List<IMySlimBlock> blocks = job.Blocks;
			Dictionary<IMyCubeBlock,CubeGrid.Item> needs = job.Needs;

			try {
				foreach( IMySlimBlock slim in blocks ) {
					if( slim == null ) continue;
					IMyCubeBlock block = slim.FatBlock;
					if( block == null || block.Closed || !block.IsFunctional ) continue;



					List<IMyCubeBlock> fulfilled = new List<IMyCubeBlock>();
					foreach( IMyCubeBlock b in needs.Keys ) {
						if( b == null || block == b || b.Closed ) continue;
						IMyInventory inventory = b.GetInventory();

						if( inventory == null ) continue;
						CubeGrid.Item need = needs[b];
						//bool fnd = false;
						for( int i = 0; i < 2; i++ ) {
							IMyInventory inv = block.GetInventory(i);
							if( inv == null ) continue;
							List<IMyInventoryItem> items = inv.GetItems();
							int j = 0;

							foreach( IMyInventoryItem item in items ) {
								if( item.Content.TypeId == need.Id.TypeId
										&& (need.Id.SubtypeName == String.Empty || need.Id.SubtypeName == item.Content.SubtypeName )
										&& (b.BlockDefinition.TypeId != block.BlockDefinition.TypeId ) // Same type of block
										//&& (!needs.ContainsKey(block) || needs[block] != need ) // Need same thing
										//&& (need.Id != OBTypes.AnyOre || (need.Id == OBTypes.AnyOre && (item.Content.SubtypeName != "Ice" && item.Content.SubtypeName != "Stone" ) ) )
										&& (need.Id != OBTypes.AnyOre || (need.Id == OBTypes.AnyOre && block.BlockDefinition.TypeIdString != "MyObjectBuilder_SurvivalKit" ) )
									) {

									inv.TransferItemTo(inventory, j, null, true, need.Amount, false);
									//if( !fnd )
									if( need.Id != OBTypes.AnyOre && need.Id != OBTypes.Ice ) {
										fulfilled.Add(b);

										//fnd = true;
										break;
									}/* else {
										fnd = true;
									}*/
								}
								j++;
							}

							//if( fnd )
								//fulfilled.Add(b);
						}
					}

					foreach( IMyCubeBlock f in fulfilled ) needs.Remove(f);
				}
			} catch( Exception e ) {
				return;
			}

			if( ConstructionSite == null || Owner == null ) return;

			if( ConstructionSite.FatBlock == null || ConstructionSite.FatBlock.Closed ) {
				StopProduction( Filter<IMyAssembler>(blocks) );
				ConstructionCompleted();
				FindConstructionSite();
				return;
			}

			long owner = Owner.MyFaction == null ? (long)0 : Owner.MyFaction.FounderId;
			//ConstructionSite.IncreaseMountLevel(5.0f,Owner.MyFaction.FounderId);

			// if( ConstructionSite.CanContinueBuild(null))
			ConstructionSite.IncreaseMountLevel(5.0f, owner);

			if( ConstructionSite.IsFullIntegrity ) {
				StopProduction( Filter<IMyAssembler>(blocks) );
				ConstructionCompleted();
				FindConstructionSite();
			} else if( job.Integrity < ConstructionSite.BuildIntegrity ) {
				ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionProcess);
			}
		}

		public void UpdateInventory() {
			if( Job == null && Balance == null )
				if( ConstructionSite != null && ConstructionSite.FatBlock != null && (ConstructionSite.FatBlock.MarkedForClose || ConstructionSite.FatBlock.Closed) ) {
					FindConstructionSite();
				}
			if(ConstructionSite != null) ConstructionSite.SpawnFirstItemInConstructionStockpile(); // Hack to fix world reload bug (might not be necessary)
			// float old = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity;

			if( Job == null ) {
				Job = new CubeGrid.Work{
					Blocks = GetBlocks<IMySlimBlock>( collect: x => x.FatBlock != null && (x.FatBlock is IMyBatteryBlock || x.FatBlock.GetInventory() != null) ),
					Integrity = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity
				};
				return;
			}

			// List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			List<IMySlimBlock> blocks = Job.Blocks;
			if( blocks == null ) {
				Job = null;
				return;
			}
			IMyAssembler main = GetAssembler(blocks);

			if( Job.Needs == null )
				if( ConstructionSite != null && ConstructionSite.FatBlock != null && main != null && main.GetQueue().Count == 0 ) { // Not enough components
					AddQueueItems(ConstructionSite.FatBlock,true,main);// Try again
				}

			Dictionary<IMyCubeBlock,CubeGrid.Item> needs = null;

			if( Job.Needs == null ) {
				Job.Needs = needs = new Dictionary<IMyCubeBlock,CubeGrid.Item>();
				CubeGrid.Item bp = null;
				// Assess needs
				foreach( IMySlimBlock slim in blocks ) {
					if( slim == null ) continue;

					IMyCubeBlock block = slim.FatBlock;
					if( block == null || !block.IsFunctional ) continue;
					CubeGrid.Item need = AssessNeed(block);
					if( need != null ) {
						if( need.Id.TypeId == OBTypes.Magazine )
							bp = need;

						if( !needs.ContainsKey(block)) // This is a fix for bug on reload, should get removed eventually
							needs.Add(block,need);
					}
					else if( block is IMyAssembler && block != main ) {
						IMyAssembler factory = block as IMyAssembler;
						//string SubtypeName = factory.SlimBlock.BlockDefinition.Id.SubtypeName;
						List<MyProductionQueueItem> queue = main.GetQueue();

						if( factory.IsQueueEmpty && queue.Count > 0 ) {
							// Cooperative Mode
							if( factory.CanUseBlueprint(queue[0].Blueprint) ) {
								main.RemoveQueueItem(0, (VRage.MyFixedPoint)1);
								factory.AddQueueItem(queue[0].Blueprint, (VRage.MyFixedPoint)1);
							}
						}
					}
					// if( slim != ConstructionSite )
						AllocateResources(block);
				}

				// See if ammo needs queue
				if( main != null && bp != null ) {

					MyBlueprintDefinitionBase bpd =	MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(bp.Id);
					if( bpd != null ) {

						List<MyProductionQueueItem> queue = main.GetQueue();
						if( queue.Count == 0 || queue[0].Blueprint != bpd )
							main.InsertQueueItem(0,bpd, bp.Amount);
							//main.AddQueueItem(bpd, bp.Amount);
					}
				}

				return; // Finish next frame
			}

			needs = Job.Needs;
			if( needs == null ) {
				Job = null;
				return;
			}

			// Fulfill needs
			foreach( IMySlimBlock slim in blocks ) {
				if( slim == null ) continue;
				IMyCubeBlock block = slim.FatBlock;
				if( block == null || block.Closed || !block.IsFunctional ) continue;

				List<IMyCubeBlock> fulfilled = new List<IMyCubeBlock>();
				foreach( IMyCubeBlock b in needs.Keys ) {
					if( b == null || block == b || b.Closed ) continue;
					IMyInventory inventory = b.GetInventory();
					if( inventory == null ) continue;
					CubeGrid.Item need = needs[b];
					//bool fnd = false;
					for( int i = 0; i < 2; i++ ) {
						IMyInventory inv = block.GetInventory(i);
						if( inv == null ) continue;
						List<IMyInventoryItem> items = inv.GetItems();
						int j = 0;

						foreach( IMyInventoryItem item in items ) {
							if( item.Content.TypeId == need.Id.TypeId
									&& (need.Id.SubtypeName == String.Empty || need.Id.SubtypeName == item.Content.SubtypeName )
									&& (b.BlockDefinition.TypeId != block.BlockDefinition.TypeId ) // Same type of block
									//&& (!needs.ContainsKey(block) || needs[block] != need ) // Need same thing
									//&& (need.Id != OBTypes.AnyOre || (need.Id == OBTypes.AnyOre && (item.Content.SubtypeName != "Ice" && item.Content.SubtypeName != "Stone" ) ) )
									&& (need.Id != OBTypes.AnyOre || (need.Id == OBTypes.AnyOre && block.BlockDefinition.TypeIdString != "MyObjectBuilder_SurvivalKit" ) )
								) {

								inv.TransferItemTo(inventory, j, null, true, need.Amount, false);
								//if( !fnd )
								if( need.Id != OBTypes.AnyOre && need.Id != OBTypes.Ice ) {
									fulfilled.Add(b);

									//fnd = true;
									break;
								}/* else {
									fnd = true;
								}*/
							}
							j++;
						}

						//if( fnd )
							//fulfilled.Add(b);
					}
				}

				foreach( IMyCubeBlock f in fulfilled ) needs.Remove(f);
			}

			if( ConstructionSite == null ) {
				Job = null;
				return;
			}
			long owner = Owner.MyFaction == null ? (long)0 : Owner.MyFaction.FounderId;
			//ConstructionSite.IncreaseMountLevel(5.0f,Owner.MyFaction.FounderId);

			// if( ConstructionSite.CanContinueBuild(null))
			ConstructionSite.IncreaseMountLevel(5.0f, owner);

			if( ConstructionSite.IsFullIntegrity ) {
				StopProduction( Filter<IMyAssembler>(blocks) );
				ConstructionCompleted();
				FindConstructionSite();
			} else if( Job.Integrity < ConstructionSite.BuildIntegrity ) {
				ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionProcess);
			}

			Job = null;

		}

		private void ConstructionCompleted() {
			ConstructionSite.CubeGrid.UpdateBlockNeighbours(ConstructionSite);
			ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionEnd);
			// CheckFlags();
			bool wasRefinery = Owner.BlockCompleted(ConstructionSite);
			// ConstructionSite.CubeGrid.ChangeGridOwnership(owner, MyOwnershipShareModeEnum.Faction);
			if( wasRefinery && CurrentOrder != null )
				CurrentOrder.Complete();

			foreach( CubeGrid grid in Docked ) {
				if( grid == null ) continue;
				if( grid.ConstructionSite != null ) {
					grid.CheckFlags();
				}
				grid.ConstructionSite = null;
				grid.Balance = null;
				if( wasRefinery && grid.CurrentOrder != null )
					grid.CurrentOrder.Complete();
			}
			Balance = null;

		}

		internal CubeGrid.Item AssessNeed(IMyCubeBlock block) {
			IMyInventory inventory = null;
			List<IMyInventoryItem> items = null;
			if( block is IMyAssembler ) {
				if( block.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					IMyProductionBlock kit = block as IMyProductionBlock;
					inventory = kit.GetInventory(1);
					//if( Convars.Static.ManualKits && (kit.IsQueueEmpty || !kit.IsProducing) )
					if( inventory.CurrentVolume > MyFixedPoint.MultiplySafe(inventory.MaxVolume, 0.94f) ) {
						MyAPIGateway.Utilities.InvokeOnGameThread( () => DiscardComponents(inventory) );
						// DiscardComponents(inventory);
					}
					if( kit.IsQueueEmpty || !kit.IsProducing )
						MyAPIGateway.Utilities.InvokeOnGameThread( () => kit.AddQueueItem( OBTypes.StoneToOre, (VRage.MyFixedPoint)1 ) );
						//kit.AddQueueItem( OBTypes.StoneToOre, (VRage.MyFixedPoint)1 );
					return new CubeGrid.Item {
						Id = OBTypes.Stone,
						Amount = (VRage.MyFixedPoint)500
					};
				}

				IMyAssembler ass = block as IMyAssembler;

				inventory = ass.GetInventory();
				if( inventory.CurrentVolume > MyFixedPoint.MultiplySafe(inventory.MaxVolume, 0.94f) ) {
					MyAPIGateway.Utilities.InvokeOnGameThread( () => DiscardComponents(inventory) );
					// DiscardComponents(inventory);
				}

				inventory = ass.GetInventory(1);
				if( inventory.CurrentVolume > MyFixedPoint.MultiplySafe(inventory.MaxVolume, 0.94f) ) {
					MyAPIGateway.Utilities.InvokeOnGameThread( () => DiscardComponents(inventory) );
					// DiscardComponents(inventory);
				}
				if( ass.IsQueueEmpty ) return null;
				List<MyProductionQueueItem> queue = ass.GetQueue();
				if( queue.Count == 0 ) return null;
				// item.Blueprint.Id.SubtypeName;
				MyBlueprintDefinitionBase bp =	MyDefinitionManager.Static.GetBlueprintDefinition(queue[0].Blueprint.Id);
				inventory = ass.GetInventory(0);
				items = inventory.GetItems();
				items.AddRange(ass.GetInventory(1).GetItems());

				List<MyBlueprintDefinitionBase.Item> prereqs = new List<MyBlueprintDefinitionBase.Item>(bp.Prerequisites);
				prereqs.Reverse(); // Try to pull in reverse order

				foreach( MyBlueprintDefinitionBase.Item prereq in prereqs ) {
					bool fnd = false;
					foreach( IMyInventoryItem item in items ) {

						if( item.Amount >= prereq.Amount && prereq.Id.TypeId == item.Content.TypeId && prereq.Id.SubtypeName == item.Content.SubtypeName ) {
							fnd = true;
							break;
						}
					}
					if( !fnd ) {
							return new CubeGrid.Item(prereq);
					}
				}
			}

			if( block is IMyBatteryBlock ) { // Cheating for now
				MyResourceSinkComponent sink = block.Components.Get<MyResourceSinkComponent>();
				sink.SetInputFromDistributor(OBTypes.Electricity,10f,true,true);
				return null;
			}

			inventory = block.GetInventory();
			if( inventory == null ) return null;
			items = inventory.GetItems();

			if( block is IMyReactor && items.Count == 0 )
				return new CubeGrid.Item {
					Id = OBTypes.Uranium,
					Amount = (MyFixedPoint)1
				};

			if( block is IMyOxygenGenerator && items.Count == 0 )
				return new CubeGrid.Item {
					Id = OBTypes.Ice,
					Amount = (MyFixedPoint)500
				};

			// if( block is IMyGasTank )
			// 	return new CubeGrid.Item {
			// 		Id = OBTypes.Hydrogen,
			// 		Amount = (MyFixedPoint)100
			// 	};

			inventory = block.GetInventory();

			if( block is IMyRefinery ) {
				// Shuffle ores
				if( items.Count > 1 )
					MyAPIGateway.Utilities.InvokeOnGameThread( () => ShuffleOres(inventory) );
					// MyAPIGateway.Utilities.InvokeOnGameThread( () => inventory.TransferItemTo(inventory, 0, inventory.GetItems().Count, true, inventory.GetItems()[0].Amount, false) );
					//inventory.TransferItemTo(inventory, 0, inventory.GetItems().Count, true, inventory.GetItems()[0].Amount, false);
				else if( items.Count == 0 )
					return new CubeGrid.Item {
						Id = OBTypes.AnyOre,
						Amount = (MyFixedPoint)500
					};
			}

			if( block is IMyUserControllableGun ) {

				MyWeaponBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition) as MyWeaponBlockDefinition;
				MyWeaponDefinition weapon =	MyDefinitionManager.Static.GetWeaponDefinition(def.WeaponDefinitionId);
				//IMyUserControllableGun gun = block as IMyUserControllableGun;
				if( items.Count == 0 ) {
					return new CubeGrid.Item {
						Id = weapon.AmmoMagazinesId[0],
						Amount = (MyFixedPoint)1
					};
				}
				return null;
			}

			return null;
		}

		public static void ShuffleOres( IMyInventory inventory ) {
			if( inventory == null ) return;
			List<IMyInventoryItem> items = inventory.GetItems();
			if( items.Count > 1 )
				inventory.TransferItemTo(inventory, 0, items.Count, true, items[0].Amount, false);
		}

		public static void DiscardComponents( IMyInventory inventory ) {
			List<IMyInventoryItem> items = inventory.GetItems();
			for( int j = items.Count-1; j >= 0; j-- ) {
				IMyInventoryItem item = items[j];

				if( item.Content.SubtypeName != "Stone" && item.Content.SubtypeName != "SteelPlate" && item.Content.SubtypeName != "Adanium" && item.Content.SubtypeName != "Organic" && item.Content.SubtypeName != "Iron" )
					continue;

				//if( item.Amount > 10 ) {
					//item.Amount -= 10;
				//} else {
					inventory.RemoveItemsAt(j);
				//}
			}
		}

		public void AllocateResources(IMyCubeBlock block) {
			if( Balance == null || Balance.Count == 0 ) {
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.GetInventory(i);
					if( inv == null ) continue;
					if( ConstructionSite != null ) {
						ConstructionSite.MoveItemsToConstructionStockpile( inv );
					}
				}
			} else {
				// MyAPIGateway.Utilities.ShowNotification( Owner.Name + " Trying to pay balance: " + Balance.Count.ToString() );
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.GetInventory(i);
					if( inv == null ) continue;
					List<IMyInventoryItem> items = inv.GetItems();
					for( int j = items.Count-1; j >= 0; j-- ) {
						IMyInventoryItem item = items[j];
					// foreach( IMyInventoryItem item in items ) {
						string subtypeName = item.Content.SubtypeName;

						if( Balance.ContainsKey(subtypeName) ) {
							//VRage.MyFixedPoint diff = (VRage.MyFixedPoint)Math.Max(item.Amount.ToIntSafe()-Balance[subtypeName],0);
							MyFixedPoint original = (MyFixedPoint)Balance[subtypeName];

							Balance[subtypeName] = (int)Math.Max(Balance[subtypeName]-item.Amount.ToIntSafe(),0);
							if( Balance[subtypeName] <= 0 ) {
								Balance.Remove(subtypeName);
								// if( Balance.Count == 0 )
								// 	Balance = null;
							}

							if( item.Amount > original ) {
								item.Amount -= original;
							} else {
								inv.RemoveItemsAt(j);
							}
							// inv.RemoveItemsOfType(diff, new SerializableDefinitionId(item.Content.TypeId, subtypeName) );
							break;
						}
					}
				}
			}
		}

		public void FindConstructionSite( List<IMySlimBlock> exclude = null, List<IMySlimBlock> blocks = null ) {
			if( DockedTo != null ) {
				DockedTo.FindConstructionSite( exclude );
				return;
			}
			exclude = exclude ?? new List<IMySlimBlock>();
			ConstructionSite = null;
			blocks = blocks ?? GetBlocks<IMySlimBlock>();
			IMySlimBlock best = null;
			int priority = 0;

			foreach( IMySlimBlock block in blocks ) {
				if( block.IsFullIntegrity || exclude.Contains(block) ) continue;

				int p = Prioritize(block);
				if( best == null || p > priority ) {
					best = block;
					priority = p;
				}
			}

			if( best != null ) {
				if( !SetConstructionSite(best) ) {
					exclude.Add(best);
					FindConstructionSite(exclude, blocks);
				}
			}
		}

		public void TransferAllTo( MyInventoryBase from, MyInventoryBase to ) {
			List<MyPhysicalInventoryItem> items = from.GetItems();
			foreach( MyPhysicalInventoryItem item in items ) {
				//from.TransferItemsFrom(to, item, item.Amount);
				to.Add( item, item.Amount );
			}
		}

		public bool AddQueueItems( Dictionary<string,int> components, bool clear = false, IMyAssembler ass = null ) {
			if( components == null ) return true;
			ass = ass ?? GetAssembler();
			if( ass == null ) return false;

			if( clear ) ass.ClearQueue();

			foreach( string component in components.Keys ) {
				MyDefinitionId id = new MyDefinitionId(OBTypes.Component,component);
				MyBlueprintDefinitionBase blueprint = null;
				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(id, out blueprint);
				if( blueprint == null ) {
					blueprint = SpaceCraftSession.GetBlueprintDefinition(id.SubtypeName);

					if( blueprint == null ) {
						if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", "DBP was null:" + id.ToString() );
						return false;
					}
				}
				ass.AddQueueItem( blueprint, (MyFixedPoint)components[component] );
			}

			return true;
		}

		public bool AddQueueItems( Prefab prefab ) {
			if( prefab == null ) return false;
			IMyAssembler ass = GetAssembler();
			bool success = true;
			foreach( MyObjectBuilder_CubeGrid grid in prefab.Definition.CubeGrids ) {
				if( grid == null ) continue;
				foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
					if( !AddQueueItems( MyDefinitionManager.Static.GetCubeBlockDefinition(block), false, ass ) ) {
						success = false;
					}
				}
			}

			return success;
		}

		public bool AddQueueItems( IMyCubeBlock block, bool clear = false, IMyAssembler ass = null ) {
			ass = ass ?? GetAssembler();

			if( ass == null || block == null ) return false;

			if( clear ) ass.ClearQueue();

			MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition);
			return AddQueueItems( def, clear, ass );
		}

		public bool AddQueueItems( MyObjectBuilder_CubeBlock block, bool clear = false, IMyAssembler ass = null ) {
			return AddQueueItems( MyDefinitionManager.Static.GetCubeBlockDefinition(block), clear, ass);
		}

		public bool AddQueueItems( MyCubeBlockDefinition def, bool clear = false, IMyAssembler ass = null ) {
			if( def == null ) {
				if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", "MyCubeBlockDefinition was null" );
				return false;
			}
			ass = ass ?? GetAssembler();
			if( ass == null ) {
				if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", "No assembler found" );
				return false;
			}
			//VRage.Game.MyObjectBuilder_CubeBlockDefinition.Component.CubeBlockComponent

			foreach( var component in def.Components ){
				MyBlueprintDefinitionBase blueprint = null;
				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);

				if( blueprint == null ) {
					// blueprint = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId( new MyDefinitionId(OBTypes.Blueprint,component.Definition.Id.SubtypeName) );
					// if( blueprint == null )
						continue;
				}

				if( !ass.CanUseBlueprint(blueprint) ) {
					if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", "Assembler can not use BP:" + blueprint.ToString() );
					return false;
				}
			}

			foreach( var component in def.Components ){
				MyBlueprintDefinitionBase blueprint = null;
				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);
				if( blueprint == null ) {
					// MyDefinitionId id = new MyDefinitionId(OBTypes.Blueprint,component.Definition.Id.SubtypeName);
					// blueprint = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId( id );
					blueprint = SpaceCraftSession.GetBlueprintDefinition(component.Definition.Id.SubtypeName);

					if( blueprint == null ) {
						if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", "BP was null:" + component.Definition.Id.ToString() );
						return false;
					}
				}
				ass.AddQueueItem( blueprint, component.Count );
			}

			return true;
		}

		public bool AddQueueItem( MyDefinitionBase blueprint, VRage.MyFixedPoint amount ) {
			// InsertQueueItem (int idx, MyDefinitionBase blueprint, MyFixedPoint amount)
			IMyAssembler ass = GetAssembler();
			if( ass != null ) {
				ass.AddQueueItem( blueprint, amount );
				return true;
			}

			return false;
		}

		public void Coalesce( Needs need = Needs.None ) {
			if( need == Needs.None ) need = Need;


		}

		public void AssessNeed() {
			Need = Needs.None;
			if( Grid == null ) return;

			float power = 0.0f; 	 // Power generated
			float stored = 0.0f; 	 // Power stored
			float battery = 0.0f; 	 // Power from batteries
			bool producing = false;
			bool refining = false;
			bool drilling = false;

			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			//MyInventoryBase inv = Grid.GetInventoryBase();

			//MyFixedPoint CurrentVolume 	MaxVolume   CurrentMass  MaxMass     int MaxItemCount

			Grid.GetBlocks( blocks );

			// MyCargoContainerDefinition
			// MyPowerProducerDefinition
			// MyProductionBlockDefinition  StandbyPowerConsumption  OperationalPowerConsumption
			// MySensorBlockDefinition  RequiredPowerInput
			// MyGyroDefinition   RequiredPowerInput
			// MyOxygenFarmDefinition
			// MyBatteryBlockDefinition


			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;

				if( !block.FatBlock.IsFunctional ) {
					Need = Needs.Components;
				}

				if( block.FatBlock is IMyShipDrill ) {
					drilling = true;
				}

				if( block.FatBlock is IMySolarPanel ) {
					power += (block.FatBlock as IMySolarPanel ).CurrentOutput;
				}
				else if( block.FatBlock is IMyBatteryBlock ) {
					battery += (block.FatBlock as IMyBatteryBlock).CurrentOutput;
					power += (block.FatBlock as IMyBatteryBlock).CurrentOutput;
					stored += (block.FatBlock as IMyBatteryBlock).CurrentStoredPower;
				}
				else if( block.FatBlock is IMyReactor ) {
					power += (block.FatBlock as IMyReactor).CurrentOutput;
				}
				else if( block.FatBlock is IMyProductionBlock ) {
					power -= (block.FatBlock as IMyProductionBlock).IsProducing ?
											((MyProductionBlockDefinition)block.BlockDefinition).OperationalPowerConsumption :
											((MyProductionBlockDefinition)block.BlockDefinition).StandbyPowerConsumption;
				}

				if( block.FatBlock is IMyAssembler )
					producing = true;

				if( block.FatBlock is IMyRefinery )
					refining = true;


				bool scav = Owner.CommandLine.Switch("scavenger");
				if( power < 0 ) {
					Need = Needs.Power;
				} else if( !producing && !scav ) {
					Need = Needs.Production;
				} else if( !producing && !scav ) {
					Need = Needs.Refinery;
				} else if( !drilling && !scav ) {
					Need = Needs.Drills;
				// } else if( inv.CurrentMass / inv.MaxMass > .9 ) {
				// 	Need = Needs.Storage;
				} else if( battery == power ) {
					Need = Needs.Power;
				}

			}
		}

		public IMyCubeBlock GetRespawnBlock() {
			if( Grid == null ) return null;
			// List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			// Grid.GetBlocks( blocks );
			List<IMySlimBlock> blocks = blocks = GetBlocks<IMySlimBlock>();
			foreach( IMySlimBlock slim in blocks ) {
				if( slim.FatBlock == null ) continue;
				string subtype = slim.FatBlock.BlockDefinition.SubtypeName;
				if( slim.FatBlock is IMyMedicalRoom || slim.FatBlock.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit"
				 	|| subtype == "ProtossSurvivalKit" || subtype == "ProtossSurvivalKitLarge" || subtype == "ZergSurvivalKit") {
					return slim.FatBlock;
				}
			}
			return null;
		}

		public IMySlimBlock TryPlace( MyObjectBuilder_CubeBlock block ) {
			if( block == null || Grid == null ) return null;
			if( block.Min.X == 0 && block.Min.Y == 0 && block.Min.Z == 0 ) {
				Vector3I pos = Vector3I.Zero;
				MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition( block );
				FindOpenSlot(out pos, def.Size, def.CubeSize );
				block.Min = pos;
			}

			IMySlimBlock slim = Grid.AddBlock( block, false );

			if( slim != null && block.BuildPercent == 0.0f ) {
				slim.SetToConstructionSite();
			}

			return slim;
		}

		public bool FindOpenSlot( out Vector3I slot, Vector3I size, MyCubeSize gridSize = MyCubeSize.Large ) {
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			slot = Vector3I.Zero;
			Grid.GetBlocks( blocks );

			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;

				if( block.CubeGrid.GridSizeEnum == gridSize ) {
					//MyObjectBuilder_CubeBlockDefinition
					//SerializableDefinitionId def = block.FatBlock.BlockDefinition;
					MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.FatBlock.BlockDefinition);
					foreach( MyCubeBlockDefinition.MountPoint point in def.MountPoints ) {
						//IMySlimBlock hit = Grid.GetCubeBlock( point.Normal + block.Position );
						IMySlimBlock hit = Grid.GetCubeBlock( (point.Normal*size) + block.Position );

						if( hit == null ) {
							//slot = point.Normal + block.Position;
							slot = (point.Normal*size) + block.Position;
							return true;
						}
					}
				}
			}

			return false;
		}

		public IMyAssembler GetAssembler( List<IMySlimBlock> blocks = null ) {
			blocks = blocks ?? GetBlocks<IMyAssembler>();
			IMyAssembler best = null;
			int priority = 0;

			foreach( IMySlimBlock block in blocks ) {
				if( !block.FatBlock.IsFunctional || !(block.FatBlock is IMyAssembler ) ) continue;

				int p = Prioritize( block );
				if( best == null || p > priority ) {
					best = block.FatBlock as IMyAssembler;
					priority = p;
				}
			}

			return best;
		}

		public static IMyCubeGrid Spawn(Prefab prefab, MatrixD matrix, Faction owner) {
			return Spawn( prefab.Definition, matrix, owner );
		}

		public static IMyCubeGrid Spawn(string prefabName, MatrixD matrix, Faction owner) {
			MyPrefabDefinition prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);
			return Spawn( prefab, matrix, owner );
		}


		// https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/World/MyPrefabManager.cs
		public static IMyCubeGrid Spawn(MyPrefabDefinition prefab, MatrixD matrix, Faction owner) {
			if( prefab == null ) return null;

      IMyCubeGrid g = null;
			List<IMyCubeGrid> subgrids = new List<IMyCubeGrid>();
			MatrixD original = matrix;

			// MatrixD translateToOriginMatrix = prefab.CubeGrids[0].PositionAndOrientation.HasValue ? MatrixD.CreateWorld(-(Vector3D)prefab.CubeGrids[0].PositionAndOrientation.Value.Position, Vector3D.Forward, Vector3D.Up) : MatrixD.CreateWorld(-prefab.BoundingSphere.Center, Vector3D.Forward, Vector3D.Up);

			// MyObjectBuilder_CubeGrid[] grids = prefab.CubeGrids;
			//
			// if (grids.Length == 0) return null;
			//
			// for (int i = 0; i < grids.Length; i++)
      // {
      //     grids[i] = (MyObjectBuilder_CubeGrid)prefab.CubeGrids[i].Clone();
      // }
			//
			// MyAPIGateway.Entities.RemapObjectBuilderCollection(grids);
			long ownerId = owner != null && owner.MyFaction != null ? owner.MyFaction.FounderId : 0;
      foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
				//MyPositionAndOrientation po = grid.PositionAndOrientation ?? new MyPositionAndOrientation(ref matrix);
				// MatrixD originalGridMatrix = grid.PositionAndOrientation.HasValue ? grid.PositionAndOrientation.Value.GetMatrix() : MatrixD.Identity;
				// MatrixD newWorldMatrix = MatrixD.Multiply(originalGridMatrix, MatrixD.Multiply(translateToOriginMatrix, matrix));
				// if( prefab.SubtypeId ) {
				// 	grid.Name = owner.Name + " Grid" + NumGrids.ToString();
				// 	grid.DisplayName = owner.Name + " Grid" + NumGrids.ToString();
				// 	NumGrids++;
				// } else {
					grid.Name = owner.Name + " " + prefab.Id.SubtypeName;
					grid.DisplayName = owner.Name + " " + prefab.Id.SubtypeName;
				//}
				grid.EntityId = (long)0;

				// if( g == null ) {
				// 	original = po.GetMatrix();
					//grid.PositionAndOrientation = new MyPositionAndOrientation(ref matrix);
				// } else {
				// 	MatrixD offset = matrix + (po.GetMatrix() - original);
				// 	//Quaternion rotation = Quaternion.CreateFromRotationMatrix(original);
				// 	//Quaternion placement = Quaternion.CreateFromRotationMatrix(matrix);
				// 	// original.Translation = matrix.Translation;
				// 	// MatrixD diff = original - matrix;
				// 	// original = matrix + diff;
				// 	//original = matrix + MatrixD.CreateFromQuaternion(rotation-placement);
				//
				// 	grid.PositionAndOrientation = new MyPositionAndOrientation(ref offset);
				// }


				foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
					block.EntityId = (long)0;
					block.Owner = ownerId;
					block.BuiltBy = ownerId;
					block.ShareMode = MyOwnershipShareModeEnum.Faction;
					if( block.ColorMaskHSV == DefaultColor )
						block.ColorMaskHSV = owner.Color;
					//block.Min = new Vector3I(Vector3D.Transform(new Vector3D(block.Min), matrix))	;
				}
        MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

        if( entity == null ) {
          return null;
        }

				// MyCubeGrid cg = entity as MyCubeGrid;
				// if( cg != null ) {
				// 	cg.IsRespawnGrid = false;
				// }
				//MySpaceRespawnComponent.Static.

				entity.Storage = new MyModStorageComponent();
				 // MES
				entity.Storage.Add(SpaceCraftSession.GuidSpawnType,"true");
				entity.Storage.Add(SpaceCraftSession.GuidIgnoreCleanup,"true");
				// entity.Storage.Add(GuidStartCoords,matrix.Translation.ToString());
				// entity.Storage.Add(GuidEndCoords,matrix.Translation.ToString());

        entity.Flags &= ~EntityFlags.Save;
				entity.Flags &= ~EntityFlags.Sync;
				entity.Save = true;

				//entity.Render.NearFlag = false;

				entity.Render.Visible = true;
        entity.WorldMatrix = matrix;
				// entity.WorldMatrix = newWorldMatrix;

        MyAPIGateway.Entities.AddEntity(entity);


				if( g == null ) {
	        g = entity as IMyCubeGrid;
					if( g == null ) continue;

					g.ChangeGridOwnership(ownerId, MyOwnershipShareModeEnum.Faction);



					// Double check them really needed?
					// List<IMySlimBlock> blocks = new List<IMySlimBlock>();
					// g.GetBlocks(blocks);
					// foreach(IMySlimBlock slim in blocks ) {
					//
					// 	MyCubeBlock block = slim.FatBlock as MyCubeBlock;
					//
					// 	if( block == null ) continue;
					// 	// block.ChangeOwner(0, MyOwnershipShareModeEnum.Faction);
					// 	// block.ChangeBlockOwnerRequest(ownerId, MyOwnershipShareModeEnum.Faction);
					// 	//block.ChangeOwner(ownerId, MyOwnershipShareModeEnum.Faction);
					// }


				} else {
					(entity as IMyCubeGrid).ChangeGridOwnership(ownerId, MyOwnershipShareModeEnum.Faction);
					subgrids.Add(entity as IMyCubeGrid);
				}
      }

      return g;
		}



		public static IMyCubeGrid Spawn(MyObjectBuilder_CubeGrid grid, MatrixD matrix, Faction owner) {
			/*if( String.IsNullOrWhiteSpace(grid.Name) ) {
				grid.Name =
			}*/
			//grid.Name = "StarCraft Grid" + NumGrids.ToString();
			//grid.DisplayName = "StarCraft Grid" + NumGrids.ToString();
			//NumGrids++;
			long ownerId = owner != null && owner.MyFaction != null ? owner.MyFaction.FounderId : 0;
			grid.PositionAndOrientation = new MyPositionAndOrientation(ref matrix);

			foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
				block.Owner = ownerId;
				block.BuiltBy = ownerId;
			}
			MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);
			IMyCubeGrid g = null;

			if( entity != null ) {
				entity.Flags &= ~EntityFlags.Save;
        entity.Render.Visible = true;
				entity.Save = true;

				entity.Storage = new MyModStorageComponent();
				entity.Storage.Add(SpaceCraftSession.GuidFaction,owner.Name);
        //entity.WorldMatrix = matrix;
        MyAPIGateway.Entities.AddEntity(entity);

        g = entity as IMyCubeGrid;
				g.ChangeGridOwnership(ownerId, MyOwnershipShareModeEnum.Faction);
			}

			return g;
		}

		public bool AddLargeGridConverter( bool skipRefinery = true ) {
			if( Grid == null || Owner == null ) return false;
			IMyCubeGrid grid;
			IMySlimBlock slim = TryPlace( new MyObjectBuilder_MotorAdvancedRotor{
				SubtypeName = "SmallAdvancedRotor",
				Orientation =  Quaternion.CreateFromForwardUp(Vector3.Left, Vector3.Backward),
				Min = new Vector3I(-1,-1,-3),
				BuildPercent = 0.0f,
				ConstructionInventory = new MyObjectBuilder_Inventory(),
				Owner = Owner.MyFaction.FounderId,
				BuiltBy = Owner.MyFaction.FounderId,
				ShareMode = MyOwnershipShareModeEnum.Faction
			} );

			if( slim == null ) {
				return false;
			} else {
				slim.SetToConstructionSite();
				grid = Spawn( new MyObjectBuilder_CubeGrid {
					Name = Owner.Name + " Converter",
					DisplayName = Owner.Name + " Converter",
					GridSizeEnum = MyCubeSize.Large,
					CubeBlocks = new List<MyObjectBuilder_CubeBlock> {
						new MyObjectBuilder_MotorAdvancedStator{
							SubtypeName = "LargeAdvancedStator",
							Orientation =  Quaternion.CreateFromForwardUp(Vector3.Left, Vector3.Backward),
							BuildPercent = 0.0f,
							ConstructionInventory = new MyObjectBuilder_Inventory(),
							Owner = Owner.MyFaction.FounderId,
							BuiltBy = Owner.MyFaction.FounderId,
							ShareMode = MyOwnershipShareModeEnum.Faction
						}
					}
				}, slim.FatBlock.WorldMatrix, Owner );

				if( grid == null ) {
					return false;
				} else {
					MyVisualScriptLogicProvider.SetName(grid.EntityId, grid.EntityId.ToString());
					IMyMotorAdvancedStator stator = grid.GetCubeBlock( Vector3I.Zero ).FatBlock as IMyMotorAdvancedStator;
					if( stator == null ) {
						return false;
					} else {
						stator.Attach();
						stator.SlimBlock.SetToConstructionSite();

						Block.DoAction( stator as IMyTerminalBlock, "Share inertia tensor On/Off" );

						//Block.DoAction( stator as IMyTerminalBlock, "Safety lock override On/Off" ); // Doesn't work
						//Block.DoAction( stator as IMyTerminalBlock, "Toggle block On/Off" );

						//stator.SetValue("RotorLock", true);
						//Block.ListProperties( stator as IMyTerminalBlock );

						if( !skipRefinery ) {
							slim = grid.AddBlock( new MyObjectBuilder_Refinery{
								SubtypeName = "Blast Furnace",
								Min = new Vector3I(0,-1,-1),
								Orientation =  Quaternion.CreateFromForwardUp(Vector3.Forward, Vector3.Down),
								BuildPercent = 0.0f,
								ConstructionInventory = new MyObjectBuilder_Inventory(),
								Owner = Owner.MyFaction.FounderId,
								BuiltBy = Owner.MyFaction.FounderId,
								ShareMode = MyOwnershipShareModeEnum.Faction
							}, false );

							if( slim == null ) {
								return false;
							} else {
								slim.SetToConstructionSite();
							}
						}

						slim = grid.AddBlock( new MyObjectBuilder_Assembler{
							SubtypeName = "BasicAssembler",
							Min = new Vector3I(0,0,skipRefinery ? -1 : -2),
							Orientation =  Quaternion.CreateFromForwardUp(Vector3.Backward, Vector3.Right),
							BuildPercent = 0.0f,
							ConstructionInventory = new MyObjectBuilder_Inventory(),
							Owner = Owner.MyFaction.FounderId,
							BuiltBy = Owner.MyFaction.FounderId,
							ShareMode = MyOwnershipShareModeEnum.Faction
						}, false );

						if( slim == null ) {
							return false;
						} else {
							slim.SetToConstructionSite();
							SetConstructionSite( slim );
						}


					}

				}

			}

			SuperGrid = grid;
			return true;
		}

		public bool SetConstructionSite( IMySlimBlock block ) {
			if( !AddQueueItems( block.FatBlock, true ) ) {
				// if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "SetConstructionSite", "Failed to add to queue: " + block.FatBlock.ToString() );
				return false;
			}
			ConstructionSite = block;
			Need = Needs.Components;

			if( Docked.Count > 0 ) {
				CubeGrid grid = Owner.GetControllable(block.CubeGrid) as CubeGrid;
				if( grid != null ) {
					grid.ConstructionSite = block;
				}
			}

			block.SetToConstructionSite();
			block.SpawnFirstItemInConstructionStockpile();
			//block.ClearConstructionStockpile(null);
			block.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionBegin);
			return true;
		}

		public void SetToConstructionSite( List<IMySlimBlock> blocks = null ) {
			if( blocks == null ) blocks = GetBlocks<IMySlimBlock>();

			List<IMySlimBlock> subblocks = new List<IMySlimBlock>();

			IMyBatteryBlock battery = null;

			foreach( IMySlimBlock block in blocks ) {
				// Balance paid for this battery
				if( battery == null && block.FatBlock is IMyBatteryBlock ) {
					battery = block.FatBlock as IMyBatteryBlock;
					continue;
				}
				block.SetToConstructionSite();
				block.IncreaseMountLevel( 0f, (long)0 );
				if( block.FatBlock is IMyMotorSuspension ) {
					IMyMotorSuspension suspension = block.FatBlock as IMyMotorSuspension;
					Block.DoAction(block.FatBlock as IMyTerminalBlock, "Add Wheel");

					if( suspension.RotorGrid != null ) {
						suspension.RotorGrid.DisplayName = Owner.Name + " Subgrid";
						AddSubgrid(suspension.RotorGrid);
						// Subgrids.Add(suspension.RotorGrid);
					}
				}



				if( block.FatBlock is IMyMotorStator ) {
					IMyMotorStator stator = block.FatBlock as IMyMotorStator;
					stator.Attach();
					if( stator.RotorGrid != null ) {
						// Subgrids.Add(stator.RotorGrid);
						AddSubgrid(stator.RotorGrid);
						stator.RotorGrid.GetBlocks(subblocks);
					}
				}
			}

			if( subblocks.Count > 0 )
				SetToConstructionSite(subblocks);
			else
				FindConstructionSite();
		}

		public void AddSubgrid( IMyCubeGrid grid ) {
			if( grid == null ) return;
			grid.Storage = grid.Storage ?? new MyModStorageComponent();
			grid.Storage.Add(SpaceCraftSession.GuidFaction,Owner.Name);
			Subgrids.Add(grid);
		}

		protected void StopProduction( List<IMySlimBlock> factories ) {
			factories = factories ?? GetBlocks<IMyAssembler>();

			foreach( IMySlimBlock factory in factories ) {
				IMyAssembler ass = factory.FatBlock as IMyAssembler;
				ass.ClearQueue();
			}
		}

		public void FindSubgrids() {
			List<IMySlimBlock> motors = GetBlocks<IMyMotorSuspension>();
			foreach( IMySlimBlock slim in motors ) {
				IMyMotorSuspension motor = slim.FatBlock as IMyMotorSuspension;
				if( motor.RotorGrid != null ) {
					// Subgrids.Add(motor.RotorGrid);
					AddSubgrid(motor.RotorGrid);
					motor.RotorGrid.ChangeGridOwnership(Owner.MyFaction.FounderId, MyOwnershipShareModeEnum.Faction);
					//motor.RotorGrid.UpdateOwnership(Owner.MyFaction.FounderId, true);
				}
			}
		}


		// Obsolete: This was the original proof of concept
		public void AssessInventory( List<IMyInventory> inventories = null ) {
			inventories = inventories ?? new List<IMyInventory>();
			if( ConstructionSite != null && ConstructionSite.FatBlock != null && (ConstructionSite.FatBlock.MarkedForClose || ConstructionSite.FatBlock.Closed) ) {
				FindConstructionSite();
			}
			//if( ConstructionSite == null || ConstructionSite.FatBlock == null ) return;
			// TODO: New inventory needs system
			//Dictionary<IMyInventory,MyDefinitionId> needs = new Dictionary<IMyInventory,MyDefinitionId>();
			float old = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity;
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			List<IMyAssembler> factories = new List<IMyAssembler>();
			List<IMyProductionBlock> refineries = new List<IMyProductionBlock>();
			// List<IMyOxygenTank> tanks = new List<IMyOxygenTank>();
			List<IMyGasTank> tanks = new List<IMyGasTank>();

			// Update Construction Site
			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				if( block.FatBlock is IMyAssembler ) {
					factories.Add( block.FatBlock as IMyAssembler );
				}
				if( block.FatBlock is IMyRefinery )
					refineries.Add( block.FatBlock as IMyProductionBlock );
				if( block.FatBlock is IMyGasTank )
					tanks.Add( block.FatBlock as IMyGasTank );

				if( Balance == null || Balance.Count == 0 ) {
					for( int i = 0; i < 2; i++ ) {
						IMyInventory inv = block.FatBlock.GetInventory(i);
						if( inv == null ) continue;
						inventories.Add( inv );
						if( ConstructionSite != null )
							ConstructionSite.MoveItemsToConstructionStockpile( inv );
					}
				} else {
					for( int i = 0; i < 2; i++ ) {
						IMyInventory inv = block.FatBlock.GetInventory(i);
						if( inv == null ) continue;
						List<IMyInventoryItem> items = inv.GetItems();
						for( int j = items.Count; j >= 0; j-- ) {
							IMyInventoryItem item = items[j];
						//foreach( IMyInventoryItem item in items ) {
							string subtypeName = item.Content.SubtypeName;

							if( Balance.ContainsKey(subtypeName) ) {
								VRage.MyFixedPoint diff = (VRage.MyFixedPoint)Math.Max(item.Amount.ToIntSafe()-Balance[subtypeName],0);

								Balance[subtypeName] = (int)Math.Max(Balance[subtypeName]-item.Amount.ToIntSafe(),0);
								if( Balance[subtypeName] <= 0 ) {
									Balance.Remove(subtypeName);
									// if( Balance.Count == 0 )
									// 	Balance = null;
								}
								inv.RemoveItemsOfType(diff, new SerializableDefinitionId(item.Content.TypeId, subtypeName) );
								break;
							}
						}
					}
				}

			}

			// Balance tanks
			foreach( IMyGasTank filling in tanks ) {
				if( filling.FilledRatio == 1f ) continue;


				// if( source.RemainingCapacity == 0f ) continue;

				MyResourceSinkComponent sink = filling.Components.Get<MyResourceSinkComponent>();

				foreach( IMyGasTank tank in tanks ) {
					if( tank == filling || tank.FilledRatio == 0f ) continue;
					MyResourceSourceComponent source = tank.Components.Get<MyResourceSourceComponent>();
					//ConnectBlocks(filling, tank);
					//if( tank.FilledRatio > filling.FilledRatio && source.ProductionEnabledByType(OBTypes.Hydrogen) ) {
					if( tank.FilledRatio > filling.FilledRatio ) {
						float output = source.MaxOutputByType(OBTypes.Hydrogen);
						sink.SetInputFromDistributor(OBTypes.Hydrogen,output,true,true);
						//source.SetRemainingCapacityByType(OBTypes.Hydrogen, (float)((tank.FilledRatio*tank.Capacity)-output));
						sink.Update();
					}

				}

			}

			// Pull Ore
			foreach( IMyProductionBlock refinery in refineries ) {
				//if( refinery.IsProducing ) continue;

				//IMyInventory inventory = refinery is IMyAssembler ? refinery.GetInventory(1) : refinery.GetInventory(0);
				IMyInventory inventory = refinery.GetInventory(0);

				foreach( IMyInventory inv in inventories ) {
					bool found = false;
					if( inv == inventory || inv == null ) continue;

					List<IMyInventoryItem> itms = inv.GetItems();
					for( int i = 0; i < itms.Count; i++ ) {
						IMyInventoryItem itm = itms[i];
						if( itm.Content.TypeId == OBTypes.Ore ) {
							if( inv.TransferItemTo(inventory, i, null, true, (VRage.MyFixedPoint)100, false) ) {
								found = true;
								break;
							}
						}
					}
					if( found ) break;
				}
			}

			IMyAssembler main = GetAssembler();
			if( main == null ) return;

			// Pull Components
			foreach( IMyAssembler factory in factories ) {
				if( factory == null || !factory.IsFunctional ) continue;
				//string SubtypeName = factory.SlimBlock.BlockDefinition.Id.SubtypeName;
				// List<MyProductionQueueItem> queue;
				List<MyProductionQueueItem> queue = main.GetQueue();

				if( factory != main && factory.IsQueueEmpty && queue.Count > 0 ) {
					// Cooperative Mode
					if( factory.CanUseBlueprint(queue[0].Blueprint) ) {
						main.RemoveQueueItem(0, (VRage.MyFixedPoint)1);
						factory.AddQueueItem(queue[0].Blueprint, (VRage.MyFixedPoint)1);
					}
				}

				queue = factory.GetQueue();
				if( queue.Count == 0 ) continue;
				MyProductionQueueItem item = queue[0];
				// item.Blueprint.Id.SubtypeName;
				MyBlueprintDefinitionBase bp =	MyDefinitionManager.Static.GetBlueprintDefinition(item.Blueprint.Id);
				IMyInventory inventory = factory.GetInventory(0);

				List<MyBlueprintDefinitionBase.Item> needs = new List<MyBlueprintDefinitionBase.Item>();
				List<IMyInventoryItem> items = inventory.GetItems();


				foreach( MyBlueprintDefinitionBase.Item prereq in bp.Prerequisites ) {
					bool fnd = false;
					foreach( IMyInventoryItem i in items ) {

						if( i.Amount >= prereq.Amount && prereq.Id.TypeId == i.Content.TypeId && prereq.Id.SubtypeName == i.Content.SubtypeName ) {
							fnd = true;
						}
					}
					if( !fnd )
						needs.Add(prereq);
				}

				if( factory.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					needs.Add( OBTypes.StoneBP.Prerequisites[0] );
				}

				if( needs.Count == 0 ) continue;

				foreach( MyBlueprintDefinitionBase.Item need in needs ) {
					bool found = false;

					foreach( IMyInventory inv in inventories ) {
						if( inv == inventory ) continue;

						List<IMyInventoryItem> itms = inv.GetItems();
						for( int i = 0; i < itms.Count; i++ ) {
							IMyInventoryItem itm = itms[i];
							if( need.Id.TypeId == itm.Content.TypeId && need.Id.SubtypeName == itm.Content.SubtypeName ) {
								// Transfer
								found = true;
								inv.TransferItemTo(inventory, i, null, true, need.Amount, false);
								break;
							}
						}

						if( found ) break; // Inventories
					}
				}

			} // End pull components


			if( ConstructionSite != null ) {
				ConstructionSite.IncreaseMountLevel(1.0f,(long)0);


				if( ConstructionSite.IsFullIntegrity ) {
					ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionEnd);
					StopProduction( Filter<IMyAssembler>(blocks) );
					CheckFlags();
					Owner.BlockCompleted(ConstructionSite);
					if( CurrentOrder != null )
		        CurrentOrder.Complete();
					FindConstructionSite();
					foreach( CubeGrid grid in Docked ) {
						grid.ConstructionSite = ConstructionSite;
						if( grid.CurrentOrder != null )
			        grid.CurrentOrder.Complete();
					}
				} else if( old < ConstructionSite.BuildIntegrity ) {
					ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionProcess);
				}
			}
		}


	}

}
