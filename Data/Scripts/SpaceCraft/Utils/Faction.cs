using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Engine.Voxels;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Voxels;
using SpaceCraft;
using SpaceCraft.Utils;
using VRageMath;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame.Utilities; // MyCommandLine


namespace SpaceCraft.Utils {

  public enum Priorties {
    None,
    Gather,
    Attack,
    Defend
  };

  public enum Tech : ushort {
    Primitive,
    Established,
    Advanced,
    Space,
    Future
  };

  public enum TargetMethod {
    Reputation,
    Player,
    Closest,
    Random
  };


  //[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
  //public class Faction:MySessionComponentBase {
  public class Faction {

    public class Stats {
      public float Grids = 0f;
      public float Static = 0f;
      public float Workers = 0f;
      public float Fighters = 0f;
      public float Factories = 0f;
      public float Refineries = 0f;
      public int Spacecraft = 0;
      public bool InSpace = false;
      public Dictionary<string,float> Ratio = new Dictionary<string,float>{};
      public Dictionary<string,float> Desired = new Dictionary<string,float>{};
    }

    public static int LIMIT = 20;
    public List<IMyCharacter> Bots = new List<IMyCharacter>();
    public int Engineers = 0;
    public Races Race = Races.Terran;
    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();
    public bool Spawned = false;
    public static readonly Vector3 DefaultColor = new Vector3(0.575f,0.150000036f,0.199999958f);
    public SerializableVector3 Color = new SerializableVector3(0f,-0.8f,-0.306840628f);
    public Goal CurrentGoal = new Goal{ Type = Goals.Stabilize };
    private IMyCubeBlock RespawnPoint;
    public List<Controllable> Controlled = new List<Controllable>();
    private List<IMyFaction> Enemies = new List<IMyFaction>();
    private List<IMyEntity> Targets = new List<IMyEntity>();
    public CubeGrid MainBase;
    private static readonly List<string> DefaultResources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    //private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    public List<string> Resources = DefaultResources.ToList();
    public static Random Randy = new Random();
    protected int Tick = 0;
    public MyPlanet Homeworld;
    public List<MyPlanet> Colonized = new List<MyPlanet>();
    private static bool First = true;
    public Tech Tier = Tech.Primitive;
    public IMyFaction MyFaction;
    public IMyIdentity Founder;
    public bool Established = false;
    public string StartingPrefab;
    public Stats MyStats = new Stats();
    public Vector3D Origin = Vector3D.Zero;
    public IMyPlayer Following;
    public float FollowDistance = 0f;
    public List<IMyCubeGrid> SpawnedGrids;
    public bool Spawning = false;
    public bool Building = false;

    // public Vector3D SearchOffset = Vector3D.Zero;

    // Main loop
    public void UpdateBeforeSimulation() {
      Tick++;

      // if( Targets.Count > 0 && (Targets[0] == null || Targets[0].MarkedForClose) ) {
      //   Targets.RemoveAt(0);
      // }

      //if( MainBase != null && (MainBase.Grid == null || MainBase.Grid.Closed || MainBase.Grid.MarkedForClose) ) {
      if( MainBase != null && (MainBase.Grid == null || MainBase.Grid.Closed ) ) {
        RemapDocking();
        if( MainBase == null ) {
          Mulligan();
          return;
        }
      }

      AssessGoal();

      Controllable remove = null;
      foreach( Controllable c in Controlled ) {
        // if( c is CubeGrid && (c.Entity == null || c.Entity.Closed || c.Entity.MarkedForClose)  )
        if( c is CubeGrid && (c.Entity == null || c.Entity.Closed)  )
          remove = c;
        else
          c.UpdateBeforeSimulation();
      }

      if( remove != null ) Controlled.Remove( remove );

      if( Tick == 99 ) {
        Tick = 0;

        // Cleanup Bots
        foreach( IMyCharacter bot in Bots.ToList() ) {
          if( bot == null || bot.Closed || bot.MarkedForClose )
            Bots.Remove(bot);
        }

        if( Spawning || CommandLine.Switch("spawned") ) return;

        if( Engineers < Convars.Static.Engineers ) {
          TakeControl( new Engineer(this) );
        } else if( Engineers > Convars.Static.Engineers ) {
          RemoveEngineer();
        }

      }
    }

    public string GetCharacterModel() {

      if( Founder != null)
        foreach(MyCharacterDefinition character in MyDefinitionManager.Static.Characters ) {
          if( character.Name == Founder.DisplayName )
            return character.Name;
        }

      return "Astronaut";
    }

    private void RemapDocking() {
      MainBase = null;
      foreach( Controllable c in Controlled ) {
        CubeGrid grid = c as CubeGrid;
        if( grid == null ) continue;

        grid.DockedTo = null;
        grid.Docked = new List<CubeGrid>();

        if( MainBase != null ) continue;

        if( grid.Grid != null && !grid.Grid.Closed && !grid.Grid.MarkedForClose ) {
          MainBase = grid;
        }

      }

      if( MainBase == null ) {
        Mulligan("No grids remaining");
        return;
      }

      foreach( Controllable c in Controlled ) {
        CubeGrid grid = c as CubeGrid;
        if( grid == null || grid == MainBase ) continue;

        MainBase.ToggleDocked(grid);

      }


    }

    public void SpawnPrefab(string prefabName, MatrixD position, SpawningOptions options = SpawningOptions.None ) {
      Spawning = true;
      SpawnedGrids = new List<IMyCubeGrid>();
      Prefab prefab = Prefab.Get(prefabName);
      // if( prefab != null )
      //   prefab.ChangeColor(Color);
			//options |= SpawningOptions.RotateFirstCockpitTowardsDirection;
			// options |= SpawningOptions.UseGridOrigin;
      try {
        MyAPIGateway.PrefabManager.SpawnPrefab(SpawnedGrids, prefabName, position.Translation, position.Forward, position.Up, Vector3.Zero, Vector3.Zero, null, options, MyFaction == null ? 0 : MyFaction.FounderId, false, PrefabSpawned );
        // if( prefab != null )
        //   prefab.ChangeColor(Prefab.DefaultColor,Color);
      }catch(Exception exc){
        Spawning = false;
      }
    }

    public void PrefabSpawned() {
      if( SpawnedGrids.Count == 0 ) {
        if(Convars.Static.Debug) MyAPIGateway.Utilities.ShowMessage( "PrefabSpawned", "Fail" );
        Spawning = false;
        Building = false;
        return;
      }

      if(Convars.Static.Debug) MyAPIGateway.Utilities.ShowMessage( "PrefabSpawned", "Success" );
      CubeGrid grid = new CubeGrid(SpawnedGrids[0]);
      foreach( IMyCubeGrid g in SpawnedGrids ) {
        g.Flags &= ~EntityFlags.Sync;
        //g.Storage.Add(SpaceCraftSession.GuidIgnoreCleanup,"true"); // MES
        g.Storage = g.Storage ?? new MyModStorageComponent();
				g.Storage.Add(SpaceCraftSession.GuidSpawnType,"true"); // MES
				g.Storage.Add(SpaceCraftSession.GuidIgnoreCleanup,"true"); // MES
        g.DisplayName = Name + " " + g.DisplayName;
        if( g == SpawnedGrids[0] ) continue;
        grid.Subgrids.Add(g);

        // This was all to try and fix thruster sharing
        // List<IMySlimBlock> blocks = new List<IMySlimBlock>();
        // g.GetBlocks(blocks);
        // foreach(IMySlimBlock slim in blocks) {
        //   // MyCubeBlock block = slim.FatBlock as MyCubeBlock;
        //   // MyAPIGateway.Players.SetControlledEntity((ulong)MyFaction.FounderId, slim.FatBlock);
        //   // if( block == null ) continue;
        //   // block.ChangeOwner(MyFaction.FounderId, MyOwnershipShareModeEnum.None);
        //   // block.ChangeOwner(MyFaction.FounderId, MyOwnershipShareModeEnum.Faction);
        //   //if( block == null ) continue;
        //   // block.ChangeOwner(0, MyOwnershipShareModeEnum.None);
        //   //block.ChangeOwner(MyFaction.FounderId, MyOwnershipShareModeEnum.All);
        //   //block.ChangeBlockOwnerRequest(MyFaction.FounderId, MyOwnershipShareModeEnum.All);
        //
        //   // IMyThrust thrust = slim.FatBlock as IMyThrust;
        //   // if( thrust != null ) {
        //   //   thrust.PowerConsumptionMultiplier = 1f;
        //   // }
        // }

      }
      Controllable c = TakeControl( grid );
      MainBase = MainBase ?? grid;
      if( MainBase != null )
        RespawnPoint = RespawnPoint ?? MainBase.GetRespawnBlock();

      if( Building ) {
        CurrentGoal.Entity = grid;

        grid.SetToConstructionSite();
        //MainBase = MainBase ?? GetBestRefinery();
        MainBase.ToggleDocked( grid );
        MainBase.FindConstructionSite();
      }
      Spawning = false;
      Building = false;
    }

    public void Follow( IMyPlayer player ) {
      Following = player;
      // foreach( Controllable c in Controlled ) {
      //   if( c is Engineer ) {
      //     c.Execute( new Order {
      //       Type = Orders.Follow,
      //       Player = player,
      //       Range = 20f
      //     }, true );
      //   }
      // }
    }

    public void DetectedEnemy( IMyEntity enemy ) {
      if( !Targets.Contains( enemy ) )
        Targets.Add( enemy );
    }

    public void RemoveEngineer() {
      Engineer remove = null;
      foreach( Controllable c in Controlled ) {
        if( c is Engineer ) {
          remove = c as Engineer;
          break;
        }
      }
      if( remove != null ) {
        remove.Character.Kill();
        Controlled.Remove( remove );
        Engineers--;
      }
    }

    public void AssessGoal() {
      if( CurrentGoal == null || CurrentGoal.Step == Steps.Completed ) DetermineNextGoal();

      switch( CurrentGoal.Type ) {
        case Goals.Stabilize:
          if( Tick == 99 )
            Stabilize();
          break;
        default:
          Construct();
          break;
      }

    }

    public void Stabilize() {
      if( Spawning || CurrentGoal == null || MainBase == null ) return;
      if( MainBase.Grid == null || MainBase.Grid.Closed || MainBase.Grid.MarkedForClose ) {
        Mulligan("MainBase.Grid was null or closed", true);
        return;
      }
      if( MainBase.Grid.Physics.IsMoving ) return;
      // if( RespawnPoint == null || !RespawnPoint.IsFunctional ) {
      //   Mulligan();
      //   return;
      // }


      List<IMySlimBlock> batteries = MainBase.GetBlocks<IMyBatteryBlock>();
      if( batteries.Count == 0 || batteries[0].IsDestroyed ) {
        Mulligan("No batteries or batteries destroyed", true);
        return;
      }

      // if( CommandLine.Switch("scavenger") ) {
      //   CurrentGoal.Complete();
      //   return;
      // }

      if( CurrentGoal.Step == Steps.Pending ) {
        if( MainBase.Grid.GridSizeEnum == MyCubeSize.Small ) {

          if( MainBase.Grid.DisplayName.EndsWith("Planet Pod") ) {
            if( MainBase.AddLargeGridConverter() ) {
            // if( MainBase.AddLargeGridConverter( !CommandLine.Switch("aggressive") ) ) {
              MainBase.Grid.Physics.ClearSpeed();
              CurrentGoal.Progress();
            } else {
              Mulligan("Could not add large grid converter", true);
            }
          } else {
            MainBase.FindConstructionSite();
            CurrentGoal.Complete();
          }
        } else {
          MainBase.FindConstructionSite();
          CurrentGoal.Complete();
        }

      } else {
        // MyVisualScriptLogicProvider.SetGridGeneralDamageModifier(MainBase.Grid.EntityId.ToString());
        MyVisualScriptLogicProvider.SetGridDestructible(MainBase.Grid.EntityId.ToString(),true);

        List<IMySlimBlock> assemblers = MainBase.GetBlocks<IMyAssembler>();
        if( MainBase.SuperGrid == null || MainBase.SuperGrid.MarkedForClose || MainBase.SuperGrid.Closed || assemblers.Count < 2 || !MyVisualScriptLogicProvider.HasPower(MainBase.SuperGrid.EntityId.ToString()) ) {
          Mulligan("Failed to add large grid convreter", true);
        } else {
          CurrentGoal.Complete();
        }

      }
    }

    public void BotSpawned( IMyCharacter bot ) {
      if(Convars.Static.Debug) MyAPIGateway.Utilities.ShowMessage( "BotSpawned", bot.ToString() );
      Bots.Add(bot);
      Spawning = false;
      CurrentGoal.Complete();
    }

    private static readonly float HIT_SIZE = 3f;
    private static readonly float ACCEPTABLE_HEIGHT = 5f;

    public static bool IsFlat( Vector3D point, MyPlanet planet = null, float multiplier = 1f ) {
      planet = planet ?? SpaceCraftSession.GetClosestPlanet(point);
      Vector3D up = Vector3D.Normalize(point - planet.WorldMatrix.Translation);
      Vector3D forward = Vector3D.CalculatePerpendicularVector(up);
      Vector3D right = Vector3D.Cross(forward, up);

      return Math.Abs(Vector3D.Distance(planet.GetClosestSurfacePointGlobal(point+(forward*HIT_SIZE)),planet.WorldMatrix.Translation)
        - Vector3D.Distance(planet.GetClosestSurfacePointGlobal(point-(forward*HIT_SIZE)),planet.WorldMatrix.Translation)) <= (ACCEPTABLE_HEIGHT*multiplier)
        &&
        Math.Abs(Vector3D.Distance(planet.GetClosestSurfacePointGlobal(point+(right*HIT_SIZE)),planet.WorldMatrix.Translation)
          - Vector3D.Distance(planet.GetClosestSurfacePointGlobal(point-(right*HIT_SIZE)),planet.WorldMatrix.Translation)) <= (ACCEPTABLE_HEIGHT*multiplier);
    }


    private int SearchOffset = 0;
    private static readonly int SEARCH_STEP = 5;
    private static readonly int SEARCH_MAX = 600;

    public bool GetSafeLocation( Prefab prefab, out MatrixD location ) {
      CubeGrid last = GetLastCreated();

      MatrixD matrix = last.Grid.WorldMatrix;
      Vector3D position = matrix.Translation;
      MyPlanet planet = SpaceCraftSession.GetClosestPlanet(position);
      Vector3D start = position + (matrix.Forward * SearchOffset);
      int step = (int)HIT_SIZE * 2;
      float multiplier = SearchOffset > 1000 ? 3f : 1f;
      // Vector3D[] directions = { matrix.Forward, matrix.Left, matrix.Right };
      Vector3D[] directions = { matrix.Left, matrix.Right };
      for( int offset = 1; offset < SEARCH_MAX; offset+=step ) {
        foreach( Vector3D direction in directions ) {
          MatrixD l = GetPlacementLocation( prefab, start + (direction * offset) );

          if( SearchOffset > 1500 ) { // Give up
            location = l;
            return true;
          }

          if( !IsFlat(l.Translation, planet, multiplier) )
            continue;

          BoundingSphereD sphere = new BoundingSphereD(l.Translation, prefab.Definition == null ? 1 : prefab.Definition.BoundingBox.Width/2 );

          List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

          if( ContainsGrids(entities) ) continue;

          location = l;
          return true;
        }
      }

      location = MatrixD.Zero;
      return false;

    }

    // public bool IsSafeLocation( Vector3D location, Prefab prefab, MyPlanet planet = null ) {
    //   planet = planet ?? SpaceCraftSession.GetClosestPlanet(location);
    //
    //   if( !IsFlat(location, planet) )
    //     return false;
    //
    //   BoundingSphereD sphere = new BoundingSphereD(location, prefab.Definition == null ? 1 : prefab.Definition.BoundingBox.Width/2 );
    //
    //   List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
    //
    //   if( ContainsGrids(entities) ) return false;
    //
    //   return true;
    // }

    public bool GetSafeLocation_old( Prefab prefab, Vector3D start, out MatrixD location ) {

      MyPlanet planet = null;
      double max = start.X + 10;
      for( start.X = start.X; start.X < max; start.X++ ) {
        for( start.Y = start.X; start.Y < max; start.Y++ ) {
          for( start.Z = start.X; start.Z < max; start.Z++ ) {
            MatrixD l = GetPlacementLocation( prefab, start );
            planet = planet ?? SpaceCraftSession.GetClosestPlanet(l.Translation);
            if( !IsFlat(l.Translation, planet) )
              continue;

              // BoundingBox box = prefab.Definition == null ? new BoundingBox(Vector3.Zero,new Vector3(1,1,1)) : prefab.Definition.BoundingBox; // box around prefab

            BoundingSphereD sphere = new BoundingSphereD(l.Translation, prefab.Definition == null ? 1 : prefab.Definition.BoundingBox.Width/2 );

            List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

            if( ContainsGrids(entities) ) continue;

            // Vector3D? position = MyAPIGateway.Entities.FindFreePlace(l.Translation, prefab.Definition == null ? 1f : prefab.Definition.BoundingSphere.Radius );
            //
            // if( !position.HasValue ) continue;
            //
            //
            // l.Translation = position.Value;
            location = l;
            return true;

          }
        }
      }

      location = MatrixD.CreateWorld(start);
      return false;
    }

    public static bool ContainsGrids( List<IMyEntity> entities ) {
      foreach( IMyEntity entity in entities ) {
        if( entity is IMyCubeGrid )
          return true;
      }

      return false;
    }

    public void Construct() {
      if( CurrentGoal == null ) return;

      if( Spawning ) {
        CurrentGoal.Tick++;
        if( CurrentGoal.Tick >= 100 ) {
          Spawning = false;
          CurrentGoal.Complete();
        }
        return;
      }


      if( CurrentGoal.Prefab == null && CurrentGoal.Entity == null && MyStats.Grids < Convars.Static.Grids ) {

        CurrentGoal.Prefab = CurrentGoal.Prefab ?? GetConstructionProject();

        CurrentGoal.Balance = CurrentGoal.Prefab == null ? null : CurrentGoal.Prefab.GetBalance();

        MainBase = MainBase ?? GetBestRefinery();
        if( MainBase == null ) return;

        CubeGrid target = MainBase.DockedTo == null ? MainBase : MainBase.DockedTo;
        target.Balance = CurrentGoal.Balance;
        target.AddQueueItems(CurrentGoal.Balance,true);

        // if( MainBase.DockedTo == null ) {
        //   MainBase.Balance = CurrentGoal.Balance;
        //   // MainBase.AddQueueItems(CurrentGoal.Prefab.GetBattery(),true);
        //   MainBase.AddQueueItems(CurrentGoal.Balance,true);
        // } else {
        //   MainBase.DockedTo.Balance = CurrentGoal.Balance;
        //   // MainBase.DockedTo.AddQueueItems(CurrentGoal.Prefab.GetBattery(),true);
        //   MainBase.DockedTo.AddQueueItems(CurrentGoal.Balance,true);
        // }


      }

      if( CurrentGoal.Step == Steps.Pending && CurrentGoal.Prefab != null ) {

        // Wait for balance to be paid
        // if( CurrentGoal.Balance != null )
        //   MyAPIGateway.Utilities.ShowMessage( Name, "Balance: " + String.Join(",",CurrentGoal.Balance.Keys) );
        // else
        //   MyAPIGateway.Utilities.ShowMessage( Name, "Balance paid" );
        if( CurrentGoal.Balance != null && CurrentGoal.Balance.Count > 0 ) return;
        CurrentGoal.Balance = null;

        Building = true;
        MatrixD location = MatrixD.Zero;

        if( !GetSafeLocation( CurrentGoal.Prefab, out location ) ) {
          SearchOffset += SEARCH_STEP;
          return; // Try again next frame
        }
        SearchOffset = 0;

        if( CurrentGoal.Prefab.Bot ) {
          Spawning = true;
          SpaceCraftSession.SpawnBot( CurrentGoal.Prefab.BotDefinition.Id.SubtypeName, location.Translation, BotSpawned );

        } else if( CurrentGoal.Prefab.IsStatic ) { // Haven't gotten static alignment to work yet with SpawnPrefab
          CubeGrid grid = new CubeGrid( CubeGrid.Spawn( CurrentGoal.Prefab, location, this) );
          if( grid == null || grid.Grid == null ) {
            CurrentGoal.Complete();
            return;
          }
          TakeControl(grid);
          CurrentGoal.Entity = grid;
          grid.SetToConstructionSite();
          MainBase = MainBase ?? GetBestRefinery();
          if( MainBase == null ) return;
          MainBase.ToggleDocked( grid );
          MainBase.FindConstructionSite();
        } else
          SpawnPrefab(CurrentGoal.Prefab.SubtypeId, location, CurrentGoal.Prefab.IsStatic ? SpawningOptions.UseGridOrigin : SpawningOptions.RotateFirstCockpitTowardsDirection );

        CurrentGoal.Progress();

      } else if( CurrentGoal.Step == Steps.Started ) {

        // Facilitate Production
        CubeGrid grid = CurrentGoal.Entity as CubeGrid;
        if( grid == null || grid.ConstructionSite == null || grid.ConstructionSite.FatBlock == null || grid.ConstructionSite.FatBlock.Closed ) {
          if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( Name, "Completed construction" );
          // if( grid.DockedTo != null && !grid.Drills )
          //   grid.DockedTo.ToggleDocked( grid );
          CurrentGoal.Complete();
          return;
        }
      }
    }

    public Prefab GetConstructionProject() {
      Prefab best = null;
      CubeGrid last = GetLastCreated();
      MyPlanet planet = SpaceCraftSession.GetClosestPlanet(last.Entity.WorldMatrix.Translation);
      SearchOffset = 0;
      float priority = 0f;
      // MyAPIGateway.Utilities.ShowMessage( "GetConstructionProject", String.Join(",",Resources) );
      foreach( Prefab prefab in Prefab.Prefabs ) {
        float p = Prioritize(prefab, planet.HasAtmosphere);
        // MyAPIGateway.Utilities.ShowMessage( "GetConstructionProject", prefab.SubtypeId + ": " + p.ToString() );
        if( best == null || p > priority ) {
          best = prefab;
          priority = p;
        }
      }

      return best;
    }


    // https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/Entities/Blocks/MyOreDetectorComponent.cs
    public MatrixD GetPlacementLocation( Prefab prefab, Vector3D search ) {
      CubeGrid last = GetLastCreated();
      //Dictionary<string, byte[]> maps = MyAPIGateway.Session.GetVoxelMapsArray();

      //if( maps[Homeworld] )
      int MAX = 100;


        MatrixD m = last.Grid.WorldMatrix;
        Vector3D p = m.Translation;
        MyPlanet planet = SpaceCraftSession.GetClosestPlanet(p);

        Vector3 blocked = last.Grid.LocalAABB.Size; // size of last base
        BoundingBox box = prefab.Definition == null ? new BoundingBox(Vector3.Zero,new Vector3(1,1,1)) : prefab.Definition.BoundingBox; // box around prefab
        // BoundingBox box = prefab.Definition.BoundingBox; // box around prefab

        Vector3D offset = m.Forward + m.Up + (search*HIT_SIZE);
        if( prefab.Bot ) {
          // IMyEntity enemy = GetClosestEnemy(p);
          IMyPlayer enemy = GetClosestEnemy(p);
          if( enemy != null ) {
            Vector3D towards = Vector3D.Normalize(enemy.GetPosition() - p);
            p = Vector3D.Distance(enemy.GetPosition(),p) < 140 ? enemy.GetPosition() - (towards*5) : enemy.GetPosition() - (towards*140);
          }
          planet = SpaceCraftSession.GetClosestPlanet(p);
        } else if( prefab.IsStatic ) {
          p = p + (m.Forward*100) + (m.Up*100);
        } else {
          p = p + blocked + (box.Size*2);
        }

        // Experimental
        //p = MyAPIGateway.Entities.FindFreePlace(p, prefab.Definition.BoundingSphere.Radius);

        Vector3D position = planet.GetClosestSurfacePointGlobal( p );
        Vector3D up = Vector3D.Normalize(position - planet.WorldMatrix.Translation);
        Vector3D perp = Vector3D.CalculatePerpendicularVector(up);

        if( Race != Races.Hybrid && Tier >= Tech.Advanced && (prefab.IsStatic || prefab.Spacecraft) && !CommandLine.Switch("grounded") ) {
          position = position + ( up * (planet.AtmosphereAltitude*3.5) );
        }

        else {
          if( prefab.IsStatic )
            position = position + (up * (box.Height *.05) );
          else
          // if( !prefab.IsStatic )
            position = position + (up * (box.Height*2) );
            // position = position + (up * (box.Width) );
        }
        MatrixD reference = planet.WorldMatrix;

        //MyPositionAndOrientation origin = prefab.PositionAndOrientation.HasValue ? prefab.PositionAndOrientation.Value : MyPositionAndOrientation.Default;

        MatrixD matrix = MatrixD.CreateWorld( position, perp, up );
        //MatrixD matrix = MatrixD.CreateWorld( position, origin.Forward,  origin.Up );

        //MatrixD rotationDelta = MatrixD.Invert(origin.GetMatrix()) * rotation;
        //rotation = rotation * rotationDelta;


        // Vector3D 	SwapYZCoordinates (Vector3D v)
        //MatrixD matrix = MatrixD.CreateWorld( position, Vector3D.CalculatePerpendicularVector(up), up );


        // if( prefab.SubtypeId == "Terran Battlecruiser" || prefab.SubtypeId == "Norad II" )
        //   matrix = MatrixD.CreateWorld( position, matrix.Right, matrix.Down );
        // else if( prefab.IsStatic )
  			//   matrix = MatrixD.CreateWorld(position, matrix.Backward, matrix.Left);

        matrix = prefab.Reorient(matrix);

        // if( prefab.IsStatic && (prefab.SubtypeId == "Planetary Fortress" || prefab.SubtypeId.StartsWith("Terran")) ) {
        //   matrix = MatrixD.CreateWorld(position, matrix.Backward, matrix.Left);
        // }


        //matrix = MatrixD.CreateWorld( position, rotation.Forward, rotation.Up );
        // MatrixD rotation = MatrixD.AlignRotationToAxes(ref matrix, ref reference);
        // if( prefab.SubtypeId == "Terran Battlecruiser" )
        // matrix = MatrixD.CreateWorld( position, rotation.Right, rotation.Backward );
        // else
        //   //matrix = MatrixD.CreateWorld( position, prefab.IsStatic ? rotation.Up : rotation.Right, prefab.IsStatic ? rotation.Left : rotation.Forward );
        //   matrix = MatrixD.CreateWorld( position, prefab.IsStatic ? rotation.Up : rotation.Right, prefab.IsStatic ? rotation.Left : rotation.Forward );
        //matrix = MatrixD.CreateWorld( position, rotation.Up, rotation.Left );


        //matrix = MatrixD.CreateWorld( position, up, Vector3D.CalculatePerpendicularVector(up) );

        //Matrix hitGridRotation = hitGrid.WorldMatrix.GetOrientation();
        //Base6Directions.Direction direction = matrix.GetClosestDirection(up);
        //matrix.Up = up;
        //Vector3D direction = matrix.GetDirectionVector(Base6Directions.Direction.Up);
        //matrix.SetDirectionVector( direction, up );
      //} else {
        // Place close to base
      //}

      if( Convars.Static.Debug )
        MyAPIGateway.Utilities.ShowMessage( Name, "Building " + prefab.ToString() + " at " + position.ToString() );

      return matrix;
    }


    public void SetReputation( long playerId, int reputation = 0 ) {
      if( MyFaction != null )
        MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(playerId, MyFaction.FactionId, reputation);
    }

    public int GetReputation( long playerId ) {
      if( MyFaction != null )
        return MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(playerId, MyFaction.FactionId);
      return 0;
    }

    public Controllable GetControllable( IMyEntity entity ) {
      foreach( Controllable c in Controlled ) {
        if( c.Entity == entity ) return c;
      }
      return null;
    }

    public Order NeedsOrder( Controllable c ) {
      if( c == null || CurrentGoal == null || c.Entity == null ) return null;

      if( c.Fighter )
        switch( CurrentGoal.Type ) {
          // case Goals.Stabilize:
          // case Goals.Construct:
          //   if( c == CurrentGoal.Entity ) break;
          //   // Determine if unit should transfer items
          //   /*if( c.Cargo && !c.IsStatic && CurrentGoal.Entity != MainBase && GetWithdrawlSource(c) != null ) {
          //     return null;
          //   } else*/ if( c.Drills && !CommandLine.Switch("scavenger") ) {
          //     break;
          //   }
          //   goto case Goals.Defend;

          // case Goals.Defend:
          //   if( CurrentGoal.Type == Goals.Defend && Targets.Count == 0 ) {
          //     CurrentGoal.Complete();
          //     break;
          //   }
          //   goto case Goals.Attack;

          case Goals.Attack:
            // if( !CommandLine.Switch("defensive") ) break;
            // if( Targets.Count == 0 ) {
            //   IMyEntity enemy = GetClosestEnemy(c.Entity.WorldMatrix.Translation);
            //   if( enemy == null) break;
            //   Targets.Add(enemy);
            // }
            if( CurrentGoal.Type == Goals.Attack ) {
              CubeGrid g = c as CubeGrid;
              if( g != null )
                g.SetBehaviour();

              return new Order {
                Type = Orders.Attack
              };
            }

            break;
        }


      //Vector3D position = c.Entity.WorldMatrix.Translation;
      CubeGrid refinery = GetBestRefinery();
      if( refinery == null ) return null;

      int distance = 25;

      // Vector3D position = refinery.Entity.WorldMatrix.Translation;
      Vector3D position = c.Entity.WorldMatrix.Translation;
      Engineer engineer = c as Engineer;
      MyPlanet planet = SpaceCraftSession.GetClosestPlanet( position );
      if( c != null && Convars.Static.Animations ) {
        //Vector3D position = c.Wheels || c.Atmosphere ? c.Entity.WorldMatrix.Translation : GetBestRefinery().Entity.WorldMatrix.Translation;

        if( planet == null ) return null;


        position = position + new Vector3D(Randy.Next(-distance,distance),Randy.Next(-distance,distance),Randy.Next(-distance,distance));

        // LineD line = new LineD(c.Entity.WorldMatrix.Translation,position);
  			// Vector3D? hit = null;

        //if( Vector3D.Distance(position,planet.GetClosestSurfacePointGlobal(position)) < 1000 || (planet.GetIntersectionWithLine(ref line,out hit, true) && hit.HasValue) ) {
        if( Vector3D.Distance(position,planet.GetClosestSurfacePointGlobal(position)) < 1000 ) {
          Vector3D up = Vector3D.Normalize(position - planet.WorldMatrix.Translation);
          position = planet.GetClosestSurfacePointGlobal(position) + (up*2);
        }
      }

      return new Order {
        Type = Orders.Drill,
        //Target = planet,
        Resources = GetDrillResources( planet, c ),
        // Destination = planet.GetClosestSurfacePointGlobal(position),
        Destination = position,
        // Range = 20f,
        Range = Convars.Static.Animations ? 10f : 5f,
        //Entity = GetBestRefinery(c)
      };
    }

    public CubeGrid GetBestRefinery( Controllable c = null ) {
      CubeGrid refinery = null;
      double distance = 0f;
      foreach( Controllable o in Controlled ) {
        if( o.RefineryTier == 0 ) continue;

        double d = c == null ? 0 : Vector3D.Distance( c.Entity.WorldMatrix.Translation, o.Entity.WorldMatrix.Translation );
        if( refinery == null ) {
          refinery = o as CubeGrid;
          distance = d;
          continue;
        }

        if( o.RefineryTier > refinery.RefineryTier || (o.RefineryTier == refinery.RefineryTier && d < distance)  ) {
          refinery = o as CubeGrid;
          distance = d;
        }
      }

      return refinery;
    }

    public CubeGrid GetClosestRefinery( Vector3D position ) {
      CubeGrid refinery = null;
      double distance = 0f;
      foreach( Controllable o in Controlled ) {
        if( o.RefineryTier == 0 ) continue;
        double d = Vector3D.Distance( o.Entity.WorldMatrix.Translation, position );
        if( refinery == null || d < distance ) {
          refinery = o as CubeGrid;
        }
      }

      return refinery;
    }

    public List<Order> GetScoutOrders() {
      List<Order> orders = new List<Order>();
      if( MainBase == null || MainBase.Grid == null ) return orders;
      if( Origin == Vector3D.Zero ) Origin = MainBase.Grid.WorldMatrix.Translation;

      List<Vector3D> destinations = new List<Vector3D>();
      Vector3I d = new Vector3I(0,0,0);
      for( d.X = -1; d.X < 2; d.X++ )
        for( d.Y = -1; d.Y < 2; d.Y++ )
          for( d.Z = -1; d.Z < 2; d.Z++ ) {
            Vector3D destination = Origin + (d*500);
            MyPlanet planet = SpaceCraftSession.GetClosestPlanet(destination);
            destination = planet.GetClosestSurfacePointGlobal(destination);
            Vector3D up = Vector3D.Normalize(destination - planet.WorldMatrix.Translation);
            //Vector3D up = Vector3D.Normalize(destination - planet.PositionComp.WorldVolume.Center);
            destination = destination + (up*150);
            if( !destinations.Contains(destination) ) {
              orders.Add( new Order{
                Type = Orders.Scout,
                Destination = destination,
                Planet = planet,
                Range = 100f
              });
              destinations.Add(destination);
            }
          }

      return orders;
    }

    public void Detected( IMyEntity detected ) {
      if( Targets.Contains(detected) ) return;
      if( detected is IMyCharacter ) {
        foreach( Controllable c in Controlled ) {
          if( c.Entity == detected ) return;
        }
        Targets.Add( detected );
      } else if( detected is IMyCubeGrid ) {
        IMyCubeGrid grid = detected as IMyCubeGrid;
        List<long> owners = new List<long>(grid.BigOwners);
        owners.AddRange(grid.SmallOwners);
        foreach( long owner in owners ) {
          IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
          if( faction == null ) continue;
          if( MyRelationsBetweenFactions.Enemies == MyAPIGateway.Session.Factions.GetRelationBetweenFactions(MyFaction.FactionId, faction.FactionId) ) {
            Targets.Add(detected);
            return;
          }
        }
      }

    }

    public Dictionary<string,VRage.MyFixedPoint> GetDrillResources( MyPlanet planet, Controllable c ) {
      Dictionary<string,VRage.MyFixedPoint> resources = new Dictionary<string,VRage.MyFixedPoint>();
      CubeGrid grid = c as CubeGrid;

      //resources.Add("Stone",(VRage.MyFixedPoint)(Tier == Tech.Primitive ? 100 : 10)*Convars.Static.Difficulty);
      resources.Add("Stone",(VRage.MyFixedPoint)100*Convars.Static.Difficulty);

      // if( grid != null && grid.DockedTo != null && grid.DockedTo.RefineryTier == 1 ) {
      //   return resources;
      // }
      switch( Tier ) {
        case Tech.Primitive:
          // Do nothing
          break;
        case Tech.Space:
          resources.Add("Uranium",(VRage.MyFixedPoint)0.05*Convars.Static.Difficulty);
          resources.Add("Platinum",(VRage.MyFixedPoint)0.1*Convars.Static.Difficulty);
          goto case Tech.Advanced;
        //case Tech.Established:
        case Tech.Advanced:
          if( Tier != Tech.Space && CommandLine.Switch("nuclear") ) {
            resources.Add("Uranium",(VRage.MyFixedPoint)0.05*Convars.Static.Difficulty);
            resources.Add("Platinum",(VRage.MyFixedPoint)0.1*Convars.Static.Difficulty);
          }
          resources.Add("Silver",(VRage.MyFixedPoint)1*Convars.Static.Difficulty);
          resources.Add("Gold",(VRage.MyFixedPoint)0.5*Convars.Static.Difficulty);
          goto default;
        default:
          if( Race == Races.Terran )
            resources.Add("Ice",(VRage.MyFixedPoint)12*Convars.Static.Difficulty);

          resources.Add("Cobalt",(VRage.MyFixedPoint)(Race == Races.Protoss ? 0.2 : 0.4)*Convars.Static.Difficulty);

          if( Race != Races.Zerg ) {
            resources.Add("Iron",(VRage.MyFixedPoint)2*Convars.Static.Difficulty);
            resources.Add("Nickel",(VRage.MyFixedPoint)(Race == Races.Protoss ? 0.4 : 0.2)*Convars.Static.Difficulty);
            resources.Add("Magnesium",(VRage.MyFixedPoint)(Race == Races.Terran ? 1.5 : 0.5 )*Convars.Static.Difficulty);
            resources.Add("Silicon",(VRage.MyFixedPoint)0.2*Convars.Static.Difficulty);
          } else {
            //resources["Stone"] *= 2;
            resources["Cobalt"] *= 2;
          }

          break;
      }
      return resources;
    }

    public CubeGrid GetWithdrawlSource( Controllable controllable ) {
      int available = controllable.AvailableVolume.ToIntSafe();
      float volume = (float)available;

      IMySlimBlock target = (CurrentGoal.Entity as CubeGrid).ConstructionSite;
      if( target == null ) return null;

      Dictionary<string,int> total = new Dictionary<string,int>();
      target.GetMissingComponents(total);

      Dictionary<string,int> missing = new Dictionary<string,int>(total);
      if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Missing", string.Join(",", missing.Keys ) );

      foreach(Controllable c in Controlled ) {
        if( !(c is CubeGrid) ) continue;
        CubeGrid grid = c as CubeGrid;
        if( CurrentGoal.Type == Goals.Construct && grid == CurrentGoal.Entity ) continue;
        Dictionary<string,int> surplus = c.GetSurplus();
        if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Surplus", string.Join(",", surplus.Keys ) );
        foreach( string st in total.Keys ) {
          if( surplus.ContainsKey( st ) ) {
            MyPhysicalItemDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition( MyDefinitionId.Parse("MyObjectBuilder_Component/" + st) );
            if( surplus[st] >= missing[st] ) {
              volume -= def.Volume * (missing[st]-surplus[st]);
              missing[st] = 0;
            } else {
              volume -= def.Volume * surplus[st];
              missing[st] -= surplus[st];
            }
          }
        }

        bool fulfilled = true;
        foreach( int amount in missing.Values ) {
          if( amount > 0 ) {
            fulfilled = false;
            break;
          }
        }

        // VRage.MyFixedPoint v = volume;
        // foreach( string st in surplus.Keys ) {
        //   MyPhysicalItemDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition(  MyDefinitionId.Static.Parse(st) );
        //   v -= def.Volume * surplus[ob];
        //   if( missing.HasKey( def.Id.SubtypeName ) ) {
        //     if( missing[def.Id.SubtypeName] > surplus[def.Id.SubtypeName] )
        //   }
        // }
        // TODO: Check if order is fulfilled, not just volume check
        //if( target.IsFullyDismounted && v / volume > 0.1  ) {
        if( fulfilled || volume <= 0 ) {
          controllable.Drop( OBTypes.Ore );
          controllable.Execute( new Order {
            Type = Orders.Withdraw,
            Entity = grid,
            Target = grid.Grid
          });
          return grid;
        }
      }

      return null;
    }

    public CubeGrid GetLastCreated() {
      CubeGrid last = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid )
          last = c as CubeGrid;
      }
      return last;
    }

    public CubeGrid GetClosestGrid( Controllable from ) {
      CubeGrid best = null;
      double distance = 0;
      foreach( Controllable c in Controlled ) {
        if( c == from || c is Engineer ) continue;
        CubeGrid grid = c as CubeGrid;
        double d = Vector3D.Distance( grid.Grid.WorldMatrix.Translation, from.Entity.WorldMatrix.Translation );
        if( best == null || d < distance ) {
          best = grid;
          d = distance;
        }
      }

      return best;
    }

    public void DetermineNextGoal() {

      if( CommandLine.Switch("nobuild") ) {
        CurrentGoal = new Goal{
          Type = CommandLine.Switch("defensive") ? Goals.Defend : Goals.Attack
        };
        return;
      }

      // if( CommandLine.Switch("aggressive") || CommandLine.Switch("scavenger") ) {
      //   CurrentGoal = new Goal{
      //     Type = Goals.Attack
      //   };
      //   return;
      // }

      CurrentGoal = new Goal{
        Type = Goals.Defend
      };


      // Should attack?
      if( CommandLine.Switch("aggressive") || (!CommandLine.Switch("defensive") && GetEnemies().Count > 0) ) {
        CurrentGoal.Type = Goals.Attack;
      }

      MyStats = new Stats();

      CubeGrid building = null;
      CubeGrid needs = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid ) {
          CubeGrid grid = c as CubeGrid;
          MyStats.Grids++;
          if( grid.ConstructionSite != null ) building = grid;

          if( grid.IsStatic ) MyStats.Static++;
          else if( grid.Drills ) MyStats.Workers++;
          if( grid.Fighter ) MyStats.Fighters++;
          if( grid.FactoryTier > 0 ) MyStats.Factories++;
          if( grid.RefineryTier > 0 ) MyStats.Refineries++;
          if( grid.Spacecraft ) MyStats.Spacecraft++;
          // else if( building == null ) {
          //   grid.AssessNeed();
          // }
        }
      }

      if( MyStats.Grids == 0 ) {
        Mulligan("No grids were found");
        return;
      }

      // if( MyStats.Grids == Convars.Static.Grids ) {
      //
      // }

      MyStats.Ratio.Add("Workers", MyStats.Workers/MyStats.Grids);
      MyStats.Ratio.Add("Fighters", MyStats.Fighters/MyStats.Grids);
      MyStats.Ratio.Add("Factories", MyStats.Factories/MyStats.Grids);
      MyStats.Ratio.Add("Refineries", MyStats.Refineries/MyStats.Grids);
      MyStats.Ratio.Add("Static", MyStats.Static/MyStats.Grids);

      MyStats.Desired.Add("Workers", Race == Races.Hybrid ? 0f : .5f);
      MyStats.Desired.Add("Fighters", CommandLine.Switch("aggressive") ? .5f : .25f );
      MyStats.Desired.Add("Factories", .45f);
      MyStats.Desired.Add("Refineries", .45f);
      //MyStats.Desired.Add("Static", CommandLine.Switch("aggressive") ? .75f : .5f );
      MyStats.Desired.Add("Static", .25f );



      if( building != null ) {
        // CurrentGoal = new Goal{
        //   Type = Goals.Construct,
        //   Entity = building,
        //   Step = Steps.Started
        // };
        CurrentGoal.Entity = building;
        CurrentGoal.Step = Steps.Started;
        return;
      }

      // if( MyStats.Grids >= Convars.Static.Grids ) { // Limit reached
      //   return;
      // }



    }

    public void DeclareWar() {
      MyObjectBuilder_FactionCollection fc = MyAPIGateway.Session.Factions.GetObjectBuilder();
			foreach( MyObjectBuilder_Faction ob in fc.Factions ) {
				if( MyFaction.FactionId == ob.FactionId ) continue;
				MyAPIGateway.Session.Factions.DeclareWar(MyFaction.FactionId, ob.FactionId);
        MyAPIGateway.Session.Factions.DeclareWar(ob.FactionId,MyFaction.FactionId);
        IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(ob.FactionId);

        if( faction == null ) continue;
        SetReputation(faction.FounderId,-501);
			}

      // List<IMyPlayer> players = new List<IMyPlayer>();
			// MyAPIGateway.Players.GetPlayers(players);
			// foreach( IMyPlayer player in players ) {
      //   if( !MyFaction.IsMember(player.PlayerID) )
      //     MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(player.PlayerID, MyFaction.FactionId, -501);
      // }

    }

    public List<IMyFaction> GetEnemies() {
      List<IMyFaction> enemies = new List<IMyFaction>();
      if( MyFaction == null ) return enemies;

      IMyFactionCollection factions = MyAPIGateway.Session.Factions;
      MyObjectBuilder_FactionCollection fc = factions.GetObjectBuilder();
      foreach( MyObjectBuilder_Faction ob in fc.Factions ) {
        IMyFaction faction = factions.TryGetFactionById(ob.FactionId);
        if( faction != null && factions.AreFactionsEnemies(faction.FactionId, MyFaction.FactionId) || factions.AreFactionsEnemies(MyFaction.FactionId,faction.FactionId) ) {
          enemies.Add(faction);
        }
      }

      return enemies;
    }

    public CubeGrid GetSpacecraft() {
      foreach( Controllable c in Controlled ) {
        if( c.Spacecraft ) return c as CubeGrid;
      }
      return null;
    }

    public void Mulligan( string reason = "No reason specified", bool remove = false, MyPlanet planet = null, uint attempt = 0 ) {
      if( MyFaction != null && planet == null && attempt == 0 ) {
        CLI.SendMessageToAll( new Message {
          Sender = MyFaction.Name,
          Text = "GG"
        });
      }
      if( Convars.Static.Debug )
        MyAPIGateway.Utilities.ShowMessage( "Mulligan", Name + " took a mulligan:" + reason );

      foreach( Controllable c in Controlled ) {
        if( c == null || c.Entity == null ) continue;
        if( remove )
          MyAPIGateway.Entities.RemoveEntity( c.Entity );
        else {
          c.Entity.DisplayName = c.Entity.DisplayName.Replace(Name,"");
          if( c.Entity.Storage != null && c.Entity.Storage.ContainsKey(SpaceCraftSession.GuidFaction) )
            c.Entity.Storage.Remove(SpaceCraftSession.GuidFaction);
        }
        CubeGrid grid = c as CubeGrid;
        if( grid == null ) continue;
        foreach( IMyCubeGrid g in grid.Subgrids ) {
          if( g == null ) continue;
          if( remove )
            MyAPIGateway.Entities.RemoveEntity( g );
          else {
            g.DisplayName = g.DisplayName.Replace(Name,"");
            if( g.Storage != null && g.Storage.ContainsKey(SpaceCraftSession.GuidFaction) )
              g.Storage.Remove(SpaceCraftSession.GuidFaction);
          }
        }
        if( grid.SuperGrid != null ) {
          if( remove )
            MyAPIGateway.Entities.RemoveEntity( grid.SuperGrid );
          else {
            grid.SuperGrid.DisplayName = grid.SuperGrid.DisplayName.Replace(Name,"");
            if( grid.SuperGrid.Storage != null && grid.SuperGrid.Storage.ContainsKey(SpaceCraftSession.GuidFaction) )
              grid.SuperGrid.Storage.Remove(SpaceCraftSession.GuidFaction);
          }
        }
      }
      Resources = DefaultResources.ToList();
      if( Race == Races.Zerg || Race == Races.Hybrid )
        Resources.Add("Organic");
      Controlled = new List<Controllable>();
      Colonized = new List<MyPlanet>();
      Engineers = 0;
      MainBase = null;
      Tier = Tech.Primitive;
      CurrentGoal = new Goal{
        Type = Goals.Stabilize
      };
      if( !Spawn(planet) ) {
        if( attempt < 5 ) {
          attempt++;
          Mulligan("Previous mulligan failed",remove,planet,attempt);
        } else {
          MyAPIGateway.Utilities.ShowMessage( Name, "Failed to spawn" );
        }
      }

    }


    public Controllable TakeControl( Controllable c ) {
      c.Owner = this;

      //c.Init( Session );
      Controlled.Add( c );

      if( c is Engineer )
        Engineers++;
      else if( c.Entity != null ) {
        if( !c.Entity.DisplayName.StartsWith(Name) )
          c.Entity.DisplayName = Name + " " + c.Entity.DisplayName;
        c.Entity.Storage = c.Entity.Storage ?? new MyModStorageComponent();
        if( c.Entity.Storage.ContainsKey(SpaceCraftSession.GuidFaction) )
          c.Entity.Storage[SpaceCraftSession.GuidFaction] = Name;
        else
          c.Entity.Storage.Add(SpaceCraftSession.GuidFaction,Name);
      }

      return c;
    }

    public Controllable ReleaseControl( IMyEntity entity ) {
      foreach( Controllable controllable in Controlled ) {
        if( controllable.Entity == entity ) {
          // if( entity.DisplayName.StartsWith(Name) )
          Controlled.Remove(controllable);
          entity.Storage = entity.Storage ?? new MyModStorageComponent();
          entity.Storage.Remove(SpaceCraftSession.GuidFaction);
          return controllable;
        }
      }

      return null;
    }

    public MatrixD GetSpawnLocation() {

      if( RespawnPoint == null && MainBase != null ) {
        RespawnPoint = MainBase.GetRespawnBlock();
        // foreach( Controllable c in Controlled ) {
        //   if( c is CubeGrid ) {
        //     CubeGrid grid = c as CubeGrid;
        //     RespawnPoint = grid.GetRespawnBlock();
        //
        //     if( RespawnPoint != null ) {
        //       break;
        //     }
        //   }
        // }
      }

      if( RespawnPoint != null ) {
        IMyCubeGrid grid = RespawnPoint.CubeGrid;
        //MatrixD matrix = RespawnPoint.WorldMatrix;
        MatrixD matrix = grid.WorldMatrix;
        // Vector3D translation = matrix.Translation;
        Vector3D forward = matrix.GetDirectionVector(Base6Directions.Direction.Forward);
        //translation.X += 5;

        //return MatrixD.CreateWorld( translation + (forward*3) );
        return MatrixD.CreateWorld( matrix.Translation + (forward*grid.LocalAABB.Width*1.05) + (matrix.Up*3f), matrix.Forward, matrix.Up );
      } else {
        Mulligan("Failed to get spawn location");
      }


      return MatrixD.Zero;
    }

    public IMyEntity ResolveReputationTarget( Vector3D point ) {
      Dictionary<IMyEntity,IMyFaction> enemies = GetPossibleEnemies();

      IMyEntity best = null;
      int rep = 0;
      double distance = 0.0f;

      foreach( IMyEntity entity in enemies.Keys ) {
        IMyFaction faction = enemies[entity];

        int r = faction == null ? 0 : MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(faction.FounderId, MyFaction.FactionId);
        double d = Vector3D.Distance( entity.WorldMatrix.Translation, point );

        if( best == null || r < rep || (r == rep && d < distance) ) {
          best = entity;
          rep = r;
          distance = d;
        }

      }

      return best;
    }

    public IMyEntity ResolvePlayerTarget( Vector3D point ) {
      Dictionary<IMyEntity,IMyFaction> enemies = GetPossibleEnemies();

      IMyEntity best = null;
      double distance = 0.0f;
      foreach( IMyEntity entity in enemies.Keys ) {
        if( !(entity is IMyCharacter) ) continue;

        double d = Vector3D.Distance( entity.WorldMatrix.Translation, point );
        if( best == null || d < distance ) {
          best = entity;
          distance = d;
        }
      }

      return best;
    }

    public IMyEntity ResolveClosestTarget( Vector3D point ) {
      Dictionary<IMyEntity,IMyFaction> enemies = GetPossibleEnemies();

      IMyEntity best = null;
      double distance = 0.0f;
      foreach( IMyEntity entity in enemies.Keys ) {

        double d = Vector3D.Distance( entity.WorldMatrix.Translation, point );
        if( best == null || d < distance ) {
          best = entity;
          distance = d;
        }
      }

      return best;
    }

    public IMyEntity ResolveRandomTarget( Vector3D point ) {
      Dictionary<IMyEntity,IMyFaction> enemies = GetPossibleEnemies();
      return new List<IMyEntity>(enemies.Keys)[Randy.Next(enemies.Keys.Count)];
      // List<IMyEntity> enemies = GetPossibleEnemies();
      // return enemies[Randy.Next(enemies.Count)];
    }

    public void TargetMethodChanged() {
      foreach( Controllable c in Controlled ) {
        CubeGrid grid = c as CubeGrid;
        if( grid == null ) continue;

        grid.Target = null;
      }
    }

    public IMyEntity ResolveTarget( Vector3D point ) {
      switch(Convars.Static.Target) {
        case TargetMethod.Reputation:
          return ResolveReputationTarget(point);
        case TargetMethod.Random:
          return ResolveRandomTarget(point);
        case TargetMethod.Player:
          return ResolvePlayerTarget(point);
      }
      return ResolveClosestTarget( point );
    }

    public Dictionary<IMyEntity,IMyFaction> GetPossibleEnemies() {
      Dictionary<IMyEntity,IMyFaction> enemies = new Dictionary<IMyEntity,IMyFaction>();

      HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);

      foreach( IMyEntity entity in entities ) {
        IMyFaction owner = null;

        IMyCubeGrid grid = entity as IMyCubeGrid;

        if( grid != null ) {
          List<long> owners = grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners : grid.SmallOwners;

          if( owners == null || owners.Count == 0 ) continue;

          owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owners[0]);

          if( owner != null && MyRelationsBetweenFactions.Enemies != MyAPIGateway.Session.Factions.GetRelationBetweenFactions(owner.FactionId, MyFaction.FactionId))
            continue;

          enemies[entity] = owner;

          continue;
        }

        IMyCharacter character = entity as IMyCharacter;

        if( character == null ) continue;

        IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(character);

        if( player == null ) continue;

        owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);

        if( owner != null && MyRelationsBetweenFactions.Enemies != MyAPIGateway.Session.Factions.GetRelationBetweenFactions(owner.FactionId, MyFaction.FactionId))
          continue;

        enemies[entity] = owner;

      }

      return enemies;
    }

    public IMyPlayer GetClosestEnemy( Vector3D point, IMyFaction faction = null ) {
      //BoundingSphereD sphere = new BoundingSphereD( center, (double)5000 );
      //List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere( ref sphere );
      List<IMyPlayer> players = new List<IMyPlayer>();
      MyAPIGateway.Players.GetPlayers(players);
      IMyPlayer best = null;
      double distance = 0.0f;
      foreach( IMyPlayer player in players ) {
        IMyFaction owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);

        if( owner != null && MyRelationsBetweenFactions.Enemies != MyAPIGateway.Session.Factions.GetRelationBetweenFactions(owner.FactionId, MyFaction.FactionId))
          continue;

        double d = Vector3D.Distance( player.GetPosition(), point );
        if( best == null || d < distance ) {
          best = player;
          distance = d;
        }
      }

      return best;
    }

    public IMyEntity GetClosestEnemy_old( Vector3D point, IMyFaction faction = null ) {
      //BoundingSphereD sphere = new BoundingSphereD( center, (double)5000 );
      //List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere( ref sphere );
      HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);
      IMyEntity best = null;
      double distance = 0.0f;
      foreach( IMyEntity entity in entities ) {
        IMyCubeGrid grid = entity as IMyCubeGrid;
        IMyCharacter character = entity as IMyCharacter;
        IMyFaction owner = null;
        IMyPlayer player = null;
        List<long> owners = grid == null ? null : grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners : grid.SmallOwners;
        if( faction != null ) {
          if( grid != null ) {
            if( owners == null || owners.Count == 0 ) continue;
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owners[0]);
            if( owner != faction ) continue;
          } else if( character != null ) {
            player = MyAPIGateway.Players.GetPlayerControllingEntity(character);
            if( player == null ) continue;
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);

          }
          if( faction != null && owner != faction ) continue;
        } else {
          if( grid != null ) {
            if( owners == null || owners.Count == 0 ) continue;
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owners[0]);
          } else if( character != null ) {
            player = MyAPIGateway.Players.GetPlayerControllingEntity(character);
            if( player == null ) continue;
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);
          } else {
            continue;
          }
        }

        if( owner == null || MyRelationsBetweenFactions.Enemies != MyAPIGateway.Session.Factions.GetRelationBetweenFactions(owner.FactionId, MyFaction.FactionId))
          continue;

        double d = Vector3D.Distance( entity.WorldMatrix.Translation, point );
        if( best == null || d < distance ) {
          best = entity;
          distance = d;
        }
      }

      return best;
    }

    public static MyPlanet GetRandomPlanet() {
      return SpaceCraftSession.Planets[Randy.Next(SpaceCraftSession.Planets.Count)];
		}

    public bool Spawn( MyPlanet planet = null ) {
      return Spawn( Vector3D.Zero, planet );
    }

    public bool Spawn() {
      return Spawn( Vector3D.Zero );
    }

    public static string GetDefaultPrefab( Races race ) {
      switch( race ) {
        case Races.Protoss:
          return "Protoss Outpost";
        case Races.Zerg:
          return "Zerg Drop Site";
        case Races.Hybrid:
          return "Xel'Naga Monolith";
      }

      return "Terran Planet Pod";
    }

    public bool Spawn( Vector3D position, MyPlanet planet = null ) {
      Homeworld = planet;

      if( position == Vector3D.Zero ) {
        // Get Random Spawn


        if( Homeworld == null ) {
          if( CommandLine.Switch("outsider")){
            Homeworld = GetRandomPlanet();
          } else Homeworld = SpaceCraftSession.ClosestPlanet ?? GetRandomPlanet();
        }
        if( Homeworld == null ) return false;
        // Colonized.Add( Homeworld );
        //int rand = Randy.Next(Homeworld.Size.X);
        bool flat = false;


        while( !flat ) {

          Vector3 p = new Vector3(Randy.Next(-Homeworld.Size.X,Homeworld.Size.X),Randy.Next(-Homeworld.Size.Y,Homeworld.Size.Y),Randy.Next(-Homeworld.Size.Z,Homeworld.Size.Z)) + Homeworld.WorldMatrix.Translation;
          // Vector3 p = new Vector3(Randy.Next(Homeworld.Size.X),Randy.Next(Homeworld.Size.Y),Randy.Next(Homeworld.Size.Z)) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);

          position = Homeworld.GetClosestSurfacePointGlobal( p );
          //Homeworld.CorrectSpawnLocation(ref position,250f);
          Homeworld.CorrectSpawnLocation(ref position,15f);

          flat = IsFlat(position, Homeworld, .5f);
        }

      }

      if( CommandLine.Switch("nuclear") && !Resources.Contains("Uranium") ) {
        Resources.Add("Uranium");
        Resources.Add("Platinum");
      }

      if( CommandLine.Switch("spawned") ) return true;

      Vector3D up = Vector3D.Normalize(position - Homeworld.WorldMatrix.Translation);
      // Vector3D perp = up;
      // Vector3D.CalculatePerpendicularVector(perp);
      //position = position + (up * 150);

      string subtypeId = String.IsNullOrWhiteSpace(StartingPrefab) ? GetDefaultPrefab(Race) : StartingPrefab;
      Prefab prefab = Prefab.Get(subtypeId);

      // if( prefab == null ) return false;

      Vector3D perp = Vector3D.CalculatePerpendicularVector(up);
      MatrixD matrix = MatrixD.CreateWorld(position, perp, up);

      // SpawnPrefab(String.IsNullOrWhiteSpace(StartingPrefab) ? "Terran Planet Pod" : StartingPrefab, MatrixD.CreateWorld(position, up, Vector3D.CalculatePerpendicularVector(up)), SpawningOptions.UseGridOrigin );
      if( prefab != null && prefab.IsStatic ) {
        position = Homeworld.GetClosestSurfacePointGlobal( position );
        up = Vector3D.Normalize(position - Homeworld.WorldMatrix.Translation);
        perp = Vector3D.CalculatePerpendicularVector(up);
        matrix = MatrixD.CreateWorld(position, perp, up);
        MainBase = new CubeGrid(CubeGrid.Spawn(subtypeId, matrix, this));
        //MainBase = new CubeGrid(CubeGrid.Spawn(subtypeId, MatrixD.CreateWorld(position, matrix.Backward, matrix.Left), this));
      } else MainBase = new CubeGrid(CubeGrid.Spawn(subtypeId, matrix, this));


      if( MainBase == null || MainBase.Entity == null ) return false;
      //else
        // SpawnPrefab(subtypeId, MatrixD.CreateWorld(position, up, Vector3D.CalculatePerpendicularVector(up)), SpawningOptions.UseGridOrigin );

      // MainBase = new CubeGrid(CubeGrid.Spawn(String.IsNullOrWhiteSpace(StartingPrefab) ? "Terran Planet Pod" : StartingPrefab, MatrixD.CreateWorld(position, up, Vector3D.CalculatePerpendicularVector(up)), this));
      // //MainBase.Init(Session);
      //

      TakeControl( MainBase );
      RespawnPoint = MainBase.GetRespawnBlock();
      Refill( MainBase.Grid );

      MyVisualScriptLogicProvider.SetName(MainBase.Grid.EntityId, MainBase.Grid.EntityId.ToString());
      // MyVisualScriptLogicProvider.SetGridGeneralDamageModifier(MainBase.Grid.EntityId.ToString(),0);
      MyVisualScriptLogicProvider.SetGridDestructible(MainBase.Grid.EntityId.ToString(),false);
      //
      // if( MainBase.Grid == null ) {
      //   //Mulligan();
      //   return false;
      // }


      //
      // position.X += 5;
      //
      // TakeControl( new Engineer(this) );

      return true;
    }

    public static void Refill( IMyCubeGrid grid ) {

      List<IMySlimBlock> blocks = new List<IMySlimBlock>();
      grid.GetBlocks(blocks);
      foreach( IMySlimBlock slim in blocks ) {
        IMyGasTank tank = slim.FatBlock as IMyGasTank;
        if( tank == null ) continue;
        MyResourceSinkComponent sink = tank.Components.Get<MyResourceSinkComponent>();
        sink.SetInputFromDistributor((slim.BlockDefinition as MyGasTankDefinition).StoredGasId,10000000000,true,true);
      }
    }

    public bool IsSubgrid( IMyCubeGrid grid ) {
      foreach( Controllable c in Controlled ) {
        CubeGrid g = c as CubeGrid;
        if( g != null ) {
          if(g.Subgrids.Contains(grid)) return true;

          // Determine if this grid is connected to that one
          List<IMySlimBlock> blocks = new List<IMySlimBlock>();
          grid.GetBlocks(blocks);

          foreach(IMySlimBlock block in blocks ) {
            if( block.FatBlock == null ) continue;
            IMyMotorStator stator = block.FatBlock as IMyMotorStator;
            if( stator != null && stator.RotorGrid == g.Grid ) {
              g.Subgrids.Add(grid);
              return true;
            }

          }
        }
      }

      return false;
    }

    // public override void Init(MyObjectBuilder_SessionComponent session) {
    //   base.Init(session);
    // }


    protected float Prioritize( Prefab prefab, bool atmo = true ) {
      if( prefab.Race != Race ) return 0;
      if( !String.IsNullOrWhiteSpace(prefab.Faction) && prefab.Faction != Name ) return 0;

      if( !atmo && prefab.Atmosphere && !prefab.Spacecraft ) return 0;

      foreach( string resource in prefab.Cost.Keys ) {
        // Doesn't have access to resources
        if( !Resources.Contains(resource) ) {

          switch( resource ) {
            case "Cobalt":
              // Make exception if prefab has desired refinery
              if( prefab.RefineryTier < 2 )return 0f;
              break;
            case "Silver":
            case "Gold":
              // Make exception if prefab has desired refinery
              if( prefab.RefineryTier < 3 || Tier == Tech.Primitive )return 0f;
              break;
            default:
              return 0f;
          }

        }
      }
      float priority = (float)prefab.Price; // Build highest grid you can afford to reduce quantity of grids

      if( !CommandLine.Switch("grounded") && Tier == Tech.Space && !prefab.Spacecraft && !prefab.IsStatic ) return 0f;

      if( CommandLine.Switch("grounded") && !prefab.Wheels && !prefab.IsStatic ) return 0f;

      if( CommandLine.Switch("static") && !prefab.IsStatic ) return 0f;

      if( Tier < Tech.Space )
        priority *= prefab.Atmosphere && !prefab.IsStatic ? 2 : 1;
      else if( prefab.Atmosphere ) {
        return 0f;
      }

      if( prefab.Bot && Bots.Count < Convars.Static.Bots && MyStats.Ratio["Workers"] >= MyStats.Desired["Workers"] ) {
        priority *= 2000;
        return priority;
      }

      //if( CommandLine.Switch("aerial") && !prefab.Flying && !prefab.IsStatic ) return 0f;


      if( prefab.FactoryTier > 0 ) {
        priority *= MyStats.Ratio["Factories"] < MyStats.Desired["Factories"] ? 2f : 1f;
      }
      if( prefab.RefineryTier > 0 ) {
        priority *= MyStats.Ratio["Refineries"] < MyStats.Desired["Refineries"] ? 2f : 1f;
      }

      if( prefab.Worker ) {
        priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? (prefab.IsStatic ? 1f : 25f) : 0;
      } else {
        priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? 0 : 1f;
      }

      // if( prefab.Worker ) {
      //   priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? 2f : 1f;
      // }

      if( prefab.Fighter ) {

          priority *= MyStats.Ratio["Fighters"] <= MyStats.Desired["Fighters"] ? (CommandLine.Switch("aggressive") ? 2.5f : 1f) : .5f;

      }

      if( prefab.IsStatic ) {
          // priority *= MyStats.Ratio["Static"] < MyStats.Desired["Static"] ? 2f : .5f;
          priority *= MyStats.Ratio["Static"] < MyStats.Desired["Static"] ? 3f : .25f;
      }


      return priority;
    }

    public void DetermineTechTier() {
      if( CommandLine.Switch("hybrid") ) {
        Tier = Tech.Space;
        return;
      }

      foreach( Controllable c in Controlled ) {
        CubeGrid g = c as CubeGrid;
        if( g != null )
          g.CheckFlags();
      }
      CubeGrid refinery = GetBestRefinery();
      if( refinery == null ) {
        CurrentGoal = new Goal {
          Type = Goals.Stabilize
        };
        Tier = Tech.Primitive; // Shouldn't be happening
        return;
      }
      switch( refinery.RefineryTier ) {
        case 3:
          Tier = Tech.Advanced;
          Resources.Add("Silver");
          Resources.Add("Gold");
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
          if( InSpace(refinery.Grid.WorldMatrix.Translation) ) {
            Tier = Tech.Space;
            Resources.Add("Uranium");
            Resources.Add("Platinum");
          }
          break;
        case 2:
          Tier = Tech.Established;
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
          break;
        default:
          Tier = Tech.Primitive;
          break;
      }
      if( CommandLine.Switch("nuclear") && !Resources.Contains("Uranium") ) {
        Resources.Add("Uranium");
        Resources.Add("Platinum");
      }
    }

    public static bool InSpace( Vector3D pos ) {

      MyPlanet planet = SpaceCraftSession.GetClosestPlanet(pos);
      if( planet == null ) return true; // No planets?

      return planet.GetOxygenForPosition(pos) == 0f;
      //return planet.GetAirDensity(pos) == 0f;
    }

    public bool BlockCompleted( IMySlimBlock block ) {
      if( block.FatBlock == null ) return false;
      if( block.FatBlock is IMyRefinery ) {
        string subtypeName = block.BlockDefinition.Id.SubtypeName;
        if( Tier == Tech.Primitive ) {
          Tier = Tech.Established;
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
        }

        if( subtypeName == "LargeRefinery" || subtypeName == "LargeProtossRefinery" || subtypeName == "LargeZergRefinery" ) {
          if( Tier == Tech.Established ) {
            Resources.Add("Silver");
            Resources.Add("Gold");
            Tier = Tech.Advanced;
          } else if( Tier == Tech.Advanced && InSpace(block.CubeGrid.WorldMatrix.Translation) ) {
            Tier = Tech.Space;

            if( !Resources.Contains("Uranium") ) {
              Resources.Add("Uranium");
              Resources.Add("Platinum");
            }
          }
        }

        return true;
      }

      return false;
    }


  }
}
