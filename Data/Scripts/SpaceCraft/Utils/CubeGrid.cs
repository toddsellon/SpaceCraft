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
//using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace SpaceCraft.Utils {

	//[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class CubeGrid : Controllable {

		public enum Needs {
			None,
	    Power,
	    Components,
			Storage
	  };

		public IMySlimBlock ConstructionSite;
		public Needs Need = Needs.None;
    public IMyCubeGrid Grid;
		public List<IMyCubeGrid> Subgrids = new List<IMyCubeGrid>();
		public string Prefab;
		public IMyCubeGrid SuperGrid;
		protected static int NumGrids = 0;
		public int Tick = 0;

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

		// public bool IsStatic
		// {
		// 	get
		// 	{
		// 		 return Grid == null ? false : Grid.IsStatic;
		// 	}
		// 	set
		// 	{
		// 		 if( Grid == null ) return;
		// 		 if( value && !Grid.IsStatic ) Grid.Physics.ConvertToStatic();
		// 		 else if( !value && Grid.IsStatic ) Grid.ConvertToDynamic();
		// 	}
		// }

		public CubeGrid( IMyCubeGrid grid ) {
			Grid = grid;
			ControlledEntity = Grid as IMyControllableEntity;
			// Wheels = Grid.GetFirstBlockOfType<IMyMotorSuspension>() != null;
			// Flying = Grid.GetFirstBlockOfType<MyThrust>() != null;
	    // Drill = Grid.GetFirstBlockOfType<IMyShipDrill>() != null;
	    // Welder = Grid.GetFirstBlockOfType<IMyShipWelder>() != null;
	    // Grider = Grid.GetFirstBlockOfType<IMyShipGrinder>() != null;
		}


		public override void Init( MyObjectBuilder_SessionComponent session ) {
			base.Init(session);
		}

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

		public List<IMySlimBlock> GetBlocks<t>() {
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();

			Grid.GetBlocks( blocks );
			if( SuperGrid != null ) {
				SuperGrid.GetBlocks( blocks );
			}

			if( blocks.Count > 0 && !(blocks[0] is t) ) {
				List<IMySlimBlock> ret = new List<IMySlimBlock>();

				foreach( IMySlimBlock block in blocks ) {
					if( block.FatBlock == null || !(block.FatBlock is t) ) continue;
					ret.Add(block);
				}

				return ret;
			}

			return blocks;
		}

		public override void UpdateBeforeSimulation() {
			Tick++;
			CheckOrder();
			if( Tick == 99 )
				AssessInventory();

			if( Tick == 99 ) {
				Tick = 0;
			}
		}

		// public override void UpdateBeforeSimulation100() {
		// 	CheckOrder();
		// 	MyAPIGateway.Utilities.ShowNotification("Needs: " + Need );
		// 	if( Need == null || Need == Needs.None ) {
		// 		AssessNeed();
		// 	} else {
		// 		switch( Need ) {
		// 	    case Needs.Power:
		// 				// AddPowerSource()
		// 				break;
		// 	    case Needs.Components:
		// 				AssessInventory();
		// 				break;
		// 			case Needs.Storage:
		// 				// AddStorageCapacity()
		// 				break;
		// 		}
		// 	}
		// }

		public void AssessInventory() {
			MyObjectBuilderType ORE = MyObjectBuilderType.Parse("MyObjectBuilder_Ore");
			//if( ConstructionSite == null || ConstructionSite.FatBlock == null ) return;

			float old = ConstructionSite == null ? 1.0f : ConstructionSite.BuildIntegrity;
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			List<IMyInventory> inventories = new List<IMyInventory>();
			List<IMyAssembler> factories = new List<IMyAssembler>();
			List<IMyRefinery> refineries = new List<IMyRefinery>();

			// Update Construction Site
			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				if( block.FatBlock is IMyAssembler )
					factories.Add( block.FatBlock as IMyAssembler );
				if( block.FatBlock is IMyRefinery )
					refineries.Add( block.FatBlock as IMyRefinery );

				for( int i = 0; i < 2; i++ ) {
					IMyInventory inv = block.FatBlock.GetInventory(i);
					if( inv != null ) {
						inventories.Add( inv );
						if( ConstructionSite != null )
							ConstructionSite.MoveItemsToConstructionStockpile( inv );
					}
				}

			}

			// <MyProductionQueueItem>
			// MyFixedPoint 	Amount
			// MyDefinitionBase 	Blueprint
			// uint 	ItemId
			//MyObjectBuilder_PhysicalObject ob = new MyObjectBuilder_PhysicalObject();

			// Pull Ore
			foreach( IMyRefinery refinery in refineries ) {
				if( refinery.IsProducing ) continue;

				IMyInventory inventory = refinery.GetInventory(0);

				foreach( IMyInventory inv in inventories ) {
					bool found = false;
					if( inv == inventory ) continue;

					List<IMyInventoryItem> itms = inv.GetItems();
					for( int i = 0; i < itms.Count; i++ ) {
						IMyInventoryItem itm = itms[i];
						if( itm.Content.TypeId == ORE ) {
							found = true;
							inv.TransferItemTo(inventory, i, null, true, (VRage.MyFixedPoint)100, false);
							break;
						}
					}
					if( found ) break;
				}
			}

			// Pull Components
			foreach( IMyAssembler factory in factories ) {
				if( factory.IsQueueEmpty ) continue;

				List<MyProductionQueueItem> queue = factory.GetQueue();
				MyProductionQueueItem item = queue[0];
				//item.Blueprint.Id.SubtypeName;
				MyBlueprintDefinitionBase bp =	MyDefinitionManager.Static.GetBlueprintDefinition(item.Blueprint.Id);
				IMyInventory inventory = factory.GetInventory(0);

				// Check if pull needed since IsProducing is useless
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

				if( needs.Count == 0 ) continue;

				//MyAPIGateway.Utilities.ShowMessage( "AssessInventory", String.Join( ", ", needs ) );

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
								//MyAPIGateway.Utilities.ShowMessage( "AssessInventory","Trying to pull items" );
								inv.TransferItemTo(inventory, i, null, true, need.Amount, false);
								break;
							} else if( need.Id.TypeId == itm.Content.TypeId ) {
								//MyAPIGateway.Utilities.ShowMessage( "AssessInventory", "itm.Amount:" + itm.Amount.ToString() + " != need.Amount:" + need.Amount.ToString() );
							}
						}

						if( found ) break; // Inventories
					}
				}

				// MyAPIGateway.Utilities.ShowNotification("Stopped Factory Queue: " + factory.IsProducing.ToString() + "("+queue.Count.ToString()+")" );
				// bool found = false;
				//
				// foreach( IMyInventory inv in inventories ) {
				// 	foreach( var prereq in bp.Prerequisites ) {
				//
				//
				// 		for( int i = 0; i < items.Count; i++ ) {
				// 			IMyInventoryItem itm = items[i];
				//
				// 			MyAPIGateway.Utilities.ShowMessage( "AssessInventory","prereq.Id.SubtypeName == itm.Content.SubtypeName: " + prereq.Id.SubtypeName + " == " + itm.Content.SubtypeName );
				// 			if( itm.Amount >= prereq.Amount && prereq.Id.SubtypeName == itm.Content.SubtypeName ) {
				// 				// Transfer
				// 				found = true;
				// 				MyAPIGateway.Utilities.ShowMessage( "AssessInventory","Trying to pull items" );
				// 				inv.TransferItemTo(factory.GetInventory(), i, null, true, itm.Amount, false);
				// 				break;
				// 			}
				// 		}
				//
				// 		if( found ) break;
				// 	}
				//
				// 	if( found ) break;





					//inv.ContainItems (VRage.MyFixedPoint amount, MyObjectBuilder_PhysicalObject ob)
					// foreach( MyBlueprintDefinitionBase.ProductionInfo info in blueprints ) {
					// 	List<IMyInventoryItem> items = inv.GetItems();
					// 	for( int i = 0; i < items.Count; i++ ) {
					// 		IMyInventoryItem item = items[i];
					// 		//															MyBlueprintDefinitionBase
					// 		if( item.Amount >= info.Amount && info.Blueprint == item.ItemId )
					// 	}
					// 	// ob.SubtypeName = info.Blueprint.Id.SubtypeName;
					// 	// if( inv.ContainItems((VRage.MyFixedPoint)3, ob ) ) {
					// 	// 	inv.TransferItemTo(factory.GetInventory(0));
					// 	// }
					// }

				}
			//}


			if( ConstructionSite != null ) {
				ConstructionSite.IncreaseMountLevel(5.0f,(long)0);


				if( ConstructionSite.IsFullIntegrity ) {
					ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionEnd);
					FindConstructionSite();
				} else if( old < ConstructionSite.BuildIntegrity ) {
					ConstructionSite.PlayConstructionSound(MyIntegrityChangeEnum.ConstructionProcess);
				}
			}
		}

		public void FindConstructionSite() {
			ConstructionSite = null;
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();
			IMySlimBlock best = null;
			int priority = 0;

			foreach( IMySlimBlock block in blocks ) {
				if( block.IsFullIntegrity ) continue;

				int p = Prioritize(block);
				//MyAPIGateway.Utilities.ShowMessage( "Priority", block.ToString() + " " + p.ToString() );
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

		public bool AddQueueItems( IMyCubeBlock block, bool clear = false ) {
			IMyAssembler ass = GetAssembler();
			bool success = true;

			if( ass == null || block == null ) return false;

			if( clear ) ass.ClearQueue();

			MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition);
			if( def == null ) return false;
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

			MyAPIGateway.Utilities.ShowMessage( "AddQueueItems", block.ToString() + " components queued with " + ass.ToString()  );

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

		public void AssessNeed() {
			if( Grid == null ) return;

			float power = 0.0f; 	 // Power generated
			float stored = 0.0f; 	 // Power stored

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

				if( block.FatBlock is IMySolarPanel ) {
					power += (block.FatBlock as IMySolarPanel ).CurrentOutput;
				}
				else if( block.FatBlock is IMyBatteryBlock ) {
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

				if( power < 0 ) {
					Need = Needs.Power;
				}/* else if( Need == null && inv.CurrentMass / inv.MaxMass > .9 ) {
					Need = Needs.Storage;
				}*/
				//NetPower( block, ref power, ref stored, ref required, ref capacity );

			}
		}

		public IMyCubeBlock GetRespawnBlock() {
			if( Grid == null ) return null;

			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			Grid.GetBlocks( blocks );
			foreach( IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				//MyRespawnComponent respawn = block.FatBlockComponents.Get<MyRespawnComponent>();// Not allowed
				//if( block.FatBlock is IMyMedicalRoom || block.FatBlock.BlockDefinition.TypeIdString == "SurvivalKit" ) {
				if( block.FatBlock is IMyMedicalRoom || block.FatBlock.BlockDefinition.TypeIdString == "MyObjectBuilder_SurvivalKit" ) {
					//MyAPIGateway.Utilities.ShowNotification("Respawn Block Accepted: " + block.FatBlock.BlockDefinition.TypeIdString);
					return block.FatBlock;
				} else {
					//block.BlockDefinition.DisplayNameString
					//MyAPIGateway.Utilities.ShowNotification("Block Rejected: " + block.FatBlock.BlockDefinition.TypeIdString);
				}
			}

			//MyAPIGateway.Utilities.ShowNotification("RESPAWN BLOCK NOT FOUND!!!");
			return null;
		}

		public void CheckOrder() {
			if( CurrentOrder == null ) return;

			switch( CurrentOrder.Type ) {
				case Orders.Move:
					break;
			}
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

						//MyAPIGateway.Utilities.ShowMessage( "point.Normal", point.Normal.ToString() );
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

		public IMyCubeGrid GetLargeGrid() {
			if( Grid.GridSizeEnum == MyCubeSize.Large ) {
				return Grid;
			} else {
				if( Grid == null ) return null;
				List<IMySlimBlock> blocks = new List<IMySlimBlock>();

				Grid.GetBlocks( blocks );

				foreach( IMySlimBlock block in blocks ) {
					if( block.CubeGrid.GridSizeEnum == MyCubeSize.Large )
						return block.CubeGrid;
				}
			}

			return null;
		}

		public IMyAssembler GetAssembler() {
			List<IMySlimBlock> blocks = GetBlocks<IMyAssembler>();
			IMyAssembler best = null;
			int priority = 0;

			foreach( IMySlimBlock block in blocks ) {
				if( !block.FatBlock.IsFunctional ) continue;

				int p = Prioritize( block );
				//MyAPIGateway.Utilities.ShowMessage( "GetAssembler", block.ToString() + " Priority: " + p.ToString() );
				if( best == null || p > priority ) {
					best = block.FatBlock as IMyAssembler;
					priority = p;
				}
			}

			return best;
		}

		public static IMyCubeGrid Spawn(Prefab prefab, MatrixD matrix) {
			return Spawn( prefab.Definition, matrix );
		}

		public static IMyCubeGrid Spawn(string prefabName, MatrixD matrix) {
			MyPrefabDefinition prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);
			return Spawn( prefab, matrix );
		}

		public static IMyCubeGrid Spawn(MyPrefabDefinition prefab, MatrixD matrix) {
			if( prefab == null ) return null;

      IMyCubeGrid g = null;
			List<IMyCubeGrid> subgrids = new List<IMyCubeGrid>();

      foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
				grid.Name = "StarCraft Grid" + NumGrids.ToString();
				grid.DisplayName = "StarCraft Grid" + NumGrids.ToString();
				grid.EntityId = (long)0;
				grid.PositionAndOrientation = new MyPositionAndOrientation(ref matrix);
				NumGrids++;

				foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
					block.EntityId = (long)0;
				}
        MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

        if( entity == null ) {
          return null;
        }

        entity.Flags &= ~EntityFlags.Save;
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



		public static IMyCubeGrid Spawn(MyObjectBuilder_CubeGrid grid, MatrixD matrix) {
			/*if( String.IsNullOrWhiteSpace(grid.Name) ) {
				grid.Name =
			}*/
			grid.Name = "StarCraft Grid" + NumGrids.ToString();
			grid.DisplayName = "StarCraft Grid" + NumGrids.ToString();
			grid.PositionAndOrientation = new MyPositionAndOrientation(ref matrix);
			NumGrids++;
			MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);
			IMyCubeGrid g = null;

			if( entity != null ) {
				entity.Flags &= ~EntityFlags.Save;

        entity.Render.Visible = true;
        //entity.WorldMatrix = matrix;
        MyAPIGateway.Entities.AddEntity(entity);

        g = entity as IMyCubeGrid;
			}

			return g;
		}

		public bool AddPowerSource() {
			if( Grid.Physics.Gravity != Vector3.Zero ) {

			}

			return true;
		}

		public bool AddLargeGridConverter( bool skipRefinery = true ) {
			IMyCubeGrid grid;
			MyAPIGateway.Utilities.ShowMessage( "AddLargeGridConverter", "Trying AddLargeGridConverter()" );
			IMySlimBlock slim = TryPlace( new MyObjectBuilder_MotorAdvancedRotor{
				SubtypeName = "SmallAdvancedRotor",
				Orientation =  Quaternion.CreateFromForwardUp(Vector3.Left, Vector3.Backward),
				Min = new Vector3I(-1,-1,-3),
				BuildPercent = 0.0f,
				ConstructionInventory = new MyObjectBuilder_Inventory()
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
							ConstructionInventory = new MyObjectBuilder_Inventory()
						}
					}
				}, slim.FatBlock.WorldMatrix );

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
								ConstructionInventory = new MyObjectBuilder_Inventory()
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
							ConstructionInventory = new MyObjectBuilder_Inventory()
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

		public void SetToConstructionSite() {
			List<IMySlimBlock> blocks = GetBlocks<IMySlimBlock>();

			foreach( IMySlimBlock block in blocks ) {
				block.SetToConstructionSite();
				block.IncreaseMountLevel( 0f, (long)0 );	
			}

			FindConstructionSite();
		}



	}

}
