using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage.Game.ModAPI;
using SpaceCraft;
using SpaceCraft.Utils;
using VRage;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRage.Game.Components;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.AI;
using Sandbox.Game.World;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Components;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;

namespace SpaceCraft.Utils {

  public enum QuestId : ushort {
    StartingQuest,
    FindTemple,
    FindStation,
    Drill,
    Grinder,
    Welder,
    Finale
	};

  public enum QuestState : ushort {
    Pending,
		Started,
    Commencing,
    Challenge,
    Completed
	};

  public enum QuestGrid : ushort {
    ProtossTemple,
    ResearchStation,
    DominionLab
	};

  public sealed class Quest {
    public ulong SteamUserId;
    public QuestId Id = QuestId.StartingQuest;
    public QuestState State = QuestState.Pending;
    public SerializableVector3D? Position;
    public float Range = 10f;
    public List<long> Enemies;

    public void Progress() {
      if( State < QuestState.Completed )
        State++;
    }

    public void Complete() {
      State = QuestState.Completed;
    }
  }

  public sealed class Quests {

    private static Quest CurrentQuest;
    private static List<IMyCubeGrid> SpawnedGrids = new List<IMyCubeGrid>();
    private static readonly float PlayerDistance = 10000;

    private static Quests instance;

    public static Quests Static
    {
      get {
        if( instance == null ) {
          if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Quests)) ) {
            instance = Open() ?? new Quests();
          } else {
            instance = new Quests();
          }
          Init();
        }
        return instance;
      }
    }


    public float Version = TrunkVersion;
    public List<Quest> Log = new List<Quest>();

    protected static string File = "SCQuests.xml";
    protected static readonly float TrunkVersion = 1.0f;
    public static readonly Guid GuidQuest = new Guid("50977CCF-2DC8-4839-AA38-0509FE383D0B");

    private static Dictionary<QuestId,Action<Quest>> Actions = new Dictionary<QuestId,Action<Quest>> {
      {QuestId.StartingQuest,UpdateStartingQuest},
      {QuestId.FindTemple,UpdateSecondQuest},
      {QuestId.FindStation,UpdateThirdQuest},
      {QuestId.Drill,UpdateDrillQuest},
      {QuestId.Grinder,UpdateGrinderQuest},
      {QuestId.Welder,UpdateWelderQuest},
      {QuestId.Finale,UpdateFinalQuest}
    };

    // private static Dictionary<QuestGrid,string> Prefabs = new Dictionary<QuestGrid,string> {
    //   {QuestGrid.ProtossTemple,"Protoss Temple"},
    //   {QuestGrid.ResearchStation,"Research Station"}
    // };

    protected static Random Randy = new Random();

    private static IMyFaction MyFaction;

    public static Dictionary<Races,List<MyDefinitionId>> Technology = new Dictionary<Races,List<MyDefinitionId>> {
      {Races.Zerg,new List<MyDefinitionId>{
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "ZergHeart"),
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallZergHeart"),
        new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeZergAssembler"),
        new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "EvolutionChamber"),
        new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeZergRefinery"),
        new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "ZergAssembler"),
        new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "ZergSurvivalKit"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallZergThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallZergThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SunkenColony"),
        new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "LargeZergGyro"),
        new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "SmallZergGyro"),
        new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), "SporeColony"),
        new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), "SmallSporeColony"),
        new MyDefinitionId(typeof(MyObjectBuilder_Drill), "SmallDrillingClaws"),
        new MyDefinitionId(typeof(MyObjectBuilder_Drill), "LargeDrillingClaws"),
        new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "LargeZergRemoteControl"),
        new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "SmallZergRemoteControl")
      }},
      {Races.Protoss,new List<MyDefinitionId>{
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "LargeProtossShield"),
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallProtossShield"),
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "LargePylon"),
        new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallPylon"),
        new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), "SmallPhotonCannon"),
        new MyDefinitionId(typeof(MyObjectBuilder_InteriorTurret), "PhotonCannon"),
        new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeProtossAssembler"),
        new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeProtossRefinery"),
        new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "BasicProtossAssembler"),
        new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "ProtossSurvivalKitLarge"),
        new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "ProtossSurvivalKit"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallProtossThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeProtossThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallProtossThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeProtssThrust"),
        new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeScarabLauncher"),
        new MyDefinitionId(typeof(MyObjectBuilder_Drill), "SmallParticleBeam"),
        new MyDefinitionId(typeof(MyObjectBuilder_Drill), "LargeParticleBeam"),

      }},
      {Races.Hybrid,new List<MyDefinitionId>{
        new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeHybridRefinery"),
        new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeHybridAssembler")

      }}
    };

    private static void Init() {
      MyFaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("????");


    }

    public static void Reset() {
      Static.Log = new List<Quest>();

      if( !Convars.Static.Quests ) return;

      LockTechnology();

      List<IMyPlayer> players = new List<IMyPlayer>();
			MyAPIGateway.Players.GetPlayers(players);

      foreach(IMyPlayer p in players) {
        Static.SetQuestState(p.SteamUserId,QuestId.StartingQuest);
        // LockTechnology( p.PlayerID, Technology[Races.Protoss] );
      }
    }

    private static Quest SpawningQuest;

    public static void SpawnPrefab(string prefabName, MatrixD position, Action callback, SpawningOptions options = SpawningOptions.None ) {
      SpawnedGrids = new List<IMyCubeGrid>();

			//options |= SpawningOptions.RotateFirstCockpitTowardsDirection;
			// options |= SpawningOptions.UseGridOrigin;
      try {
        MyAPIGateway.PrefabManager.SpawnPrefab(SpawnedGrids, prefabName, position.Translation, position.Forward, position.Up, Vector3.Zero, Vector3.Zero, null, options, MyFaction.FounderId, false, callback );
      }catch(Exception exc){
      }
    }


    private static readonly Dictionary<QuestGrid,Action> SpawnCallbacks = new Dictionary<QuestGrid,Action> {
      { QuestGrid.ResearchStation, ResearchStationSpawned },
      { QuestGrid.DominionLab, DominionLabSpawned }
    };


    private static IMyCubeGrid CreateQuestGrid( QuestGrid type, MyPlanet closest ) {
      IMyCubeGrid g = null;


      Vector3D position = Vector3D.Zero;
      Vector3D up = Vector3D.Zero;
      Vector3 p = Vector3.Zero;
      string prefabName = String.Empty;
      if( closest == null ) return null;

      List<IMyEntity> entities = null;
      bool safe = false;

      switch( type ) {
        case QuestGrid.ProtossTemple:
          MyPlanet prev = SpaceCraftSession.GetClosestPlanet( closest.WorldMatrix.Translation, new List<MyPlanet>{closest} ) ?? closest;
          MyPlanet next = SpaceCraftSession.GetClosestPlanet( closest.WorldMatrix.Translation, new List<MyPlanet>{closest,prev} ) ?? closest;

          while( !safe ) {
            p = new Vector3(Randy.Next(-next.Size.X,next.Size.X),Randy.Next(-next.Size.Y,next.Size.Y),Randy.Next(-next.Size.Z,next.Size.Z)) + next.WorldMatrix.Translation;
            position = next.GetClosestSurfacePointGlobal(p);
            up = Vector3D.Normalize(position - next.WorldMatrix.Translation);

            if( !Faction.IsFlat(position,next) ) continue;

            BoundingSphereD sphere = new BoundingSphereD( position, 150 );
            entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
            safe = !Faction.ContainsGrids(entities);

          }
          prefabName = "Protoss Temple";
          break;
        case QuestGrid.ResearchStation:
          prefabName = "Research Station";

          while( !safe ) {
            p = new Vector3(Randy.Next(-closest.Size.X,closest.Size.X),Randy.Next(-closest.Size.Y,closest.Size.Y),Randy.Next(-closest.Size.Z,closest.Size.Z)) + closest.WorldMatrix.Translation;
            position = closest.GetClosestSurfacePointGlobal(p);
            up = Vector3D.Normalize(position - closest.WorldMatrix.Translation);
            position += up * 7000000;

            BoundingSphereD sphere = new BoundingSphereD( position, 150 );
            entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
            safe = entities.Count == 0;
          }
          break;
        case QuestGrid.DominionLab:

          while( !safe ) {
            p = new Vector3(Randy.Next(-closest.Size.X,closest.Size.X),Randy.Next(-closest.Size.Y,closest.Size.Y),Randy.Next(-closest.Size.Z,closest.Size.Z)) + closest.WorldMatrix.Translation;
            position = closest.GetClosestSurfacePointGlobal(p);
            up = Vector3D.Normalize(position - closest.WorldMatrix.Translation);
            position += up * 5000000;

            BoundingSphereD sphere = new BoundingSphereD( position, 150 );
            entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

            safe = entities.Count == 0;
          }
          break;
      }

      MatrixD matrix = MatrixD.CreateWorld( position, Vector3D.CalculatePerpendicularVector(up), up );

      if( type != QuestGrid.ProtossTemple ) {
        // SpawnPrefab(prefabName, matrix, DominionLabSpawned );
        SpawnPrefab(prefabName, matrix, SpawnCallbacks[type] );

        return null;
      }

      MyPrefabDefinition prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);

      if( prefab == null ) return null;

      // MatrixD translateToOriginMatrix = prefab.CubeGrids[0].PositionAndOrientation.HasValue ? MatrixD.CreateWorld(-(Vector3D)prefab.CubeGrids[0].PositionAndOrientation.Value.Position, Vector3D.Forward, Vector3D.Up) : MatrixD.CreateWorld(-prefab.BoundingSphere.Center, Vector3D.Forward, Vector3D.Up);
      // MatrixD translateToOriginMatrix = MatrixD.CreateWorld(-prefab.BoundingSphere.Center, Vector3D.Forward, Vector3D.Up);
      int index = 0;
      foreach( MyObjectBuilder_CubeGrid grid in prefab.CubeGrids ) {
        // MatrixD originalGridMatrix = grid.PositionAndOrientation.HasValue ? grid.PositionAndOrientation.Value.GetMatrix() : MatrixD.Identity;
				// MatrixD newWorldMatrix = MatrixD.Multiply(originalGridMatrix, MatrixD.Multiply(translateToOriginMatrix, matrix));
        // MatrixD newWorldMatrix = index == 0 ? matrix : MatrixD.Multiply(originalGridMatrix, matrix);
        IMyCubeGrid cg = null;
        grid.EntityId = (long)0;
        foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
          block.EntityId = (long)0;
          block.Owner = MyFaction.FounderId;
					block.BuiltBy = MyFaction.FounderId;
        }

        MyEntity entity = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(grid);

        if( entity == null ) {
          return null;
        }

        entity.Storage = new MyModStorageComponent();
        entity.Storage.Add(GuidQuest,type.ToString());

        entity.Flags &= ~EntityFlags.Save;
				entity.Save = true;


        entity.Render.Visible = true;
        entity.WorldMatrix = matrix;
        // entity.WorldMatrix = newWorldMatrix;
        MyAPIGateway.Entities.AddEntity(entity);

        cg = entity as IMyCubeGrid;

				if( cg != null ) {
	        cg.ChangeGridOwnership(MyFaction.FounderId, MyOwnershipShareModeEnum.None);
        }

        g = g ?? cg;
        index++;
      }

      return g;


    }

    // Special thanks to https://ttsmp3.com/

    private static IMyCubeGrid GetQuestGrid( QuestGrid type ) {
      string name = type.ToString();
      HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);

			foreach( IMyEntity entity in entities ) {
        IMyCubeGrid grid = entity as IMyCubeGrid;
        if( grid == null || grid.Storage == null || !grid.Storage.ContainsKey(GuidQuest) ) continue;
        if( grid.Storage[GuidQuest] == name )
          return grid;
      }

      return null;
    }

    private static IMyCubeGrid CreateIfNotExists( QuestGrid type, MyPlanet closest ) {
      IMyCubeGrid grid = GetQuestGrid( type );
      if( grid == null ) {
        grid = CreateQuestGrid(type, closest);
      }
      return grid;
    }

    public void HitCheck() {
      foreach( Quest quest in Log.ToList() ) {
        if( quest.State == QuestState.Completed ) continue;
        if( quest.Position.HasValue ) {
          IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
          if( player == null || Vector3D.Distance(player.GetPosition(),(Vector3D)quest.Position) > quest.Range ) continue;
          quest.Progress();
          UpdateQuest(quest);
          Save();
        } else if( quest.Enemies != null ) {
          bool changed = false;
          foreach( long id in quest.Enemies.ToList() ) {
            IMyEntity entity;
            if( !MyAPIGateway.Entities.TryGetEntityById(id, out entity) || entity.Closed || entity.MarkedForClose ) {
              quest.Enemies.Remove(id);
              changed = true;
              continue;
            }
            IMyCubeGrid grid = entity as IMyCubeGrid;
            if( grid == null ) continue;

            if( !MyVisualScriptLogicProvider.HasPower(grid.EntityId.ToString())  ) {
              quest.Enemies.Remove(id);
              changed = true;
            }
          }
          if( changed && quest.Enemies.Count == 0 ) {
            quest.Enemies = null;
            quest.Progress();
            UpdateQuest(quest);
          }
          if( changed ) Save();
        }
      }
    }

    public static void RemoveGPSByName( string name, long identityId ) {
      List<IMyGps> list =	MyAPIGateway.Session.GPS.GetGpsList(identityId);
      foreach(IMyGps gps in list) {
        if( gps.Name == name )
          MyAPIGateway.Session.GPS.RemoveGps(identityId, gps);
      }

    }

    public Quest GetQuest( ulong playerId, QuestId questId ) {
      foreach( Quest quest in Log ) {
        if( quest.SteamUserId == playerId && quest.Id == questId )
          return quest;
      }

      return null;
    }

    public void SetQuestState( ulong playerId, QuestId questId, QuestState state = QuestState.Pending ) {
      Quest quest = GetQuest(playerId,questId);
      if( quest == null ) {
        quest = new Quest {
          SteamUserId = playerId,
          Id = questId,
          State = state
        };
        Log.Add(quest);
      } else {
        quest.State = state;
      }
      UpdateQuest(quest);
      Save();
    }

    private static void UpdateQuest( Quest quest ) {
      Actions[quest.Id](quest);
    }

    private static bool PositionIsFree( Vector3D position, float radius = 150 ) {
      BoundingSphereD sphere = new BoundingSphereD( position, radius );
      List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
      return !Faction.ContainsGrids(entities);
    }

    private static void UpdateStartingQuest( Quest quest ) {
      Vector3 p;
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;
      MyPlanet closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition());

      switch( quest.State ) {
        case QuestState.Pending:
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Commander, we're detecting a mysterious signal. I've marked the GPS location for you.",
            SteamUserId = player.SteamUserId,
            Sound = "quest0-pending"
          });

          p = new Vector3(Randy.Next(-closest.Size.X,closest.Size.X),Randy.Next(-closest.Size.Y,closest.Size.Y),Randy.Next(-closest.Size.Z,closest.Size.Z)) + closest.WorldMatrix.Translation;
          // Vector3 p = new Vector3(Randy.Next(Homeworld.Size.X),Randy.Next(Homeworld.Size.Y),Randy.Next(Homeworld.Size.Z)) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);

          quest.Position = closest.GetClosestSurfacePointGlobal( p );
          IMyGps gps = MyAPIGateway.Session.GPS.Create("Mysterious Signal", "A mysterous signal eminates from this location. Perhaps you should investigage?", quest.Position.Value, true, false);
          MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);
          break;

        case QuestState.Started:
          Vector3D[] offsets = {new Vector3D(10f,10f,0),new Vector3D(0,10f,10f),new Vector3D(10f,0,10f),new Vector3D(-10f,-10f,0),new Vector3D(0,-10f,-10f),new Vector3D(-10f,0,-10f)};
          for( int i = 0; i < 5; i++) {
            // SpaceCraftSession.SpawnBot( "ZergZergling", quest.Position.Value );
            SpaceCraftSession.SpawnBot( "ZergZergling", closest.GetClosestSurfacePointGlobal(quest.Position.Value + offsets[i]) );
          }

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "The Swarm!! Defend yourself or run if you have to!",
            SteamUserId = player.SteamUserId,
            Sound = "quest0-started"
          });

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "The zerg were grouped around wreckage containing a datapad. It seems a Protoss ship was spotted nearby and this brood was escaping its wrath. Your GPS has been updated with its last known location.",
            SteamUserId = player.SteamUserId
          });

          closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition(), new List<MyPlanet>{closest}) ?? closest;

          p = new Vector3(Randy.Next(-closest.Size.X,closest.Size.X),Randy.Next(-closest.Size.Y,closest.Size.Y),Randy.Next(-closest.Size.Z,closest.Size.Z)) + closest.WorldMatrix.Translation;
          // Vector3 p = new Vector3(Randy.Next(Homeworld.Size.X),Randy.Next(Homeworld.Size.Y),Randy.Next(Homeworld.Size.Z)) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);
          //MyVisualScriptLogicProvider.RemoveGPS("Mysterious Signal", player.PlayerID );
          Vector3D old = (Vector3D)(quest.Position.Value);
          quest.Position = closest.GetClosestSurfacePointGlobal( p );
          UpdateGPS(player.PlayerID, old, (Vector3D)(quest.Position.Value));
          // gps = MyAPIGateway.Session.GPS.Create("Mysterious Signal 2", "Mysterious Signal 2", quest.Position.Value, true, false);
          // MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);
          break;

        case QuestState.Commencing:
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Commander, our sensors are picking up a surge of psionic energy. The Protoss are nearby. Perhaps their ship contains important information?",
            SteamUserId = player.SteamUserId,
            Sound = "quest0-commencing"
          });
          Vector3D up = Vector3D.Normalize(quest.Position.Value - closest.WorldMatrix.Translation);
          Vector3D perp = Vector3D.CalculatePerpendicularVector(up);
          CurrentQuest = quest;
          SpawnedGrids = new List<IMyCubeGrid>();

          Vector3D position = Vector3D.Zero;

          bool safe = false;
          while( !safe ) {
            position = closest.GetClosestSurfacePointGlobal((Vector3D)quest.Position.Value + GetRandomVector(1000)) + (up*1000);
            safe = PositionIsFree(position);
          }

          SpawnEnemy("Protoss Scout", MatrixD.CreateWorld( position ) );

          RemoveGPS( player.PlayerID, quest );
          quest.Position = null;

          //MyVisualScriptLogicProvider.SpawnGroup("StartingQuest", quest.Position.Value + (up*2000) + (perp*2000), quest.Position.Value, up, 0);
          break;
        case QuestState.Challenge:
          RemoveGPSByName("Protoss Scout",player.PlayerID);
          Quests.Static.SetQuestState(quest.SteamUserId, QuestId.FindTemple);
          break;
      }
    }

    private static void UpdateSecondQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;
      MyPlanet closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition());
      IMyCubeGrid temple = null;

      switch( quest.State ) {
        case QuestState.Pending:
          temple = CreateIfNotExists(QuestGrid.ProtossTemple,closest);
          if( temple == null ) {
            CLI.SendMessageToClient( new Message {
              Sender = "Error",
              Text = "There was an error creating the Protoss Temple",
              SteamUserId = player.SteamUserId
            });
            return;
          }

          IMyCubeBlock safeZone = GetSafeZone(temple);
          //
          // if( safeZone == null ) {
          //   CLI.SendMessageToClient( new Message {
          //     Sender = "Error",
          //     Text = "There was an finding the Safe Zone",
          //     SteamUserId = player.SteamUserId
          //   });
          //   return;
          // }

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "After analyzing what is left of the Protoss wreckage, it seems they were damaged in combat, but were tracking an ancient temple. I've created a GPS marker at its location.",
            SteamUserId = player.SteamUserId,
            Sound = "quest1-pending"
          });

          quest.Position = temple.WorldMatrix.Translation;
          quest.Range = 2000f;

          IMyGps gps = MyAPIGateway.Session.GPS.Create("Protoss Temple", "The location of a Protoss temple found in the wreckage of a Scout. What could go wrong?", quest.Position.Value, true, false);
          MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);

          break;
        case QuestState.Started:
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "We are close now, commander, but I'm picking up even more Psionic energy than before. Be careful!",
            SteamUserId = player.SteamUserId,
            Sound = "quest1-started"
          });

          Vector3D up = Vector3D.Normalize(quest.Position.Value - closest.WorldMatrix.Translation);
          Vector3D perp = Vector3D.CalculatePerpendicularVector(up);
          CurrentQuest = quest;

          SpawnedGrids = new List<IMyCubeGrid>();
          for( int i = 0; i < 2; i++ ) {
            bool safe = false;
            Vector3D position = Vector3D.Zero;
            while( !safe ) {
              position = closest.GetClosestSurfacePointGlobal((Vector3D)quest.Position.Value + GetRandomVector(2000)) + (up*2000);
              safe = PositionIsFree(position);
            }
            SpawnEnemy("Protoss Scout", MatrixD.CreateWorld( position ) );
          }
          // SpawnEnemy("Protoss Scout", MatrixD.CreateWorld(quest.Position.Value + (up*2000) + (perp*2000)) );
          // SpawnEnemy("Protoss Scout", MatrixD.CreateWorld(quest.Position.Value + (up*2250) + (perp*2250)) );

          quest.Position = null;
          break;
        case QuestState.Commencing:
          RemoveGPSByName("Protoss Scout",player.PlayerID);

          temple = GetQuestGrid(QuestGrid.ProtossTemple);
          if( temple == null ) {
            CLI.SendMessageToClient( new Message {
              Sender = "Error",
              Text = "Protoss temple was not found",
              SteamUserId = player.SteamUserId
            });
            return;
          }

          Unlock( temple );

          quest.Position = temple.WorldMatrix.Translation;
          quest.Range = 10f;

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Now that we've dealt with the Protoss, we can finally enter the temple. Who knows what's inside?",
            SteamUserId = player.SteamUserId,
            Sound = "quest1-commencing"
          });
          break;

        case QuestState.Challenge:
          RemoveGPS( player.PlayerID, quest );
          Quests.Static.SetQuestState(quest.SteamUserId, QuestId.FindStation);
          break;
      }
    }

    private static void ResearchStationSpawned() {
      Quest quest = SpawningQuest;
      SpawningQuest = null;

      if( SpawnedGrids == null || SpawnedGrids.Count == 0 ) {
        return;
      }
      IMyCubeGrid station = SpawnedGrids[0];
      station.Storage = station.Storage ?? new MyModStorageComponent();
      station.Storage.Add(GuidQuest,QuestGrid.ResearchStation.ToString());

      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      MyPlanet closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition());
      // IMyCubeGrid station = CreateIfNotExists(QuestGrid.ResearchStation,closest);

      if( station == null ) {
        CLI.SendMessageToClient( new Message {
          Sender = "Error",
          Text = "There was an error creating the research station",
          SteamUserId = player.SteamUserId
        });
        return;
      }

      IMyCubeBlock safeZone = GetSafeZone(station);

      CLI.SendMessageToClient( new Message {
        Sender = "Adjutant",
        Text = "Facinating. It seems this temple used to be home to a database of advanced Protoss technology. The computer seems to be mostly empty but there is still a psionic link to some of the materials taken.",
        SteamUserId = player.SteamUserId,
        Sound = "quest2-pending"
      });

      // quest.Position = station.WorldMatrix.Translation;
      quest.Position = station.GridIntegerToWorld(safeZone.Position);
      quest.Range = 2000f;

      IMyGps gps = MyAPIGateway.Session.GPS.Create("Psionic Link", "We detected a strong psionic link with components stolen from the Protoss Temple.", quest.Position.Value, true, false);
      MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);

      Static.Save();
    }

    private static void UpdateThirdQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;
      MyPlanet closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition());
      IMyCubeGrid station = null;

      switch( quest.State ) {
        case QuestState.Pending:
          SpawningQuest = quest;
          // SpawningQuest = null;
          station = CreateIfNotExists(QuestGrid.ResearchStation,closest);

          if( station != null ) {
            ResearchStationSpawned();
          }
          // if( station == null ) {
          //   CLI.SendMessageToClient( new Message {
          //     Sender = "Error",
          //     Text = "There was an error creating the research station",
          //     SteamUserId = player.SteamUserId
          //   });
          //   return;
          // }
          //
          // IMyCubeBlock safeZone = GetSafeZone(station);
          //
          // CLI.SendMessageToClient( new Message {
          //   Sender = "Adjutant",
          //   Text = "Facinating. It seems this temple used to be home to a database of advanced Protoss technology. The computer seems to be mostly empty but there is still a psionic link to some of the materials taken.",
          //   SteamUserId = player.SteamUserId,
          //   Sound = "quest2-pending"
          // });
          //
          // // quest.Position = station.WorldMatrix.Translation;
          // quest.Position = station.GridIntegerToWorld(safeZone.Position);
          // quest.Range = 2000f;
          //
          // IMyGps gps = MyAPIGateway.Session.GPS.Create("Psionic Link", "We detected a strong psionic link with components stolen from the Protoss Temple.", quest.Position.Value, true, false);
          // MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);
          break;

        case QuestState.Started:

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Commander, there seems to be an abandoned space station ahead, but we're not alone...",
            SteamUserId = player.SteamUserId,
            Sound = "quest2-started"
          });


          CurrentQuest = quest;

          SpawnedGrids = new List<IMyCubeGrid>();
          for( int i = 0; i < 3; i++ ) {
            bool safe = false;
            Vector3D position = Vector3D.Zero;
            while( !safe ) {
              position = (Vector3D)quest.Position.Value + GetRandomVector(3000);
              safe = PositionIsFree(position);
            }
            SpawnEnemy("Protoss Scout", MatrixD.CreateWorld( position ) );
          }

          // for( int i = 0; i < 3; i++ )
          //   SpawnEnemy("Protoss Scout", MatrixD.CreateWorld( (Vector3D)quest.Position.Value + GetRandomVector(5000) ) );

          quest.Position = null;
          break;

        case QuestState.Commencing:
          RemoveGPSByName("Protoss Scout",player.PlayerID);
          station = GetQuestGrid(QuestGrid.ResearchStation);
          if( station == null ) {
            CLI.SendMessageToClient( new Message {
              Sender = "Error",
              Text = "Research station was not found",
              SteamUserId = player.SteamUserId
            });
            return;
          }

          Unlock( station );

          IMyCubeBlock safeZone = GetSafeZone(station);

          // quest.Position = station.WorldMatrix.Translation;
          quest.Position = station.GridIntegerToWorld(safeZone.Position);
          // quest.Position = station.WorldMatrix.Translation;
          quest.Range = 10f;

          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "I am not detecting any more life signs. It should be safe to head inside but be on your guard.",
            SteamUserId = player.SteamUserId,
            Sound = "quest2-commencing"
          });
          break;
        case QuestState.Challenge:
        RemoveGPS( player.PlayerID, quest );
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Commander, this is it! We've found the missing Protoss technology. Adding the schematics to our library now.",
            SteamUserId = player.SteamUserId,
            Sound = "quest2-challenge"
          });
          UnlockTechnology(player.PlayerID, Technology[Races.Protoss] );


          // Quests.Static.SetQuestState(quest.SteamUserId, QuestId.Drill);
          // Quests.Static.SetQuestState(quest.SteamUserId, QuestId.Grinder);
          // Quests.Static.SetQuestState(quest.SteamUserId, QuestId.Welder);
          break;
      }
    }

    public static List<IMySlimBlock> GetBlocks<t>( IMyCubeGrid grid ) {
			List<IMySlimBlock> list = new List<IMySlimBlock>();
			if( grid == null || grid.Closed ) return list;
			grid.GetBlocks( list );


			if( list.Count > 0 && !(list[0] is t) ) {
				List<IMySlimBlock> ret = new List<IMySlimBlock>();

				foreach( IMySlimBlock block in list ) {
					if( block.FatBlock == null || !(block.FatBlock is t) ) continue;
					ret.Add(block);
				}

				return ret;
			}

			return list;
		}

    private static void DominionLabSpawned() {
      Quest quest = SpawningQuest;
      SpawningQuest = null;
      if( quest == null || SpawnedGrids == null || SpawnedGrids.Count == 0 ) return;

      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      IMyCubeGrid lab = SpawnedGrids[0];

      foreach( IMyCubeGrid grid in SpawnedGrids) {
        MyVisualScriptLogicProvider.SetName(grid.EntityId, grid.EntityId.ToString());
        MyVisualScriptLogicProvider.SetGridDestructible(grid.EntityId.ToString(),false);

        List<IMySlimBlock> b = new List<IMySlimBlock>();
        grid.GetBlocks( b );


        foreach( IMySlimBlock block in b ) {
          if( block.FatBlock is IMyPistonBase || block.FatBlock is IMyDoor || block.FatBlock is IMyCargoContainer || block.FatBlock is IMyMotorStator ) {
            MyCubeBlock cb = block.FatBlock as MyCubeBlock;
            if( cb == null ) continue;
            cb.ChangeOwner(MyFaction.FounderId, MyOwnershipShareModeEnum.All);
            // cb.ChangeBlockOwnerRequest(MyFaction.FounderId, MyOwnershipShareModeEnum.All);
          }
        }
        // grid.ChangeGridOwnership(MyFaction.FounderId, MyOwnershipShareModeEnum.All);
      }

      List<IMySlimBlock> blocks = GetBlocks<IMyProgrammableBlock>(lab);

      quest.Position = blocks.Count == 0 ? lab.WorldMatrix.Translation : lab.GridIntegerToWorld(blocks[0].Position);
      quest.Range = 1000f;

      IMyGps gps = MyAPIGateway.Session.GPS.Create("Dominion Lab", "One Protoss artifact is at this abandoned Dominion lab", quest.Position.Value, true, false);
      MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);

      Static.Save();

      CLI.SendMessageToClient( new Message {
        Sender = "Adjutant",
        Text = "Wait, there's more... we're receiving a transmission and GPS coordinates from someone claiming to be from the Alpha Squadron.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-a"
      });
      CLI.SendMessageToClient( new Message {
        Sender = "Adjutant",
        Text = "Some of them have turned to the Dominion but many are still loyal to the Confederacy. I'm patching them through.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-b"
      });
      // Joey
      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "Hello Commander. I'm glad to finally meet you. My name is Samir Duran and I've been following you ever since you landed at the Protoss Temple.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-c"
      });
      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "You see, the temple is actually an ancient prison which has been containing a powerful creature for milienia.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-d"
      });

      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "Three of its components have gone missing and the prison is losing strength. You must find them or we are all doomed.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-e"
      });

      CLI.SendMessageToClient( new Message {
        Sender = "Adjutant",
        Text = "I do not know if we can trust him, commander, but it sounds like we have no choice.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-f"
      });

      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "It won't be easy. I've sent you their last GPS locations. One is buried deep under the ground. You'll have to drill to reach it.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-g"
      });

      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "Another is on lock down in a Planetary Fortress. There are no ships in orbit if you can sneak in undetected.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-h"
      });

      CLI.SendMessageToClient( new Message {
        Sender = "Samir Duran",
        Text = "The third is hidden up somewhere in an abandoned Dominion lab. It's no longer guarded, but be careful, it was a weapons research facility, and any explosions could cause a chain reaction.",
        SteamUserId = player.SteamUserId,
        Sound = "quest3-pending-i"
      });

    }

    private static void UpdateDrillQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      switch( quest.State ) {
        case QuestState.Pending:

          MyPlanet closest = SpaceCraftSession.GetClosestPlanet(player.GetPosition());

          SpawningQuest = quest;

          IMyCubeGrid station = CreateIfNotExists(QuestGrid.DominionLab,closest);




          break;

        case QuestState.Started:
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "There is the lab but it has been abandoned for some time. I was able to restore power remotely but you may have trouble getting in.",
            SteamUserId = player.SteamUserId,
            // Sound = "quest3-pending-e"
          });
          quest.Range = 5f;
          break;

        case QuestState.Commencing:
          CLI.SendMessageToClient( new Message {
            Sender = "Adjutant",
            Text = "Excellent work commander, you've recovered the artifact.",
            SteamUserId = player.SteamUserId,
            // Sound = "quest3-pending-e"
          });
          Static.SetQuestState(player.SteamUserId,QuestId.Drill,QuestState.Completed);

          UnlockFinalQuest(player.SteamUserId);
          break;
        // case QuestState.Challenge:
        //   break;
      }
    }

    private static void UnlockFinalQuest(ulong steamUserId) {
      if( !UnlockedFinalQuest(steamUserId) ) return;
    }

    private static bool UnlockedFinalQuest(ulong steamUserId) {
      QuestId[] quests = { QuestId.Drill, QuestId.Grinder, QuestId.Welder };
      foreach( QuestId id in quests) {
        Quest quest = Quests.Static.GetQuest(steamUserId,id);
        if( quest == null || quest.State != QuestState.Completed ) return false;
      }
      return true;
    }

    private static void UpdateGrinderQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      switch( quest.State ) {
        case QuestState.Pending:
          break;

        case QuestState.Started:
          break;

        case QuestState.Commencing:
          break;
        case QuestState.Challenge:
          break;
      }
    }

    private static void UpdateWelderQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      switch( quest.State ) {
        case QuestState.Pending:
          break;

        case QuestState.Started:
          break;

        case QuestState.Commencing:
          break;
        case QuestState.Challenge:
          break;
      }
    }

    private static void UpdateFinalQuest( Quest quest ) {
      IMyPlayer player = SpaceCraftSession.GetPlayer(quest.SteamUserId);
      if( player == null ) return;

      switch( quest.State ) {
        case QuestState.Pending:
          break;

        case QuestState.Started:
          break;

        case QuestState.Commencing:
          break;
        case QuestState.Challenge:
          break;
      }
    }

    public static Vector3 GetRandomVector( int size ) {
      return new Vector3(Randy.Next(-size,size),Randy.Next(-size,size),Randy.Next(-size,size));
    }

    public static void UnlockTechnology( ) {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        UnlockTechnology(list);
      }
    }

    public static void UnlockTechnology( List<MyDefinitionId> list ) {
      if( list == null ) return;
      foreach(MyDefinitionId tech in list )
        MyVisualScriptLogicProvider.ResearchListRemoveItem(tech);

      // List<IMyPlayer> players = new List<IMyPlayer>();
			// MyAPIGateway.Players.GetPlayers(players);
			// foreach( IMyPlayer player in players ) {
      //   foreach(MyDefinitionId tech in list ) {
      //     MyVisualScriptLogicProvider.PlayerResearchUnlock(player.PlayerID,tech);
      //   }
      // }
    }

    public static void UnlockTechnology( long playerId ) {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        UnlockTechnology(playerId, list);
      }
    }

    public static void UnlockTechnology( long playerId, List<MyDefinitionId> list ) {
      if( list == null ) return;
      foreach(MyDefinitionId tech in list ) {
        // MyVisualScriptLogicProvider.PlayerResearchUnlock(playerId,tech);
        MyVisualScriptLogicProvider.ResearchListRemoveItem(tech);
      }
    }

    public static void LockTechnology( ) {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        LockTechnology(list);
      }
    }

    public static void LockTechnology( long playerId ) {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        LockTechnology(playerId, list);
      }
    }

    public static void LockTechnology( List<MyDefinitionId> list ) {
      if( list == null ) return;
      foreach(MyDefinitionId tech in list ) {
        MyVisualScriptLogicProvider.ResearchListAddItem(tech);
      }
      // List<IMyPlayer> players = new List<IMyPlayer>();
			// MyAPIGateway.Players.GetPlayers(players);
			// foreach( IMyPlayer player in players ) {
      //   foreach(MyDefinitionId tech in list ) {
      //     MyVisualScriptLogicProvider.PlayerResearchLock(player.PlayerID,tech);
      //
      //   }
      // }
    }

    public static void LockTechnology( long playerId, List<MyDefinitionId> list ) {
      foreach(MyDefinitionId tech in list ) {
        // MyVisualScriptLogicProvider.PlayerResearchLock(playerId,tech);
        MyVisualScriptLogicProvider.ResearchListAddItem(tech);
      }
    }


    public static void RemoveTechnology() {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        foreach(MyDefinitionId tech in list ) {
          MyVisualScriptLogicProvider.ResearchListAddItem(tech);
        }
      }
    }

    public static void RestoreTechnology() {
      foreach( List<MyDefinitionId> list in Technology.Values ) {
        foreach(MyDefinitionId tech in list ) {
          MyVisualScriptLogicProvider.ResearchListRemoveItem(tech);
        }
      }
    }

    public static void SpawnEnemy(string prefabName, MatrixD position, SpawningOptions options = SpawningOptions.RotateFirstCockpitTowardsDirection ) {
      // SpawnedGrids = new List<IMyCubeGrid>();
      CurrentQuest.Enemies = CurrentQuest.Enemies ?? new List<long>();
      try {
        MyAPIGateway.PrefabManager.SpawnPrefab(SpawnedGrids, prefabName, position.Translation, position.Forward, position.Up, Vector3.Zero, Vector3.Zero, null, options, MyVisualScriptLogicProvider.GetPirateId(), false, EnemiesSpawned );
      }catch(Exception exc){
      }
    }

    private static void EnemiesSpawned() {
      if( CurrentQuest == null || SpawnedGrids == null ) return;
      IMyPlayer player = SpaceCraftSession.GetPlayer(CurrentQuest.SteamUserId);
      CurrentQuest.Enemies = CurrentQuest.Enemies ?? new List<long>();
      foreach( IMyCubeGrid grid in SpawnedGrids ) {
        CurrentQuest.Enemies.Add(grid.EntityId);

        MyVisualScriptLogicProvider.SetName(grid.EntityId, grid.EntityId.ToString());
        MyVisualScriptLogicProvider.SetGPSHighlight(grid.EntityId.ToString(),grid.DisplayName,"Destroy this enemy", default(Color), playerId: player.PlayerID );
        MyVisualScriptLogicProvider.SetDroneBehaviourFull(grid.EntityId.ToString(), "Default", true, false, null, false, null, 10, PlayerDistance);
      }

      // CurrentQuest = null;
      // SpawnedGrids = new List<IMyCubeGrid>();
      Quests.Static.Save();
    }

    private static void Unlock( IMyCubeGrid grid ) {
      if( grid == null ) return;
      List<IMySlimBlock> blocks = new List<IMySlimBlock>();
      grid.GetBlocks(blocks);
      foreach( IMySlimBlock block in blocks ) {
        if( block.FatBlock == null ) continue;
        IMyDoor door = block.FatBlock as IMyDoor;
        // if( door == null && ass == null ) continue;
        if( door != null )
          TerminalPropertyExtensions.SetValue<bool>(door,"AnyoneCanUse",true);
        if( block.FatBlock is IMyAssembler || block.FatBlock is IMyCargoContainer )
          (block.FatBlock as MyCubeBlock).ChangeBlockOwnerRequest(block.FatBlock.OwnerId, MyOwnershipShareModeEnum.All);
      }
    }

    private static IMyCubeBlock GetSafeZone( IMyCubeGrid grid ) {
      if( grid == null ) return null;
      List<IMySlimBlock> blocks = new List<IMySlimBlock>();
      grid.GetBlocks(blocks);
      foreach( IMySlimBlock slim in blocks ) {
        if( slim.FatBlock == null || slim.FatBlock.BlockDefinition.TypeIdString != "MyObjectBuilder_SafeZoneBlock" ) continue;
        // Block.ListProperties(slim.FatBlock as IMyTerminalBlock);

        TerminalPropertyExtensions.SetValue<bool>(slim.FatBlock as IMyTerminalBlock, "SafeZoneCreate", true);
        // MySafeZoneComponent sz = slim.FatBlock.Components.Get<MySafeZoneComponent>();
        // if( sz != null ) {
        //
        // }
        return slim.FatBlock;
      }
      return null;
    }

    private static bool UpdateGPS(long identityId, Vector3D oldPos, Vector3D newPos, string name = "" ) {
      List<IMyGps> list =	MyAPIGateway.Session.GPS.GetGpsList(identityId);
      foreach(IMyGps gps in list) {
        if( gps.Coords == oldPos ) {
          gps.Coords = newPos;
          gps.Name = String.IsNullOrWhiteSpace(name) ? gps.Name : name;
          MyAPIGateway.Session.GPS.ModifyGps(identityId, gps);
          return true;
        }
      }

      return false;
    }

    private static bool RemoveGPS(long identityId, Quest quest ) {
      if( quest == null || !quest.Position.HasValue ) return false;

      List<IMyGps> list =	MyAPIGateway.Session.GPS.GetGpsList(identityId);
      foreach(IMyGps gps in list) {
        if( gps.Coords == quest.Position.Value ) {
          MyAPIGateway.Session.GPS.RemoveGps(identityId, gps);
          return true;
        }
      }

      return false;
    }

    private static Quests Open() {
      try {
        TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(File, typeof(Quests));
        return MyAPIGateway.Utilities.SerializeFromXML<Quests>(reader.ReadToEnd());
      }catch(Exception e){
        return null;
      }
      return null;
    }

    public bool Save() {
      try{
        if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Quests)) )
          MyAPIGateway.Utilities.DeleteFileInWorldStorage(File,typeof(Quests));
				TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(File, typeof(Quests));
				using (writer){

					writer.Write(MyAPIGateway.Utilities.SerializeToXML<Quests>(this));

				}

			}catch(Exception exc){
				return false;

			}

      return true;
    }


  }

}
