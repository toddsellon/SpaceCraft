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

    public override void Init(MyObjectBuilder_SessionComponent session) {
      base.Init(session);

      //SaveName = MyAPIGateway.Session.Name;

      //MyLog.Default.WriteLineAndConsole("SpaceCraftSession Init()");
      //MyAPIGateway.Utilities.ShowMessage("SC:","SpaceCraftSession Init()");
      MyAPIGateway.Utilities.ShowNotification("SpaceCraftSession Init()");

			MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out Spawned);
			Server = MyAPIGateway.Multiplayer.IsServer;
			Loaded = !Server;
			//MyAPIGateway.Utilities.IsDedicated;
    }

    public void LoadFactions() {

			//var planetDefList = MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions();

			//foreach(var mod in MyAPIGateway.Session.Mods) {}

			/*foreach(var id in MyAPIGateway.Session.Factions.Factions.Keys) {
			}*/

      ListReader<MySpawnGroupDefinition> groups = MyDefinitionManager.Static.GetSpawnGroupDefinitions();

      foreach(MySpawnGroupDefinition group in groups ){
				if(group.Enabled == false){
					continue;
				}

				MyCommandLine cmd = new MyCommandLine();


        if (cmd.TryParse(group.DescriptionText) && cmd.Argument(0).ToLower() == "spacecraft") {

					//MyAPIGateway.Utilities.ShowNotification("Found: " + group.DescriptionText);

          string Name = cmd.Argument(1).ToUpper();

          if( Name != String.Empty ) {

						Faction faction = GetFaction( Name );
						faction.CommandLine = cmd;
						faction.Groups.Add( group );


          }
        }




				if( !Spawned ) {
					SpawnFactions();
				}

				//MyDefinitionManager.Static.GetPrefabDefinition();
				//MyAPIGateway.Session.Factions.TryGetFactionByTag(
				//MyAPIGateway.PrefabManager.SpawnPrefab(


      }

			Loaded = true;

    }

		public Faction GetFaction( string Name ) {
			if( !MyAPIGateway.Session.Factions.FactionTagExists(Name) ) {
				MyAPIGateway.Session.Factions.CreateFaction(0,Name,"SpaceCraft Faction","Description","Private Info");
			}
			foreach( Faction f in Factions) {
				if( f.Name == Name ) {
					return f;
				}
			}
			Faction faction = new Faction{
				Name = Name
			};
			Factions.Add(faction);
			return faction;
		}

		public void SpawnFactions() {
			foreach( Faction faction in Factions ) {
				//var position = MyAPIGateway.Session.Player.GetPosition() + new Vector3D(0,100,0);
				Vector3D position = MyAPIGateway.Session.Player.GetPosition();
				position.Y -= 1000;

				//CubeGrid grid = faction.AddPrefab("TerranPlanetPod", MatrixD.CreateWorld(position) );
				faction.AddControllable( new CubeGrid(){
					Grid = CubeGrid.Spawn("TerranPlanetPod", MatrixD.CreateWorld(position))
				} as Controllable );

				position.X += 5;
				//Engineer e = faction.AddEngineer( MatrixD.CreateWorld(position) );
				faction.AddControllable( new Engineer(){
					Character = Engineer.Spawn(MatrixD.CreateWorld(position))
				} as Controllable );

				//matrix.Translation = new Vector3D(0,1,0);
				//string prefabName = faction.Groups[0].Prefabs[0].SubtypeId;
				/*string prefabName = "TerranPlanetPod";

				//Matrix matrix = Vector3D.Transform(new Vector3D(0,100,0), MatrixD.CreateWorld(position));
				MyPrefabDefinition prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);

				if( prefab == null ) {
					MyAPIGateway.Utilities.ShowNotification("Prefab not found");
					return;
				}

				foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
					MyAPIGateway.Utilities.ShowNotification("Trying to create: " + grid.DisplayName);
					MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

					if( entity == null ) {
						MyAPIGateway.Utilities.ShowNotification("Failed to create entity");
						return;
					}

					entity.Flags &= ~EntityFlags.Save;
					//ent.Flags &= ~EntityFlags.NeedsUpdate;

					entity.Render.Visible = true;
					entity.WorldMatrix = matrix;
					//entity.PositionComp.SetPosition(new Vector3D(10,0,0));
					MyAPIGateway.Entities.AddEntity(entity);

					Engineer e = faction.AddEngineer( entity.WorldMatrix );
				}
				//MyAPIGateway.PrefabManager.AddShipPrefab(prefabName, matrix);
				//MyAPIGateway.PrefabManager.SpawnPrefab( prefabName, position, Vector3.Forward, Vector3.Up, Vector3.Zero, Vector3.Zero, null, SpawningOptions.None, 0, false, null );
				//MyAPIGateway.Utilities.ShowNotification("Added to " + Name + " " + group.DescriptionText);

				//var bot = CreatePlayer();
				//bot.Spawn
				*/
				Spawned = true;
				MyAPIGateway.Utilities.SetVariable<bool>("SC-Spawned", true);
			}
		}

    public override void UpdateBeforeSimulation() {

			if( !Loaded ) {
				LoadFactions();
			}

			if( !Server ) return;

			//MyAPIGateway.Utilities.ShowNotification("SpaceCraftSession::UpdateBeforeSimulation()");

			foreach( Faction faction in Factions ) {
				faction.UpdateBeforeSimulation();
			}

			Tick++;
      if( Tick % 10 == 0 ) UpdateBeforeSimulation10();
      if( Tick == 100 ) {
        UpdateBeforeSimulation100();
        Tick = 0;
      }

      /*if(SaveName != MyAPIGateway.Session.Name) {
        // Saved
        SaveName = MyAPIGateway.Session.Name;
				Settings.General.SaveSettings(Settings.General);
      }*/
    }

		public void UpdateBeforeSimulation10() {
			if( !Server ) return;
			foreach( Faction faction in Factions ) {
				faction.UpdateBeforeSimulation10();
			}
		}

		public void UpdateBeforeSimulation100() {
			if( !Server ) return;
			foreach( Faction faction in Factions ) {
				faction.UpdateBeforeSimulation100();
			}
		}


  }
}
