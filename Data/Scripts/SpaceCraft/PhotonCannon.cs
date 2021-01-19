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

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorTurret), false, "PhotonCannon")]
	public class PhotonCannon : MyGameLogicComponent {

		public IMyUserControllableGun Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;

			Block = Entity as IMyUserControllableGun;
			if( Block == null ) return;

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			// MyResourceSourceComponent source = Block.Components.Get<MyResourceSourceComponent>();
			// if( source != null ) {
			// 	source.Enabled = false;
			// }
			// SpaceCraftSession.SwitchToPsi(Block);
		}

		public override void UpdateAfterSimulation100() {
			if( Block == null || Block.Closed ) {
				Block = null;
				NeedsUpdate = MyEntityUpdateEnum.NONE;
				return;
			}
			IMyInventory inv = Block.GetInventory();
			if( inv == null || inv.GetItems().Count > 0 ) return;

			// TODO: Make sure powered by Psi
			inv.AddItems((VRage.MyFixedPoint)1, new MyObjectBuilder_AmmoMagazine(){
        SubtypeName = "PhotonRounds"
      } );

		}


	}


}
