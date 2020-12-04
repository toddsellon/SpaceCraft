using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using VRage;
using Sandbox.ModAPI;
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

namespace SpaceCraft {

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "LargeTerranCloaking", "SmallTerranCloaking")]
	public class CloakingDevice : MyGameLogicComponent {

		public IMyBatteryBlock Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {

			Block = Entity as IMyBatteryBlock;
			if( Block == null ) return;

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
		}

		public override void UpdateAfterSimulation100() {
			if( Block == null ) {
				NeedsUpdate = MyEntityUpdateEnum.NONE;
				return;
			}
			if( Block.Enabled && Block.IsFunctional ) {
				Block.CubeGrid.Render.Visible = false;
				// TODO: Drain power
			} else {
				Block.CubeGrid.Render.Visible = true;
			}
		}

	}


}
