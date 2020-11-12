using System;
using System.Collections.Generic;
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

namespace SpaceCraft {

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class SpaceCraftSession:MySessionComponentBase {

		public int Tick = 0;
    public string SaveName;
		public bool Loaded = false;
		public bool Spawned = false;
		public bool Server = false;
    public static List<Faction> Factions = new List<Faction>();
		public static List<MyPlanet> Planets = new List<MyPlanet>();
		public static MyPlanet ClosestPlanet { get; protected set; }
		protected MyObjectBuilder_SessionComponent Session;
		public static CLI MyCLI;
    public override void Init(MyObjectBuilder_SessionComponent session) {
      base.Init(session);
			Session = session;
			Limits.Speed = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;

			MyCLI = new CLI();


			Server = MyAPIGateway.Multiplayer.IsServer;
			Loaded = !Server;
			//MyAPIGateway.Utilities.IsDedicated;

			if( Server ) {
				MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out Spawned);
				SaveName = MyAPIGateway.Session.Name;
				MyAPIGateway.Session.SessionSettings.EnableRemoteBlockRemoval = false;
				//IMyDamageSystem
				//MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler (int priority, Action< object, MyDamageInformation > handler);
				if( !Spawned ) {
					SetDefaultValues();
				}


			}
    }

		public SetDefaultValues() {
			MyAPIGateway.Utilities.SetVariable<int>("SC-Grids", 20);
			MyAPIGateway.Utilities.SetVariable<int>("SC-Difficulty", 1);
			MyAPIGateway.Utilities.SetVariable<int>("SC-Engineers", 1);
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
				distance = Vector3D.Distance(position, planet.PositionLeftBottomCorner + (planet.SizeInMetres / 2));
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

			foreach( IMyEntity entity in entities ) {
				if( entity is MyPlanet ) {
					Planets.Add( entity as MyPlanet );
					continue;
				}


				if( entity is IMyCubeGrid ) {
					// Remove Pirates
					if( entity.DisplayName.Substring(0,6) == "Pirate" ) {
						MyAPIGateway.Entities.RemoveEntity(entity);
						continue;
					}


					IMyCubeGrid grid = entity as IMyCubeGrid;
					List<long> owners = grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners : grid.SmallOwners;
					foreach(long owner in owners) {
						Faction faction = GetFaction(owner);
						if( faction != null ) {
							CubeGrid g = faction.TakeControl( new CubeGrid(grid) ) as CubeGrid;
							faction.MainBase = g;
							g.CheckFlags();
							g.FindConstructionSite();
							if( g.IsStatic )
								faction.Colonize( GetClosestPlanet(g.Entity.WorldMatrix.Translation) );
							break;
						}
					}

				}

				if( entity is IMyCharacter ) {
					IMyCharacter character = entity as IMyCharacter;
					//Faction faction = GetFactionByFounder(entity.DisplayName);
					if( character.IsBot ) {
						Faction faction = GetFaction(entity.DisplayName);
						if( faction != null ) {
							faction.TakeControl( new Engineer(faction, entity as IMyCharacter) );
						}
					}
				}
			}
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

        if( cmd.TryParse(group.DescriptionText) && cmd.Argument(0).ToLower() == "spacecraft") {

					string Name = cmd.Argument(1) ?? String.Empty;

					foreach( 	MySpawnGroupDefinition.SpawnGroupPrefab prefab in group.Prefabs ) {
						if( first == String.Empty ) first = prefab.SubtypeId;

						if( prefab.SubtypeId != "TerranPlanetPod" )
							Prefab.Add(prefab.SubtypeId, Name);
					}

          if( !String.IsNullOrWhiteSpace(Name) ) {

						Name = Name.ToUpper();

						Faction faction = GetFaction( Name, group.Id.SubtypeId.ToString() );
						faction.CommandLine = cmd;
						if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
							string[] colors = cmd.Argument(2).Split(',');
							faction.Color = new SerializableVector3(float.Parse(colors[0]),float.Parse(colors[1]),float.Parse(colors[2]));
								//RequestNewPlayer (int serialNumber, string playerName, string characterModel)
								//LoadIdentities (List< MyObjectBuilder_Identity > list)
						}
						faction.Groups.Add( group );
						faction.SpawnPrefab = first;
						if( Spawned )
							faction.DetermineNextGoal();
          }
        }

      }

			List<IMyIdentity> identities = new List<IMyIdentity>();
			MyAPIGateway.Players.GetAllIdentites(identities);
			foreach( IMyIdentity identity in identities) {
				//MyAPIGateway.Utilities.ShowMessage( "identity", identity.DisplayName + " " + identity.ColorMask.ToString() );
				foreach( Faction faction in Factions ) {
					if( faction.MyFaction == null ) continue;
					if( faction.MyFaction.IsFounder(identity.IdentityId) ) {
						faction.Founder = identity;
						//faction.Founder.SetColorMask((Vector3)faction.Color);
						//identity.ColorMask = faction.Color;
						break;
					}
				}
			}

			ScanEntities();

			Loaded = true;

    }

		public static Faction GetFactionByFounder( string founder ) {
			foreach( Faction f in Factions) {
				if( f.Founder == null ) continue;
				if( f.Founder.DisplayName == founder )
					return f;
			}

			return null;
		}

		public static Faction GetFaction( long owner ) {
			foreach( Faction f in Factions) {
				if( f.MyFaction == null ) continue;
				if( f.MyFaction.FounderId == owner )
					return f;
			}

			return null;
		}

		public static Faction GetFactionContaining( long member ) {
			foreach( Faction f in Factions) {
				if( f.MyFaction == null ) continue;
				if( f.MyFaction.IsMember(member) )
					return f;
			}

			return null;
		}

		public static Faction GetFaction( string tag, string name = "" ) {
			// MyAPIGateway.Session.Factions.TryGetFactionByTag(
			// if( !MyAPIGateway.Session.Factions.FactionTagExists(tag) ) {
			// 	MyAPIGateway.Session.Factions.CreateFaction(0,tag,name,"Description","Private Info");
			// }
			foreach( Faction f in Factions) {
				if( f.Name == tag ) {
					return f;
				}
			}


			MyAPIGateway.Session.Factions.CreateFaction(0,tag,name,"Description","Private Info");

			Faction faction = new Faction{
				Name = tag,
				MyFaction = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag)
			};

			if( faction.MyFaction == null ) {
				MyAPIGateway.Utilities.ShowMessage( "TryGetFactionByTag", "Failed for " + tag );
			}

			Factions.Add(faction);
			//faction.Init(Session);
			return faction;
		}

		public void SpawnFactions() {
			if( MyAPIGateway.Session.ControlledObject == null ) return;
			if( ClosestPlanet == null ) ClosestPlanet = GetClosestPlanet( MyAPIGateway.Session.Player.GetPosition() );



			foreach( Faction faction in Factions ) {

				if( ClosestPlanet == null ) {
					MyAPIGateway.Utilities.ShowMessage( "SpawnFactions", "Could not find closest planet" );
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

			Spawned = true;
			MyAPIGateway.Utilities.SetVariable<bool>("SC-Spawned", true);
		}

    public override void UpdateBeforeSimulation() {

			if( !Server ) return;

			if( !Loaded ) {
				Preload();
			}

			if( !Spawned ) {
				SpawnFactions();
			}

			foreach( Faction faction in Factions ) {
				faction.UpdateBeforeSimulation();
			}

      /*if(SaveName != MyAPIGateway.Session.Name) {
        // Saved
        SaveName = MyAPIGateway.Session.Name;
				Settings.General.SaveSettings(Settings.General);
      }*/
    }



  }
}
