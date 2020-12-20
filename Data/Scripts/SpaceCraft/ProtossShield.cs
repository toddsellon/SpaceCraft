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

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "LargeProtossShield", "SmallProtossShield")]
	public class ProtossShield : MyGameLogicComponent {

		private readonly static MyStringId Material = MyStringId.GetOrCompute("ProtossShield");
		private static Color Blue = new Color(0,153,255);
		public IMyBatteryBlock Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;
			
			Block = Entity as IMyBatteryBlock;
			if( Block == null ) return;
			MyResourceSourceComponent source = Block.Components.Get<MyResourceSourceComponent>();
			if( source != null ) {
				source.Enabled = false;
			}
			// SpaceCraftSession.SwitchToPsi(Block);
			SpaceCraftSession.AddShield(this,Block.CubeGrid);
		}

		public bool Activate() {
			if( Block == null || !Block.IsFunctional || Block.CubeGrid == null ) return false;

			MatrixD matrix = Block.CubeGrid.WorldMatrix;
			MySimpleObjectDraw.DrawTransparentSphere(ref matrix, Block.CubeGrid.LocalVolume.Radius*1.1f, ref Blue, MySimpleObjectRasterizer.SolidAndWireframe, 20);

			return true;

		}


	}


}
