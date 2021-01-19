using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using VRage;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.AI;
using Sandbox.Game.World;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Components;
using VRage.Game;
using VRage.Library.Utils;	// For MyGameModeEnum
using VRageMath;
using VRageRender;
using VRage.Utils;
using VRage.Game.Entity;
// using SpaceEngineers.Game.ModAPI;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace SpaceCraft {

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenFarm), false, "VespeneFarm")]
	public class VespeneFarm : MyGameLogicComponent {

		public IMyCubeBlock Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;

			Block = Entity as IMyCubeBlock;
			if( Block == null ) return;

			// NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			// MyAPIGateway.Utilities.ShowMessage( "Init", "VespeneFarm initialized" );
		}

		// public override void UpdateAfterSimulation() {
		public override void UpdateAfterSimulation100() {
			// if( Block == null || Entity.Closed ) {
			// 	Block = null;
			// 	NeedsUpdate = MyEntityUpdateEnum.NONE;
			// 	MyAPIGateway.Utilities.ShowMessage( "UAS", "VespeneFarm removed" );
			// 	return;
			// }

			if( Block.CubeGrid.Physics.Speed > 10f ) {
				MyVisualScriptLogicProvider.CreateExplosion(Entity.WorldMatrix.Translation, 15.1f, 500);
				// MyAPIGateway.Utilities.ShowMessage( "UAS", "VespeneFarm blew up" );
			} /*else {
				MyAPIGateway.Utilities.ShowMessage( "UAS", Block.CubeGrid.Physics.Speed.ToString() );
			}*/

		}


	}


}
