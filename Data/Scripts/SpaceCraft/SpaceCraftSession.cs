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
    public List<Faction> Factions = new List<Faction>();
		public static List<MyPlanet> Planets = new List<MyPlanet>();
		public static MyPlanet ClosestPlanet { get; protected set; }
		protected MyObjectBuilder_SessionComponent Session;
    public override void Init(MyObjectBuilder_SessionComponent session) {
      base.Init(session);
			Session = session;
			MyAPIGateway.Utilities.ShowNotification("SpaceCraft Session Init()" );
      SaveName = MyAPIGateway.Session.Name;


			MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out Spawned);
			Server = MyAPIGateway.Multiplayer.IsServer;
			Loaded = !Server;
			//MyAPIGateway.Utilities.IsDedicated;
    }

		public static MyPlanet GetClosestPlanet( Vector3D position ) {
			// planet.PositionLeftBottomCorner

			MyPlanet best = null;
			double bestDistance = 0.0f;
			double distance = 0.0f;
			foreach( MyPlanet planet in Planets ) {
				distance = Vector3D.Distance(position, planet.PositionLeftBottomCorner + (planet.SizeInMetres / 2));
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
				}

				// Remove Pirates
				if( entity is IMyCubeGrid && entity.DisplayName.Substring(0,6) == "Pirate" ) {
					MyAPIGateway.Entities.RemoveEntity(entity);
				}
			}
		}

    public void Preload() {

			ScanEntities();

			//var planetDefList = MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions();

			//foreach(var mod in MyAPIGateway.Session.Mods) {}

			/*foreach(var id in MyAPIGateway.Session.Factions.Factions.Keys) {
			}*/

      ListReader<MySpawnGroupDefinition> groups = MyDefinitionManager.Static.GetSpawnGroupDefinitions();

			foreach(MySpawnGroupDefinition group in groups ){
				//if(group.Enabled == false || String.IsNullOrWhiteSpace(group.DescriptionText) ) continue;
				if( String.IsNullOrWhiteSpace(group.DescriptionText) ) continue;

				//MyAPIGateway.Utilities.ShowMessage( "Preload", group.DescriptionText );
				MyCommandLine cmd = new MyCommandLine();


        if (cmd.TryParse(group.DescriptionText) && cmd.Argument(0).ToLower() == "spacecraft") {

          string Name = cmd.Argument(1).ToUpper();

          if( Name != string.Empty ) {

						Faction faction = GetFaction( Name, group.Id.SubtypeId.ToString() );
						faction.CommandLine = cmd;
						if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
							string[] colors = cmd.Argument(2).Split(',');
							faction.Color = new SerializableVector3(float.Parse(colors[0]),float.Parse(colors[1]),float.Parse(colors[2]));
						}
						faction.Groups.Add( group );


          }
        }

				//MyDefinitionManager.Static.GetPrefabDefinition();
				//MyAPIGateway.Session.Factions.TryGetFactionByTag(
				//MyAPIGateway.PrefabManager.SpawnPrefab(


      }

			Loaded = true;

    }

		public Faction GetFaction( string tag, string name ) {
			if( !MyAPIGateway.Session.Factions.FactionTagExists(tag) ) {
				MyAPIGateway.Session.Factions.CreateFaction(0,tag,name,"Description","Private Info");
			}
			foreach( Faction f in Factions) {
				if( f.Name == tag ) {
					return f;
				}
			}
			Faction faction = new Faction{
				Name = tag
			};

			if( faction == null ) {
				MyAPIGateway.Utilities.ShowMessage( "GetFaction", tag + " faction was null..." );
			}

			Factions.Add(faction);
			//faction.Init(Session);
			return faction;
		}

		public void SpawnFactions() {
			//if( MyAPIGateway.Session.ControlledObject == null ) return;
			if( ClosestPlanet == null ) ClosestPlanet = GetClosestPlanet( MyAPIGateway.Session.Player.GetPosition() );



			foreach( Faction faction in Factions ) {
				//var position = MyAPIGateway.Session.Player.GetPosition() + new Vector3D(0,100,0);

				//MyAPIGateway.Utilities.ShowMessage( "SpawnFactions", "Spawning " + faction.Name + " faction" );

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
