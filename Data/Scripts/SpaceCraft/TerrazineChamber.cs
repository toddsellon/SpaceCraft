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
using Sandbox.Game.Components;
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
using VRage.Game.ObjectBuilders.Definitions;
using SpaceCraft.Utils;

namespace SpaceCraft {

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_CryoChamber), false, "TerrazineChamber", "TerrazineChamberLarge")]
	public class TerrazineChamber : MyGameLogicComponent {

		public IMyCryoChamber Block;
		public MyResourceSinkComponent Sink;
		private int Tick = 0;
		// private bool _working = false;
		// private bool Working {
		// 	get {
		// 		return _working;
		// 	}
		// 	set {
		// 		_working = value;
		//
		// 		// Sink.SetRequiredInputByType(Terrazine, _working ? .1f : 0);
		// 	}
		// }

		private bool Working = false;
		private static readonly MyDefinitionId Terrazine = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Terrazine");

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			if( !SpaceCraftSession.Server ) return;

			Block = Entity as IMyCryoChamber;
			if( Block == null ) return;

			// Sink = Block.Components.Get<MyResourceSinkComponent>();
			//
			// if( Sink != null ) {
			// 	MyResourceSinkInfo info = new MyResourceSinkInfo {
			// 		ResourceTypeId = Terrazine,
			// 		MaxRequiredInput = 0,
			// 		RequiredInputFunc = ComputeRequiredGas
			// 	};
			// 	Sink.Init(MyStringHash.GetOrCompute("Thrust"),info);
			// 	Sink.AddType(ref info);
			// 	//Sink.SetMaxRequiredInputByType(Terrazine, 0f);
			// }

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
		}

		public float ComputeRequiredGas() {
			// if( !Block.IsFunctional || !Working )
      //   return 0f;
			return .1f;
    }

		private IMyOxygenTank GetTank() {
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			Block.CubeGrid.GetBlocks(blocks);
			foreach( IMySlimBlock slim in blocks ) {
				if( slim.FatBlock == null ) continue;
				IMyOxygenTank tank = slim.FatBlock as IMyOxygenTank;
				if( tank == null || tank.FilledRatio == 0f ) continue;
				MyGasTankDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(tank.BlockDefinition) as MyGasTankDefinition;
				if( def == null ) continue;
				if( def.StoredGasId != Terrazine ) {
					continue;
				}
				return tank;
			}

			return null;
		}

		public override void UpdateAfterSimulation100() {
			// IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(block.CubeGrid);

			if( Block == null || Block.Closed ) {
				Block = null;
				NeedsUpdate = MyEntityUpdateEnum.NONE;
				return;
			}

			if( Block.IsUnderControl ) {
				MyObjectBuilder_Cockpit ob = Block.GetObjectBuilderCubeBlock() as MyObjectBuilder_Cockpit;
				IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(Entity);
				// Sink.Update();

				// if( !Sink.IsPoweredByType(Terrazine) ) {
				// if( Working && !Sink.IsPowerAvailable(Terrazine, .1f) ) {
				IMyOxygenTank tank = GetTank();
				if( tank == null ) {
					CLI.SendMessageToClient( new Message {
						Sender = "Adjutant",
						Text = "There is no terrazine connected to the chamber, commander.",
						SteamUserId = player.SteamUserId,
						Sound = "terrazine-error"
					});
					Eject();
					return;
				}

				MyResourceSinkComponent sink = tank.Components.Get<MyResourceSinkComponent>();
				if( sink != null )
					sink.SetInputFromDistributor(Terrazine,-.1f,true,true);

				Working = true;
				BoundingSphereD sphere = new BoundingSphereD( Block.CubeGrid.GridIntegerToWorld(Block.Position), 1 );
				List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
				IMyCharacter character = GetCharacter(entities);

				// IMyPlayer player = ob.AttachedPlayerId.HasValue ? SpaceCraftSession.GetPlayer(ob.AttachedPlayerId.Value) : null;
				if( character != null ) {

					// Sink.Update();

					if( Tick == 1 ) {

						// if( player != null)
							CLI.SendMessageToClient( new Message {
		            Sender = "Adjutant",
		            Text = "Try to remain calm, commander. The Terrazine treatment process does not take long.",
		            SteamUserId = player.SteamUserId,
		            Sound = "terrazine-start"
		          });
					}

					if( Tick == 10 ) {
						// if( player != null)
							CLI.SendMessageToClient( new Message {
		            Sender = "Adjutant",
		            Text = "Process complete. Do you feel any different?",
		            SteamUserId = player.SteamUserId,
		            Sound = "terrazine-complete"
		          });



						Eject();

						Buffs.ApplyBuff(character, Buff.Terrazine, player: player );
						Tick = 0;
						return;
					}

					Tick++;


				} else {
					Tick = 0;
					Working = false;
				}
			} else {
				Tick = 0;
				Working = false;
			}

		}

		private void Eject() {
			MyVisualScriptLogicProvider.SetName(Entity.EntityId,Entity.EntityId.ToString());
			MyVisualScriptLogicProvider.CockpitRemovePilot(Entity.EntityId.ToString());
		}

		private static IMyCharacter GetCharacter( List<IMyEntity> entities ) {
			foreach(IMyEntity entity in entities) {
				IMyCharacter character = entity as IMyCharacter;
				if( character != null ) return character;
			}

			return null;
		}


	}


}
