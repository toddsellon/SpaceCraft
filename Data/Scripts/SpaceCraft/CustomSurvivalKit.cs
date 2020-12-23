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

	// [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), false, "ZergSurvivalKit","ProtossSurvivalKitLarge","ProtossSurvivalKit")]
	public class CustomSurvivalKit : MyGameLogicComponent {

		public IMyAssembler Block;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;

			Block = Entity as IMyAssembler;
			if( Block == null ) return;


			MyResourceDistributorComponent dist = Block.Components.Get<MyResourceDistributorComponent>();
			MyResourceSourceComponent source = Block.Components.Get<MyResourceSourceComponent>();
			//MyResourceSinkComponent sink = Block.Components.Get<MyResourceSinkComponent>();
			if( dist == null || source == null ) {
				MyAPIGateway.Utilities.ShowMessage( "CustomSurvivalKit", "Source or dist was null" );
				return;
			}

			//dist.AddSink(sink);
			dist.AddSource(source);

			MyAPIGateway.Utilities.ShowMessage( "CustomSurvivalKit", "Init success?" );

		}



	}


}
