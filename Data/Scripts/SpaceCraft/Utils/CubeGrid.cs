using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Definitions;
//using Sandbox.ModAPI.Ingame;
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

	public class CubeGrid : Controllable {

		public enum Needs {
			None,
	    Power,
	    Components,
			Storage
	  };

		public Needs Need = Needs.None;
    public IMyCubeGrid Grid;
		public string Prefab;

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

		public bool IsStatic
		{
			get
			{
				 return Grid == null ? false : Grid.IsStatic;
			}
			set
			{
				 if( Grid == null ) return;
				 //if( value && !Grid.IsStatic ) Grid.ConvertToStatic();
				 //else if( !value && Grid.IsStatic ) Grid.ConvertToDynamic();
			}
		}

		public override void Init() {
			if( Grid == null ) return;

			ControlledEntity = Grid as Sandbox.Game.Entities.IMyControllableEntity;

			// Determine available Conveyers
			//MyAPIGateway.Utilities.ShowMessage( "Init:", "" );
			// Wheels = Grid.GetFirstBlockOfType<IMyMotorSuspension>() != null;
			// Flying = Grid.GetFirstBlockOfType<MyThrust>() != null;
	    // Drill = Grid.GetFirstBlockOfType<IMyShipDrill>() != null;
	    // Welder = Grid.GetFirstBlockOfType<IMyShipWelder>() != null;
	    // Grider = Grid.GetFirstBlockOfType<IMyShipGrinder>() != null;
		}

		public override void UpdateBeforeSimulation () {

		}

		public override void UpdateBeforeSimulation100() {
			CheckOrder();
			if( Need == null )
				AssessNeed();
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
					CompleteOrder();
					break;
			}
		}

		public bool AddQueueItem( MyDefinitionBase blueprint, VRage.MyFixedPoint amount ) {
			IMyProductionBlock ass = GetAssembler();
			if( ass != null ) {
				ass.AddQueueItem( blueprint, amount );
				return true;
			}

			return false;
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

		//public Vector3I FindOpenSlot( SerializableVector3I size, Grid.GridSizeEnum gridSize = MyCubeSize.Large ) {
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

		public IMyProductionBlock GetAssembler() {
			if( Grid == null ) return null;
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();

			Grid.GetBlocks( blocks );

			foreach( IMySlimBlock block in blocks ) {
				//if(block.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_StoreBlock) == true) {}
				if( block is IMyProductionBlock ) {
					return block as IMyProductionBlock;
				}
			}

			return null;
		}

		public static IMyCubeGrid Spawn(string prefabName, MatrixD matrix) {
			MyPrefabDefinition prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);
      IMyCubeGrid g = null;

      if( prefab == null ) {
        MyAPIGateway.Utilities.ShowNotification("Prefab not found");
        return null;
      }

      foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
        MyAPIGateway.Utilities.ShowNotification("Trying to create: " + grid.DisplayName);
        MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

        if( entity == null ) {
          MyAPIGateway.Utilities.ShowNotification("Failed to create entity " + prefabName);
          return null;
        }

        entity.Flags &= ~EntityFlags.Save;
        //ent.Flags &= ~EntityFlags.NeedsUpdate;

        entity.Render.Visible = true;
        entity.WorldMatrix = matrix;
        //entity.PositionComp.SetPosition(new Vector3D(10,0,0));
        MyAPIGateway.Entities.AddEntity(entity);

        g = entity as IMyCubeGrid;
      }

      return g;
		}

	}

}
