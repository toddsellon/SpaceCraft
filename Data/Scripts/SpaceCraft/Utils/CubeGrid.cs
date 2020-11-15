using System;
using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Definitions;
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

		internal class Item {
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

		private static SerializableVector3 DefaultColor = new SerializableVector3(0.575f,0.150000036f,0.199999958f);

		public IMySlimBlock ConstructionSite;
		public Needs Need = Needs.None;
    public IMyCubeGrid Grid;
		public List<IMyCubeGrid> Subgrids = new List<IMyCubeGrid>();
		public string Prefab;
		public IMyCubeGrid SuperGrid;
		protected static int NumGrids = 0;
		public int Tick = 0;
		public IMyRemoteControl Remote;
		public Dictionary<string,int> Balance = null;

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

			Entity = Grid;
			if( grid != null )
				CheckFlags();
		}

		// Main loop
		public override void UpdateBeforeSimulation() {
			if( Grid == null ) return;

			Tick++;
			CheckOrder();
			if( DockedTo != null && ConstructionSite != null ) {
				Grid.Physics.ClearSpeed();
			}

			if( Tick == 99 ) {


				//AssessInventory();
				if( DockedTo == null )
					UpdateInventory();
				// if( Grid.IsStatic )
				// 	Drill();
				Tick = 0;
			}
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
			// This is not working ATM
			// if( PercentFull > .9) {
			// 	if( !IsStatic )
			// 		Execute( new Order{
			// 			Type = Orders.Deposit,
			// 			Range = 50f,
			// 			Entity = Owner.GetBestRefinery(this)
			// 		}, true );
			// 	return;
			// }
			List<IMySlimBlock> drills = GetBlocks<IMyShipDrill>();
			foreach( IMySlimBlock slim in drills ) {
				if( !slim.FatBlock.IsFunctional ) continue;
				IMyInventory inv = slim.FatBlock.GetInventory(0);
				foreach( string ore in CurrentOrder.Resources.Keys ) {
					inv.AddItems(CurrentOrder.Resources[ore]*100, new MyObjectBuilder_Ore(){
						SubtypeName = ore
					} );
				}
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

			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.FatBlock.GetInventory(i);
					if( inv != null )
						list.Add( inv );
				}
			}

      return list;
    }

		public void CheckFlags() {
			Flying = false;
			Spacecraft = false;
			Wheels = GetBlocks<IMyMotorSuspension>(null,true).Count > 0;
	    Drills = GetBlocks<IMyShipDrill>(null,true).Count > 0;
	    //Welders = GetBlocks<IMyShipWelder>().Count > 0;
	    //Griders = GetBlocks<IMyShipGrinder>().Count > 0;
			List<IMySlimBlock> blocks = GetBlocks<IMyAssembler>(null,true);
			foreach( IMySlimBlock block in blocks ) {
				if( !block.FatBlock.IsFunctional ) continue;
				if( block.FatBlock.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					FactoryTier = (uint)Math.Max(FactoryTier,1);
					RefineryTier = (uint)Math.Max(RefineryTier,1);
				} else {
					FactoryTier = (uint)Math.Max(FactoryTier,block.FatBlock.BlockDefinition.SubtypeId == "LargeAssembler" ? 3 : 2);
				}
			}
			blocks = GetBlocks<IMyRefinery>(null,true);
			foreach( IMySlimBlock block in blocks ) {
				if( !block.FatBlock.IsFunctional ) continue;
				RefineryTier = (uint)Math.Max(RefineryTier,block.FatBlock.BlockDefinition.SubtypeId == "LargeRefinery" ? 3 : 2);
			}
			//IMySmallMissileLauncher IMyUserControllableGun
			if( Drills)
				Fighter = false;
			else
				Fighter = GetBlocks<IMyUserControllableGun>(null,true).Count > 0 || GetBlocks<IMySmallGatlingGun>(null,true).Count > 0 || GetBlocks<IMySmallMissileLauncher>(null,true).Count > 0;
			List<IMySlimBlock> thrusters = GetBlocks<IMyThrust>(null,true);
			foreach( IMySlimBlock block in thrusters ) {
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

		public List<IMySlimBlock> GetBlocks<t>( List<IMySlimBlock> blocks = null, bool excludeDocked = false ) {
			List<IMySlimBlock> list = new List<IMySlimBlock>();
			Grid.GetBlocks( list );


			if( SuperGrid != null ) {
				SuperGrid.GetBlocks( list );
			}

			foreach( IMyCubeGrid grid in Subgrids ) {
				grid.GetBlocks( list );
			}

			if( !excludeDocked ) {
				foreach( CubeGrid grid in Docked ) {
					grid.Grid.GetBlocks( list );
					foreach( IMyCubeGrid sub in grid.Subgrids ) {
						sub.GetBlocks( list );
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

		public void UpdateInventory() {
			// if( ConstructionSite != null && ConstructionSite.FatBlock != null && (ConstructionSite.FatBlock.MarkedForClose || ConstructionSite.FatBlock.Closed) ) {
			// 	FindConstructionSite();
			// }

			float old = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity;
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			IMyAssembler main = GetAssembler();
			Dictionary<IMyCubeBlock,CubeGrid.Item> needs = new Dictionary<IMyCubeBlock,CubeGrid.Item>();
			CubeGrid.Item bp = null;
			// Assess needs
			foreach( IMySlimBlock slim in blocks ) {
				IMyCubeBlock block = slim.FatBlock;
				if( block == null || !block.IsFunctional ) continue;
				CubeGrid.Item need = AssessNeed(block);
				if( need != null ) {
					if( need.Id.TypeId == OBTypes.Magazine )
						bp = need;

					needs.Add(block,need);
				}
				else if( block is IMyAssembler && block != main ) {
					IMyAssembler factory = block as IMyAssembler;
					//string SubtypeName = factory.SlimBlock.BlockDefinition.Id.SubtypeName;
					// List<MyProductionQueueItem> queue;
					List<MyProductionQueueItem> queue = main.GetQueue();

					if( factory.IsQueueEmpty && queue.Count > 0 ) {
						// Cooperative Mode
						if( factory.CanUseBlueprint(queue[0].Blueprint) ) {
							main.RemoveQueueItem(0, (VRage.MyFixedPoint)1);
							factory.AddQueueItem(queue[0].Blueprint, (VRage.MyFixedPoint)1);
						}
					}
				}

				AllocateResources(block);
			}

			// See if ammo needs queue
			if( bp != null ) {

				MyBlueprintDefinitionBase bpd =	MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(bp.Id);
				if( bpd != null ) {

					List<MyProductionQueueItem> queue = main.GetQueue();
					if( queue.Count == 0 || queue[0].Blueprint != bpd )
						main.InsertQueueItem(0,bpd, bp.Amount);
						//main.AddQueueItem(bpd, bp.Amount);
				}
			}

			// Fulfill needs
			foreach( IMySlimBlock slim in blocks ) {
				IMyCubeBlock block = slim.FatBlock;
				if( block == null || !block.IsFunctional ) continue;

				List<IMyCubeBlock> fulfilled = new List<IMyCubeBlock>();
				foreach( IMyCubeBlock b in needs.Keys ) {
					if( block == b ) continue;
					IMyInventory inventory = b.GetInventory();
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
									&& (!needs.ContainsKey(block) || needs[block] != need ) // Need same thing
									&& (need.Id != OBTypes.AnyOre || (need.Id == OBTypes.AnyOre && (item.Content.SubtypeName != "Ice" && item.Content.SubtypeName != "Stone" ) ) ) ) {

								inv.TransferItemTo(inventory, j, null, true, need.Amount, false);
								//if( !fnd )
								if( need.Id != OBTypes.AnyOre ) {
									fulfilled.Add(b);

									//fnd = true;
									break;
								}
							}
							j++;
						}
					}
				}

				foreach( IMyCubeBlock f in fulfilled ) needs.Remove(f);
			}

			if( ConstructionSite == null ) return;
			ConstructionSite.IncreaseMountLevel(5.0f,Owner.MyFaction.FounderId);

			if( ConstructionSite.IsFullIntegrity ) {
				ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionEnd);
				StopProduction();
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

		internal CubeGrid.Item AssessNeed(IMyCubeBlock block) {
			IMyInventory inventory = null;
			if( block is IMyAssembler ) {
				if( block.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" )
					return new CubeGrid.Item {
						Id = OBTypes.Stone,
						Amount = (VRage.MyFixedPoint)500
					};

				IMyAssembler ass = block as IMyAssembler;
				if( ass.IsQueueEmpty ) return null;
				List<MyProductionQueueItem> queue = ass.GetQueue();
				if( queue.Count == 0 ) return null;
				// item.Blueprint.Id.SubtypeName;
				MyBlueprintDefinitionBase bp =	MyDefinitionManager.Static.GetBlueprintDefinition(queue[0].Blueprint.Id);
				inventory = ass.GetInventory(0);
				List<IMyInventoryItem> items = inventory.GetItems();
				items.AddRange(ass.GetInventory(1).GetItems());

				// Not pulling all
				foreach( MyBlueprintDefinitionBase.Item prereq in bp.Prerequisites ) {
					bool fnd = false;
					foreach( IMyInventoryItem item in items ) {

						if( item.Amount >= prereq.Amount && prereq.Id.TypeId == item.Content.TypeId && prereq.Id.SubtypeName == item.Content.SubtypeName ) {
							fnd = true;
							break;
						}
					}
					if( !fnd )
						return new CubeGrid.Item(prereq);
				}
			}

			if( block is IMyOxygenGenerator )
				return new CubeGrid.Item {
					Id = OBTypes.Ice,
					Amount = (MyFixedPoint)100
				};

			// if( block is IMyGasTank )
			// 	return new CubeGrid.Item {
			// 		Id = OBTypes.Hydrogen,
			// 		Amount = (MyFixedPoint)100
			// 	};

			inventory = block.GetInventory();

			if( block is IMyRefinery ) {
				// Shuffle ores
				if( inventory.GetItems().Count > 1 )
					inventory.TransferItemTo(inventory, 0, inventory.GetItems().Count, true, inventory.GetItems()[0].Amount, false);
				return new CubeGrid.Item {
					Id = OBTypes.AnyOre,
					Amount = (MyFixedPoint)500
				};
			}

			if( block is IMyUserControllableGun ) {

				MyWeaponBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition) as MyWeaponBlockDefinition;
				MyWeaponDefinition weapon =	MyDefinitionManager.Static.GetWeaponDefinition(def.WeaponDefinitionId);
				//IMyUserControllableGun gun = block as IMyUserControllableGun;
				if( inventory.GetItems().Count == 0 ) {
					return new CubeGrid.Item {
						Id = weapon.AmmoMagazinesId[0],
						Amount = (MyFixedPoint)1
					};
				}
				return null;
			}

			return null;
		}

		public void AllocateResources(IMyCubeBlock block) {
			if( Balance == null || Balance.Count == 0 ) {
				//MyAPIGateway.Utilities.ShowNotification( "Trying to allocate resources" );
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.GetInventory(i);
					if( inv == null ) continue;
					//inventories.Add( inv );
					if( ConstructionSite != null ) {
						//MyAPIGateway.Utilities.ShowNotification( "Trying to move to stockpile" );
						ConstructionSite.MoveItemsToConstructionStockpile( inv );
					}
				}
			} else {
				//MyAPIGateway.Utilities.ShowNotification( "Trying to pay balance" );
				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.GetInventory(i);
					if( inv == null ) continue;
					List<IMyInventoryItem> items = inv.GetItems();
					foreach( IMyInventoryItem item in items ) {
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

		public void FindConstructionSite( List<IMySlimBlock> exclude = null ) {
			if( DockedTo != null ) {
				DockedTo.FindConstructionSite( exclude );
				return;
			}
			if( exclude == null ) exclude = new List<IMySlimBlock>();
			ConstructionSite = null;
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
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
				SetConstructionSite(best);
			}
		}

		public void TransferAllTo( MyInventoryBase from, MyInventoryBase to ) {
			List<MyPhysicalInventoryItem> items = from.GetItems();
			foreach( MyPhysicalInventoryItem item in items ) {
				//from.TransferItemsFrom(to, item, item.Amount);
				to.Add( item, item.Amount );
			}
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
			if( ass == null ) ass = GetAssembler();

			if( ass == null || block == null ) return false;

			if( clear ) ass.ClearQueue();

			MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition);
			return AddQueueItems( def, clear, ass );
		}

		public bool AddQueueItems( MyObjectBuilder_CubeBlock block, bool clear = false, IMyAssembler ass = null ) {
			return AddQueueItems( MyDefinitionManager.Static.GetCubeBlockDefinition(block), clear, ass);
		}

		public bool AddQueueItems( MyCubeBlockDefinition def, bool clear = false, IMyAssembler ass = null ) {
			if( def == null ) return false;
			if( ass == null ) ass = GetAssembler();
			//VRage.Game.MyObjectBuilder_CubeBlockDefinition.Component.CubeBlockComponent

			foreach( var component in def.Components ){
				MyBlueprintDefinitionBase blueprint = null;
				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);

				if( blueprint == null ) continue;

				if( !ass.CanUseBlueprint(blueprint) ) return false;
			}

			foreach( var component in def.Components ){
				MyBlueprintDefinitionBase blueprint = null;
				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);
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
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			Grid.GetBlocks( blocks );
			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				if( block.FatBlock is IMyMedicalRoom || block.FatBlock.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					return block.FatBlock;
				}
			}
			return null;
		}

		public IMySlimBlock TryPlace( MyObjectBuilder_CubeBlock block ) {
			if( block.Min.X == 0 && block.Min.Y == 0 && block.Min.Z == 0 ) {
				Vector3I pos = Vector3I.Zero;
				MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition( block );
				FindOpenSlot(out pos, def.Size, def.CubeSize );
				block.Min = pos;
			}

			IMySlimBlock slim = Grid.AddBlock( block, false );

			if( block.BuildPercent == 0.0f ) {
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

		// public IMyCubeGrid GetLargeGrid() {
		// 	if( Grid.GridSizeEnum == MyCubeSize.Large ) {
		// 		return Grid;
		// 	} else {
		// 		if( Grid == null ) return null;
		// 		List<IMySlimBlock> blocks = new List<IMySlimBlock>();
		//
		// 		Grid.GetBlocks( blocks );
		//
		// 		foreach( IMySlimBlock block in blocks ) {
		// 			if( block.CubeGrid.GridSizeEnum == MyCubeSize.Large )
		// 				return block.CubeGrid;
		// 		}
		// 	}
		//
		// 	return null;
		// }

		public IMyAssembler GetAssembler() {
			List<IMySlimBlock> blocks = GetBlocks<IMyAssembler>();
			IMyAssembler best = null;
			int priority = 0;

			foreach( IMySlimBlock block in blocks ) {
				if( !block.FatBlock.IsFunctional ) continue;

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

		public static IMyCubeGrid Spawn(MyPrefabDefinition prefab, MatrixD matrix, Faction owner) {
			if( prefab == null ) return null;

      IMyCubeGrid g = null;
			List<IMyCubeGrid> subgrids = new List<IMyCubeGrid>();
			MatrixD original = matrix;
      foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
				MyPositionAndOrientation po = grid.PositionAndOrientation ?? new MyPositionAndOrientation(ref matrix);
				// if( prefab.SubtypeId ) {
				// 	grid.Name = owner.Name + " Grid" + NumGrids.ToString();
				// 	grid.DisplayName = owner.Name + " Grid" + NumGrids.ToString();
				// 	NumGrids++;
				// } else {
					grid.Name = owner.Name + " " + prefab.Id.SubtypeName;
					grid.DisplayName = owner.Name + " " + prefab.Id.SubtypeName;
				//}
				grid.EntityId = (long)0;

				if( g == null ) {
					original = po.GetMatrix();
					grid.PositionAndOrientation = new MyPositionAndOrientation(ref matrix);
				} else {
					MatrixD offset = matrix + (po.GetMatrix() - original);
					//Quaternion rotation = Quaternion.CreateFromRotationMatrix(original);
					//Quaternion placement = Quaternion.CreateFromRotationMatrix(matrix);
					// original.Translation = matrix.Translation;
					// MatrixD diff = original - matrix;
					// original = matrix + diff;
					//original = matrix + MatrixD.CreateFromQuaternion(rotation-placement);

					grid.PositionAndOrientation = new MyPositionAndOrientation(ref offset);
				}

				long ownerId = owner != null && owner.MyFaction != null ? owner.MyFaction.FounderId : 0;
				foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
					block.EntityId = (long)0;
					block.Owner = ownerId;
					block.BuiltBy = ownerId;
					if( block.ColorMaskHSV == DefaultColor )
						block.ColorMaskHSV = owner.Color;
					//block.Min = new Vector3I(Vector3D.Transform(new Vector3D(block.Min), matrix))	;
				}
        MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

        if( entity == null ) {
          return null;
        }

        entity.Flags &= ~EntityFlags.Save;
				entity.Save = true;
        //ent.Flags &= ~EntityFlags.NeedsUpdate;

        entity.Render.Visible = true;
        //entity.WorldMatrix = matrix;
        //entity.PositionComp.SetPosition(new Vector3D(10,0,0));
        MyAPIGateway.Entities.AddEntity(entity);

				if( g == null ) {
	        g = entity as IMyCubeGrid;
				} else {
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
        //entity.WorldMatrix = matrix;
        MyAPIGateway.Entities.AddEntity(entity);

        g = entity as IMyCubeGrid;
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
					Name = "Converter",
					DisplayName = "Converter",
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

		public void SetConstructionSite( IMySlimBlock block ) {
			ConstructionSite = block;
			Need = Needs.Components;
			block.SetToConstructionSite();
			block.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionBegin);
			AddQueueItems( block.FatBlock, true );
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
						Subgrids.Add(suspension.RotorGrid);
					}
				}



				if( block.FatBlock is IMyMotorStator ) {
					IMyMotorStator stator = block.FatBlock as IMyMotorStator;
					stator.Attach();
					if( stator.RotorGrid != null ) {
						Subgrids.Add(stator.RotorGrid);
						stator.RotorGrid.GetBlocks(subblocks);
					}
				}
			}

			if( subblocks.Count > 0 )
				SetToConstructionSite(subblocks);
			else
				FindConstructionSite();
		}

		protected void StopProduction() {
			List<IMySlimBlock> factories = GetBlocks<IMyAssembler>();

			foreach( IMySlimBlock factory in factories ) {
				IMyAssembler ass = factory.FatBlock as IMyAssembler;
				ass.ClearQueue();
			}
		}

		public void FindSubgrids() {
			List<IMySlimBlock> motors = GetBlocks<IMyMotorStator>();
			foreach( IMySlimBlock slim in motors ) {
				IMyMotorStator motor = slim.FatBlock as IMyMotorStator;
				if( motor.RotorGrid != null )
					Subgrids.Add(motor.RotorGrid);
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
						foreach( IMyInventoryItem item in items ) {
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
				ConstructionSite.IncreaseMountLevel(5.0f,(long)0);


				if( ConstructionSite.IsFullIntegrity ) {
					ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionEnd);
					StopProduction();
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
