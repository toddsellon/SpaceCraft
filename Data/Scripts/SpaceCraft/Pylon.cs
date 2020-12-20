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

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "LargePylon", "SmallPylon","LargeProtossShield","SmallProtossShield")]
	public class Pylon : MyGameLogicComponent {


		public IMyBatteryBlock Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;
			
			Block = Entity as IMyBatteryBlock;
			if( Block == null ) return;

			// SpaceCraftSession.SwitchToPsi(Block, true);

		}


	}


}
