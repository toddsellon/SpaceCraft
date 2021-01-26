using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Game.World;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using ProtoBuf;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;
using VRageMath;
using VRage.Collections;
using LitJson;
using SpaceCraft.Utils;
using SpaceCraft.Utils.MES;

namespace SpaceCraft {

	public enum Races {
		Terran,
		Zerg,
		Protoss,
		Hybrid
	};


	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class SpaceCraftSession:MySessionComponentBase {

		private readonly static MyStringHash SourceGroup = MyStringHash.Get("Battery");
		private readonly static MyStringHash SinkGroup = MyStringHash.Get("BatteryBlock");


		public static Guid GuidFaction = new Guid("420E2CAB-0947-4E2C-A8E2-41D96806E22D");
		// Modular Encounter Spawner
		public static Guid GuidSpawnType = new Guid("C9D22735-C76B-4DB4-AFB5-51D1E1516A05");
    public static Guid GuidIgnoreCleanup = new Guid("7ADDED32-4069-4C52-891C-25F52478B2EB");
		public static Guid GuidStartCoords = new Guid("CC27ADFD-A121-477A-94B1-FB1B4E2E3046");
		public static Guid GuidEndCoords = new Guid("513F6C90-E0D9-4A8F-972E-09757FE32C19");



		public ulong Current = 0;
		public ushort Tick = 0;
    public string SaveName;
		public bool Loaded = false;
		public bool Spawned = false;
		public static bool Server = false;
    public static List<Faction> SCFactions = new List<Faction>();
		public static List<MyPlanet> Planets = new List<MyPlanet>();
		public static MyPlanet ClosestPlanet { get; protected set; }
		public static CLI MyCLI;
		public static long NumPlayers = 0;
		protected static ConcurrentDictionary<ProtossShield,IMyCubeGrid> ShieldedGrids = new ConcurrentDictionary<ProtossShield,IMyCubeGrid>();
		protected static List<IMySlimBlock> ZergBlocks = new List<IMySlimBlock>();
		public static Dictionary<string,MyBlueprintDefinitionBase> Components = new Dictionary<string,MyBlueprintDefinitionBase>();
		private static Stack<Action<IMyCharacter>> SpawnActions = new Stack<Action<IMyCharacter>>();

    public override void Init(MyObjectBuilder_SessionComponent session) {
      base.Init(session);

			Server = MyAPIGateway.Multiplayer.IsServer;
			Loaded = !Server;
			//MyAPIGateway.Utilities.IsDedicated;
			MyCLI = new CLI(Server);


			// HashSet<long> npcs = MyAPIGateway.Players.GetNPCIdentities();
			// foreach( )

			if( Server ) {

				//MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out Spawned);
				Spawned = Convars.Static.Spawned;
				SaveName = MyAPIGateway.Session.Name;
				MyAPIGateway.Session.SessionSettings.EnableRemoteBlockRemoval = false;
				NumPlayers = MyAPIGateway.Players.Count;
				MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, DamageHandler);
				MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
				FindBlueprintDefinitions();
			}
    }

		// Was having trouble getting MyDefinitionManager to recognize custom BPs so this is the workaround
		private static void FindBlueprintDefinitions() {
			string[] names = { "Adanium", "Organic", "Crystal", "PsionicLink", "ControlUnit", "ZergCarapace", "VentralSacks", "MetabolicGlands" };
			var bps = MyDefinitionManager.Static.GetBlueprintDefinitions();
			foreach( MyBlueprintDefinitionBase bp in bps ) {
				if( Array.IndexOf(names, bp.Id.SubtypeName) >= 0 ) {
					Components.Add(bp.Id.SubtypeName, bp );
				}
			}
		}

		public static MyBlueprintDefinitionBase GetBlueprintDefinition( string name ) {
			if( Components.Keys.Contains(name) )
				return Components[name];
			return null;
		}

		public void EntityAdded( IMyEntity entity ) {
			IMyCubeGrid grid = entity as IMyCubeGrid;
			IMyCharacter character = entity as IMyCharacter;

			if( character != null ) {
				CharacterAdded(character);
				return;
			}

			if( grid == null ) return;
			grid.OnBlockAdded += BlockAdded;
			// Determine if zerg
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);
			foreach( IMySlimBlock block in blocks ) {
				if( Zerg.Static.IsZerg(block) ) {
					ZergBlocks.Add(block);
				}
			}
		}

		public static void SpawnBot( string subtype, Vector3D position, Action<IMyCharacter> callback = null ) {
			if( callback != null )
				SpawnActions.Push(callback);
			MyVisualScriptLogicProvider.SpawnBot( subtype, position );
		}

		private void CharacterAdded( IMyCharacter character ) {

			// if( !character.IsBot ) return;
			// IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(character);
			//
			// if( player != null ) return;


			if( SpawnActions.Count == 0 ) return;
			Action<IMyCharacter> action = SpawnActions.Pop();
			action(character);
			// MyObjectBuilder_Character ob = character.GetObjectBuilder() as MyObjectBuilder_Character;
			//
			// if( ob == null ) {
			// 	MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", "ob was null" );
			// 	return;
			// }
			//
			// if( !ob.EntityDefinitionId.HasValue ) {
			// 	MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", "id was null" );
			// 	return;
			// }
			//
			// MyAPIGateway.Utilities.ShowMessage( "SubtypeName", ob.EntityDefinitionId.Value.SubtypeName );



			// MyAPIGateway.Utilities.ShowMessage( "PlayerSteamId", ob.PlayerSteamId.ToString() );
			// MyAPIGateway.Utilities.ShowMessage( "PlayerSerialId", ob.PlayerSerialId.ToString() );

			// if( player == null ) {
			// 	MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", "Player was null" );
			// 	return; // Not much we can do...
			// }
			// List<IMyIdentity> identities = new List<IMyIdentity>();
			// MyAPIGateway.Players.GetAllIdentites(identities);
			// foreach(IMyIdentity identity in identities) {
			//
			// }

			// MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", player.PlayerID.ToString() );
			// IMyFaction faction = 	MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);
			// if( faction == null ) {
			// 	MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", "Faction was null" );
			// 	return;
			// }
			// Faction scFaction = GetFaction(faction.Tag);
			// if( scFaction == null ) return; // Not our bot
			// MyAPIGateway.Utilities.ShowMessage( "CharacterAdded", scFaction.Name + " used a bot" );
			// scFaction.Bots.Add(character);
		}

		public void BlockAdded( IMySlimBlock block ) {
			if( Zerg.Static.IsZerg(block) )
				ZergBlocks.Add(block);
		}

		protected static void DamageHandler(object target, ref MyDamageInformation info) {
			IMySlimBlock slim = target as IMySlimBlock;
			// IMyCharacter character = target as IMyCharacter;
			//
			// if( character != null ) {
			// 	MyObjectBuilder_Character ob = character.GetObjectBuilder() as MyObjectBuilder_Character;
			// 	if( ob != null && ob.AIMode )
			// 		info.Amount *= 0;
			// 	else
			// 		MyAPIGateway.Utilities.ShowMessage( "DamageHandler", ob == null ? "ob was null" :  character.ToString() + " took damage" );
			// 	return;
			// }

			if( slim == null || slim.FatBlock == null ) return;
			IMyCubeGrid grid = slim.FatBlock.CubeGrid;

			if( ShieldedGrids.Values.Contains(grid) ) {
				List<ProtossShield> shields = new List<ProtossShield>();
				foreach( ProtossShield shield in ShieldedGrids.Keys.ToList() ) {
					if( shield.Block == null )  {
						ShieldedGrids.TryRemove(shield, out grid);
						continue;
					}
					if( ShieldedGrids[shield] == grid && shield.Activate() ) {
						info.Amount *= .5f;
						break;
					}
				}
			}
		}

		public static void AddShield( ProtossShield shield, IMyCubeGrid grid ) {
			ShieldedGrids.TryAdd(shield,grid);
		}

		// // Enable Zerg Healing
		// public static void SwitchToZerg( IMySlimBlock block ) {
		// 	if( !ZergBlocks.Contains(block) )
		// 		ZergBlocks.Add(block);
		// }

		public static void HealZergBlocks() {
			foreach( IMySlimBlock block in ZergBlocks.ToList() ) {
				if( block == null || block.FatBlock == null || block.FatBlock.Closed ) {
					ZergBlocks.Remove(block);
					continue;
				}
				if( block.IsFullIntegrity ) continue;
				// block.SetToConstructionSite();
				block.SpawnFirstItemInConstructionStockpile();
				// block.ClearConstructionStockpile(null);
				// block.SpawnConstructionStockpile();
				// block.MoveItemsToConstructionStockpile(null);
				// block.ClearConstructionStockpile(null);
        // block.SpawnConstructionStockpile();
				//block.IncreaseMountLevel(2f,block.FatBlock == null ? 0 : block.FatBlock.OwnerId);
				block.IncreaseMountLevel(2f,block.FatBlock.OwnerId);
			}
		}

		public static void SwitchToPsi( IMyCubeBlock block, bool isSource = false ) {
			MyResourceSourceComponent source = block.Components.Get<MyResourceSourceComponent>();
			MyResourceSinkComponent sink = block.Components.Get<MyResourceSinkComponent>();
			MyResourceDistributorComponent dist = block.Components.Get<MyResourceDistributorComponent>();



			if( sink != null ) {
				// sink.SetMaxRequiredInputByType(OBTypes.Psi, sink.MaxRequiredInput);
				// sink.SetMaxRequiredInputByType(OBTypes.Electricity, 0f);
				// sink.SetRequiredInputByType(OBTypes.Psi, sink.RequiredInput);
				// sink.SetRequiredInputByType(OBTypes.Electricity, 0f);
				MyResourceSinkInfo info = new MyResourceSinkInfo {
			 			MaxRequiredInput = sink.MaxRequiredInput,
            ResourceTypeId = OBTypes.Psi,
            RequiredInputFunc = Sink_ComputeRequiredPower,
        };
				sink.AddType(ref info);
				MyDefinitionId electricity = OBTypes.Electricity;
				// sink.RemoveType(ref electricity);
			}

			if( source != null ) {
				// source.Init(SourceGroup, new MyResourceSourceInfo {
				// 		ResourceTypeId = OBTypes.Psi,
				// 		DefinedOutput = source.MaxOutput,
				// 		ProductionToCapacityMultiplier = 60*60
				// });
				source.SetOutputByType(OBTypes.Psi, isSource ? source.CurrentOutput : 0f);
				source.SetOutputByType(OBTypes.Electricity, 0f);
				source.SetMaxOutputByType(OBTypes.Psi, source.MaxOutput);
				source.SetMaxOutputByType(OBTypes.Electricity, 0f);
				// if( source.ProductionEnabledByType(OBTypes.Electricity) ) {
				//
				// 	source.SetProductionEnabledByType(OBTypes.Electricity, false);
				//
				// }

				source.SetRemainingCapacityByType(OBTypes.Electricity, 0f);
				if( isSource && source.ProductionEnabledByType(OBTypes.Electricity) ) {
					source.SetProductionEnabledByType(OBTypes.Psi, true);
					source.SetRemainingCapacityByType(OBTypes.Psi, source.RemainingCapacity);
				}
			}

			if( sink != null ) sink.Update();

			// if( dist != null ) {
			// 	dist.ChangeSourcesState(Psi, MyMultipleEnabledEnum.AllEnabled, block.OwnerId);
			// 	dist.ChangeSourcesState(Electricity, MyMultipleEnabledEnum.AllDisabled, block.OwnerId);
			// }
		}

		public static float Sink_ComputeRequiredPower() {
			return 0f;
        // float inputRequiredToFillIn100Updates = (MyEnergyConstants.BATTERY_MAX_CAPACITY - ResourceSource.RemainingCapacityByType(MyResourceDistributorComponent.ElectricityId)) * VRage.Game.MyEngineConstants.UPDATE_STEPS_PER_SECOND / m_productionUpdateInterval * ResourceSource.ProductionToCapacityMultiplierByType(MyResourceDistributorComponent.ElectricityId);
        // float currentOutput = ResourceSource.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId);
        // currentOutput *= MySession.Static.CreativeMode ? 0f : 1f;
        // return Math.Min(inputRequiredToFillIn100Updates + currentOutput, MyEnergyConstants.BATTERY_MAX_POWER_INPUT);
    }

		// Main loop
    public override void UpdateBeforeSimulation() {

			if( !Server ) return;

			if( !Loaded ) {
				Preload();
				MES.Init();
				Quests.RemoveTechnology();
			}

			if( !Spawned ) {
				SpawnFactions();
				return;
			}

			foreach( Faction faction in SCFactions ) {
				faction.UpdateBeforeSimulation();
			}

			Tick++;
			if( Tick == 99 ) {
				Tick = 0;

				if(SaveName != MyAPIGateway.Session.Name) {// Saved
	        SaveName = MyAPIGateway.Session.Name;
					Convars.Static.Save();
					Factions.Static.Save();
					Quests.Static.Save();
	      }

				if( Convars.Static.Quests )
					Quests.Static.HitCheck();
			}

			if( MyAPIGateway.Players.Count > NumPlayers ) {
				PlayerJoined();
			}

			NumPlayers = MyAPIGateway.Players.Count;


			Current++;

			if( Current == 4000 ) {
				Current = 0;
				HealZergBlocks();
			}
			//MyAPIGateway.Players.NewPlayerRequestSucceeded += NewPlayerAdded;

    }

		private void PlayerJoined() {
			List<IMyPlayer> players = new List<IMyPlayer>();
			MyAPIGateway.Players.GetPlayers(players);
			if( !Convars.Static.Quests ) {
				IMyPlayer player = players.Last();
				SetReputation(player.PlayerID);
				return;
			}

			foreach(IMyPlayer p in players) {
				Quest quest = Quests.Static.GetQuest(p.SteamUserId,QuestId.StartingQuest);
				if( quest == null ) {
					SetReputation(p.PlayerID);
					Quests.Static.SetQuestState(p.SteamUserId,QuestId.StartingQuest);
					// Quests.LockTechnology( p.PlayerID );
				}
			}
		}

		// Destructor
		protected override void UnloadData(){
			//MyAPIGateway.Players.NewPlayerRequestSucceeded -= NewPlayerAdded;
			MyCLI.Destroy();
			MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
		}

		public static MyPlanet GetPlanet( string name ) {
			foreach( MyPlanet planet in Planets ) {
				if( planet.AsteroidName.StartsWith(name,StringComparison.OrdinalIgnoreCase) ) return planet;
			}
			return null;
		}

		public static MyPlanet GetClosestPlanet( Vector3D position, List<MyPlanet> exclude = null, string containing = "" ) {
			if( exclude == null ) exclude = new List<MyPlanet>();
			MyPlanet best = null;
			double bestDistance = 0.0f;
			double distance = 0.0f;
			foreach( MyPlanet planet in Planets ) {
				if( exclude.Contains(planet) ) continue;
				if( containing != String.Empty ) {
					bool found = false;
					foreach( MyPlanetOreMapping mapping in planet.Generator.OreMappings ) {
						if( mapping.Type == containing ) {
							found = true;
							break;
						}
					}
					if( !found ) continue;
				}
				//distance = Vector3D.Distance(position, planet.PositionLeftBottomCorner + (planet.SizeInMetres / 2));
				distance = Vector3D.Distance(position, planet.WorldMatrix.Translation);
				//distance = Vector3D.Distance(position, (planet as IMyEntity).LocalVolume.Center);
				if( best == null || distance < bestDistance ) {

					best = planet;
					bestDistance = distance;
				}
			}
			return best;
		}

		public void ScanEntities() {
			HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);

			Dictionary<Faction,List<IMyCubeGrid>> Owned = new Dictionary<Faction,List<IMyCubeGrid>>();

			foreach( Faction faction in SCFactions ) {
				Owned[faction] = new List<IMyCubeGrid>();
			}

			foreach( IMyEntity entity in entities ) {
				if( entity is MyPlanet ) {
					Planets.Add( entity as MyPlanet );
					continue;
				}


				if( entity is IMyCubeGrid ) {
					// Remove Pirates
					// if( entity.DisplayName.Substring(0,6) == "Pirate" ) {
					// 	MyAPIGateway.Entities.RemoveEntity(entity);
					// 	continue;
					// }



					IMyCubeGrid grid = entity as IMyCubeGrid;

					List<IMySlimBlock> blocks = new List<IMySlimBlock>();
					grid.GetBlocks(blocks);
					foreach( IMySlimBlock block in blocks ) {
						if( Zerg.Static.IsZerg(block) ) {
							ZergBlocks.Add(block);
							// if( !block.IsFullIntegrity ) {
							// 	// block.ApplyAccumulatedDamage();
							// 	// block.SetToConstructionSite();
							// 	block.DecreaseMountLevel(0f,null);
							// 	block.IncreaseMountLevel(0f,block.FatBlock == null ? 0 : block.FatBlock.OwnerId);
							// }
						}
					}

					//List<long> owners = grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners : grid.SmallOwners;
					//foreach(long owner in owners) {
						//Faction faction = GetFaction( owner ); // This should have worked but didn't, assuming the NPC owner ids get jumbled?

						Faction faction = entity.Storage != null && entity.Storage.ContainsKey(GuidFaction) ? GetFaction( entity.Storage[GuidFaction] ) : GetFaction( grid.DisplayName.Split(' ')[0] ); // Use first word in grid name, not ideal but it works
						if( faction == null ) continue;
						Owned[faction].Add(grid);
						grid.ChangeGridOwnership(faction.MyFaction == null ? 0 : faction.MyFaction.FounderId, MyOwnershipShareModeEnum.Faction);

						// if( faction.MyFaction != null ) {
						// 	grid.ChangeGridOwnership(faction.MyFaction.FounderId, MyOwnershipShareModeEnum.Faction);
						// 	//grid.UpdateOwnership(faction.MyFaction.FounderId, true);
						// }
						//
						// if( !faction.IsSubgrid(grid) ) {
						//
						//
						// 	CubeGrid g = faction.TakeControl( new CubeGrid(grid) ) as CubeGrid;
						// 	g.FindSubgrids();
						// 	g.CheckFlags();
						//
						// 	if( faction.MainBase == null ) // Until Cargo Ships, this is how resources are transferred
						// 		faction.MainBase = g;
						// 	else
						// 		faction.MainBase.ToggleDocked(g);
						// }

				}

			}



			foreach( Faction faction in SCFactions ) {

				// MyAPIGateway.Utilities.ShowMessage( "Owned[faction]", faction.Name + " " + Owned[faction].Count.ToString() );

				Dictionary<IMyCubeGrid,List<IMyCubeGrid>> subgrids = new Dictionary<IMyCubeGrid,List<IMyCubeGrid>>();

				foreach( IMyCubeGrid grid in Owned[faction] ) {
					IMyCubeGrid parent = GetParentGrid(grid);
					if( parent == null ) {
						CubeGrid g = faction.TakeControl( new CubeGrid(grid) ) as CubeGrid;
						g.FindSubgrids();
						g.CheckFlags();
						if( faction.MainBase == null ) // Until Cargo Ships, this is how resources are transferred
							faction.MainBase = g;
						else
							faction.MainBase.ToggleDocked(g);
					} else {
						if(!subgrids.ContainsKey(parent))
							subgrids[parent] = new List<IMyCubeGrid>();

						subgrids[parent].Add(grid);
					}
				}

				foreach( IMyCubeGrid grid in subgrids.Keys ) {
					CubeGrid g = faction.GetControllable(grid) as CubeGrid;
					if( g == null ) continue;
					foreach( IMyCubeGrid cg in subgrids[grid] ) {
						g.Subgrids.Add(cg);
					}
					// g.Subgrids = subgrids[grid];
					g.CheckFlags();
					//List<IMyCubeGrid> sg = subgrids[grid];
				}


			}

			// HealZergBlocks();

		}

		public static IMyCubeGrid GetParentGrid( IMyCubeGrid grid ) {

			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach(IMySlimBlock block in blocks ) {
				if( block.FatBlock == null ) continue;
				IMyMotorStator stator = block.FatBlock as IMyMotorStator;
				if( stator != null && stator.RotorGrid != null ) {
					return stator.RotorGrid;
				}
				IMyWheel wheel = block.FatBlock as IMyWheel;
				if( wheel != null ) {
					return wheel.CubeGrid;
				}
				// if( wheel != null && wheel.Stator != null && wheel.Stator.CubeGrid != null ) {
				// 	return wheel.Stator.CubeGrid;
				// }
				// IMyMotorSuspension sus = block.FatBlock as IMyMotorSuspension;
				// if( sus != null && sus.RotorGrid != null ) {
				// 	return sus.RotorGrid;
				// }
			}

			return null;
		}

		public void CheckMods() {
			// Not Implemented
			//foreach(var mod in MyAPIGateway.Session.Mods) {}
		}

    public void Preload() {

			OBTypes.Init();

      ListReader<MySpawnGroupDefinition> groups = MyDefinitionManager.Static.GetSpawnGroupDefinitions();

			foreach(MySpawnGroupDefinition group in groups ){
				if(group.Enabled == false || String.IsNullOrWhiteSpace(group.DescriptionText) ) continue;

				MyCommandLine cmd = new MyCommandLine();
				string first = String.Empty;

        if( group.DescriptionText.StartsWith("spacecraft",StringComparison.OrdinalIgnoreCase) && cmd.TryParse(group.DescriptionText) ) {

					string Name = cmd.Argument(1) ?? String.Empty;

					foreach( 	MySpawnGroupDefinition.SpawnGroupPrefab prefab in group.Prefabs ) {
						if( first == String.Empty ) first = prefab.SubtypeId;

						Races race = Races.Terran;
						if( cmd.Switch("toss") ) race = Races.Protoss;
						if( cmd.Switch("zerg") ) race = Races.Zerg;
						if( cmd.Switch("hybrid") ) race = Races.Hybrid;

						// if( first == String.Empty && Name != String.Empty ) first = prefab.SubtypeId;
						// else Prefab.Add(prefab.SubtypeId, Name, race);
						if( prefab.SubtypeId != "Terran Planet Pod" ) // This needs fixing
							Prefab.Add(prefab.SubtypeId, race, Name);
					}

          if( !String.IsNullOrWhiteSpace(Name) ) {

						Name = Name.ToUpper();

						Faction faction = CreateIfNotExists( Name, cmd );

						faction.Groups.Add( group );
						faction.StartingPrefab = first;

          }
        }

      }

			foreach( EstablishedFaction f in Factions.Static.Established ) {
				MyCommandLine cmd = new MyCommandLine();
				if( !cmd.TryParse("SpaceCraft F " + f.Command) ) continue;
				Faction faction = CreateIfNotExists( f.Tag, cmd );
				if( faction == null ) continue;
				faction.Established = true;
			}

			foreach( Faction faction in SCFactions ) {
				if( faction.CommandLine.Switch("aggressive") )
					faction.DeclareWar();
			}

			List<IMyIdentity> identities = new List<IMyIdentity>();
			MyAPIGateway.Players.GetAllIdentites(identities);
			foreach( IMyIdentity identity in identities) {
				foreach( Faction faction in SCFactions ) {
					if( faction.MyFaction == null ) continue;

					if( faction.MyFaction.FounderId == identity.IdentityId ) {
						faction.Founder = identity;
						//faction.Founder.SetColorMask((Vector3)faction.Color);
						//identity.ColorMask = faction.Color;
						break;
					}
				}
			}


			foreach(MyBotDefinition bot in MyDefinitionManager.Static.GetBotDefinitions()) {
				MyAnimalBotDefinition animal = bot as MyAnimalBotDefinition;
				if( animal == null ) continue;
				Faction faction = GetFaction(animal.FactionTag);
				if( faction != null ) {
					Prefab prefab = new Prefab {
						SubtypeId = animal.Id.SubtypeName,
						Bot = true,
						BotDefinition = animal,
						Faction = animal.FactionTag
					};

					prefab.Init();
				}
			}


			// List<IMyPlayer> players = new List<IMyPlayer>();
			// MyAPIGateway.Players.GetPlayers(players);
			// foreach( IMyPlayer player in players) {
			// 	MyAPIGateway.Utilities.ShowMessage( player.DisplayName,player.PlayerID.ToString() );
			// }

			ScanEntities();

			if( Spawned ) {
				if( ClosestPlanet == null ) ClosestPlanet = GetClosestPlanet( MyAPIGateway.Utilities.IsDedicated ? Vector3D.Zero : MyAPIGateway.Session.Player.GetPosition() );
				foreach( Faction faction in SCFactions ) {
					if( faction.MainBase != null ) {
						faction.MainBase.FindConstructionSite();
						//if( faction.MainBase.ConstructionSite != null )
							//faction.MainBase.ConstructionSite.ClearConstructionStockpile(null);
					}
					faction.DetermineTechTier();
					faction.DetermineNextGoal();

				}
			}

			Loaded = true;

    }

		public static Faction GetFactionByFounder( string founder ) {
			foreach( Faction f in SCFactions) {
				if( f.Founder == null ) continue;
				if( f.Founder.DisplayName == founder )
					return f;
			}

			return null;
		}

		public static Faction GetFaction( long owner ) {
			foreach( Faction f in SCFactions) {
				if( f.MyFaction == null ) continue;
				if( f.MyFaction.FounderId == owner )
					return f;
			}

			return null;
		}

		public static Faction GetFactionContaining( long member ) {
			foreach( Faction f in SCFactions) {
				if( f.MyFaction == null ) continue;
				if( f.MyFaction.IsMember(member) )
					return f;
			}

			return null;
		}

		public static Faction CreateIfNotExists( string tag, MyCommandLine cmd ) {
			Faction faction = GetFaction(tag);
			if( faction != null ) return faction;

			faction = new Faction{
				Name = tag,
				MyFaction = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag),
				CommandLine = cmd
			};

			if( cmd.Switch("toss") ) faction.Race = Races.Protoss;
			if( cmd.Switch("zerg") ) {
				faction.Race = Races.Zerg;
				if( !faction.Resources.Contains("Organic") )
					faction.Resources.Add("Organic");
			}
			if( cmd.Switch("hybrid") ) {
				faction.Race = Races.Hybrid;
				if( !faction.Resources.Contains("Organic") )
					faction.Resources.Add("Organic");
				faction.Resources.Add("Silver");
        faction.Resources.Add("Gold");
        faction.Resources.Add("Cobalt");
        faction.Resources.Add("Magnesium");
        faction.Resources.Add("Uranium");
        faction.Resources.Add("Platinum");
				faction.Tier = Tech.Space;
			}
			if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
				string[] colors = cmd.Argument(2).Split(',');
				faction.Color = new SerializableVector3(float.Parse(colors[0]),float.Parse(colors[1]),float.Parse(colors[2]));
					//RequestNewPlayer (int serialNumber, string playerName, string characterModel)
					//LoadIdentities (List< MyObjectBuilder_Identity > list)
			}

			SCFactions.Add(faction);

			// Make peace with existing factions
			MyObjectBuilder_FactionCollection fc = MyAPIGateway.Session.Factions.GetObjectBuilder();
			foreach( MyObjectBuilder_Faction ob in fc.Factions ) {
				if( faction.MyFaction.FactionId == ob.FactionId ) continue;
				MyAPIGateway.Session.Factions.SendPeaceRequest(faction.MyFaction.FactionId, ob.FactionId);
				MyAPIGateway.Session.Factions.AcceptPeace(faction.MyFaction.FactionId, ob.FactionId);
			}
			return faction;
		}

		public static Faction GetFaction( string tag ) {
			foreach( Faction f in SCFactions) {
				if( f.Name == tag ) {
					return f;
				}
			}
			return null;
		}

		// public void NewPlayerAdded( int playerId ) {
		// 	MyAPIGateway.Utilities.ShowMessage( "NewPlayerAdded", playerId.ToString() );
		// 	IMyPlayer player = MyAPIGateway.Players.GetPlayerById(playerId);
		// 	foreach( Faction faction in SCFactions ) {
		// 		if( !faction.CommandLine.Switch("aggressive") ) {
		// 			faction.SetReputation(player.PlayerID);
		// 		}
		// 	}
		// }

		// public void IncreaseStat( IMyCharacter character, MyStringHash id) {
		// 	if( character == null ) return;
		// 	MyEntityStatComponent comp = character.Components.Get<MyEntityStatComponent>();
		// 	if( comp == null ) return;
		//
		// 	MyEntityStat stat;
    //   if(!Stats.TryGetValue(id, out stat)) return;
		//
		// 	stat.MaxValue += amount;
		// }

		public void SetReputation(long playerID) {
			foreach( Faction faction in SCFactions ) {
				if( !faction.CommandLine.Switch("aggressive") ) {
					faction.SetReputation(playerID);
				}
			}
		}

		public void SpawnFactions() {
			if( !MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.ControlledObject == null ) return;
			if( ClosestPlanet == null ) ClosestPlanet = GetClosestPlanet( MyAPIGateway.Utilities.IsDedicated ? Vector3D.Zero : MyAPIGateway.Session.Player.GetPosition() );

			if( !MyAPIGateway.Utilities.IsDedicated ) {
				SetReputation(MyAPIGateway.Session.Player.PlayerID);
			}

			foreach( Faction faction in SCFactions ) {

				if( ClosestPlanet == null ) {
					Vector3D position = MyAPIGateway.Session.Player.GetPosition();
					position.Y -= 500;
					position.Z += 100;
					faction.Spawn(position);
				} else {
					faction.Spawn(Vector3D.Zero);
				}

				// faction.Spawn(position);
				//faction.Spawn(Vector3D.Zero);
			}

			Convars.Static.Spawned = Spawned = true;
			Convars.Static.Save();
			//MyAPIGateway.Utilities.SetVariable<bool>("SC-Spawned", true);
		}


		public static IMyPlayer GetPlayer( ulong id ) {
			List<IMyPlayer> players = new List<IMyPlayer>();
			MyAPIGateway.Players.GetPlayers(players);
			foreach( IMyPlayer player in players ) {
				if( player.SteamUserId == id ) return player;
			}
			return null;
		}

		public static IMyPlayer GetPlayer( long id ) {
			List<IMyPlayer> players = new List<IMyPlayer>();
			MyAPIGateway.Players.GetPlayers(players);
			foreach( IMyPlayer player in players ) {
				if( player.PlayerID == id || player.IdentityId == id ) return player;
			}
			return null;
		}



  }
}
