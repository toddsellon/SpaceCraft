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

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_SurvivalKit), false, "SurvivalKit", "SurvivalKit")]
	public class SurvivalKit : MyGameLogicComponent {

		public IMyProductionBlock block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
			block = Entity as IMyProductionBlock;
		}


		public override void UpdateAfterSimulation() {
			//MyInventoryBase inv = block.GetInventoryBase();
			MyInventoryBase inv = block.Components.Get<MyInventoryBase>();

			if( inv == null ) return;

			if( block.IsQueueEmpty )
				block.AddQueueItem( MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/StoneOreToIngotBasic"), (VRage.MyFixedPoint)1 );

			inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_Ore(){
        SubtypeName = "Stone"
      } );
    }


	}


}
