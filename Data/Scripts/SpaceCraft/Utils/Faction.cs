using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using SpaceCraft;
using SpaceCraft.Utils;
using VRageMath;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame.Utilities; // MyCommandLine


namespace SpaceCraft.Utils {

  public enum Progression {
    None,
    BasicAssembler,
    BasicRefinery,
    Assembler,
    Refinery,
    Reactor
  };

  public enum Priorties {
    None,
    Gather,
    Attack,
    Defend
  };


  //[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
  //public class Faction:MySessionComponentBase {
  public class Faction {
    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();
    public bool Spawned = false;
    public SerializableVector3 Color;
    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    private List<Engineer> Engineers = new List<Engineer>();
    private List<Controllable> Controlled = new List<Controllable>();
    private List<MyEntity> Enemies = new List<MyEntity>();
    private CubeGrid MainBase;
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    protected static Random Randy = new Random();
    protected int Tick = 0;

    // public override void Init(MyObjectBuilder_SessionComponent session) {
    //   base.Init(session);
    // }

    public void AssessProgression() {
      if( MainBase == null || MainBase.Grid == null ) {
        MyAPIGateway.Utilities.ShowNotification( MainBase == null ? "AssessProgression() " + Name + " Main base is null" : Name + " Main base has no grid" );
        return;
      }

      IMySlimBlock slim;
      switch( Progress ) {
        case Progression.None:
          if( MainBase.Grid.GridSizeEnum == MyCubeSize.Small ) {
            if( MainBase.Grid.Physics.IsMoving ) return;

            List<IMySlimBlock> batteries = MainBase.GetBlocks<IMyBatteryBlock>();

            if( batteries.Count == 0 || !batteries[0].IsFullIntegrity ) {
              Mulligan();
              return;
            }

            if( MainBase.AddLargeGridConverter() ) {
              batteries = MainBase.GetBlocks<IMyBatteryBlock>();

             if( MainBase.SuperGrid == null || batteries.Count == 0 || !batteries[0].IsFullIntegrity ) {
               Mulligan();
               return;
              }
              Progress = Progression.BasicAssembler;
            } else {
              Mulligan();
              return;
            }

          } else {
            MainBase.TryPlace(new MyObjectBuilder_Assembler{
							SubtypeName = "BasicAssembler"
						});

            MainBase.TryPlace(new MyObjectBuilder_Refinery{
							SubtypeName = "Blast Furnace"
						});
          }
          break;
        case Progression.BasicAssembler:

          break;
        case Progression.BasicRefinery:
        case Progression.Assembler:
        case Progression.Refinery:
        case Progression.Reactor:
          break;
      }
    }

    public void Mulligan() {
      MyAPIGateway.Utilities.ShowMessage( "Mulligan", Name + " took a mulligan" );
      foreach( Controllable c in Controlled ) {
        MyAPIGateway.Entities.RemoveEntity( c.ControlledEntity as IMyEntity );
      }
      Controlled = new List<Controllable>();

      // if( !Spawn(Vector3D.Zero) ) {
      //   Mulligan();
      // }
    }

    public void UpdateBeforeSimulation() {
      Tick++;
      //bool ten = Tick % 10 == 0;
      bool needs = false;
      /*foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
        if( ten ) c.UpdateBeforeSimulation10();
        if( Tick == 100 ) {
          MyAPIGateway.Utilities.ShowNotification("Faction UpdateBeforeSimulation() " + Tick.ToString() );
          c.UpdateBeforeSimulation100();
          if( c is CubeGrid && (c as CubeGrid).Need != CubeGrid.Needs.None ) needs = true;
        }
      }*/

      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
      }


      if( Tick == 99 ) {
        Tick = 0;

        foreach( Controllable c in Controlled ) {
          if( c is CubeGrid && (c as CubeGrid).Need != CubeGrid.Needs.None ) needs = true;
        }

        if( !needs ) {
          AssessProgression();
        }
      }
    }

    // public void UpdateBeforeSimulation10() {
    //   foreach( Controllable c in Controlled ) {
    //     c.UpdateBeforeSimulation10();
    //   }
    // }
    //
    // public void UpdateBeforeSimulation100() {
    //   bool needs = false;
    //   foreach( Controllable c in Controlled ) {
    //     c.UpdateBeforeSimulation100();
    //
    //     if( c is CubeGrid && (c as CubeGrid).Need != CubeGrid.Needs.None ) needs = true;
    //   }
    //
    //   if( !needs ) {
    //     AssessProgression();
    //   }
    //
    // }

    public Controllable AddControllable( Controllable c ) {
      c.Owner = this;
      //c.Init( Session );
      Controlled.Add( c );
      if( c is Engineer ) {
        Engineer e = c as Engineer;
        Engineers.Add( e );

        if( CommandLine.Switch("scavenger") ) {
          e.IssueOrder( new Order() {
            Type = Orders.Scout
          } );
        } else {
          e.IssueOrder( new Order() {
            Type = Orders.Grind,
            Destination = MyAPIGateway.Session.Player.GetPosition()
          } );
        }

      }

      return c;
    }

    public MatrixD GetSpawnLocation() {

      if( RespawnPoint == null ) {
        foreach( Controllable c in Controlled ) {
          if( c is CubeGrid ) {
            CubeGrid grid = c as CubeGrid;
            RespawnPoint = grid.GetRespawnBlock();

            if( RespawnPoint != null ) {
              break;
            }
          }
        }
      }

      if( RespawnPoint != null ) {
        MatrixD matrix = RespawnPoint.CubeGrid.WorldMatrix;
        Vector3D translation = matrix.Translation;
        Vector3D forward = matrix.GetDirectionVector(Base6Directions.Direction.Forward);
        //translation.X += 5;

        return MatrixD.CreateWorld( translation + forward );
      } else {
        // TODO: Respawn Drop Pod
      }


      return MatrixD.Zero;
    }


    public bool Spawn( Vector3D position ) {

      if( position == Vector3D.Zero ) {
        // Get Random Spawn
        MyPlanet planet = SpaceCraftSession.ClosestPlanet;
        int rand = Randy.Next(planet.Size.X);
        Vector3 p = new Vector3(rand,rand,rand) + Vector3.Normalize(planet.PositionLeftBottomCorner);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        position = planet.GetClosestSurfacePointGlobal( p );
        Vector3D up = (position - planet.WorldMatrix.Translation);
        up.Normalize();
        position = position + (up * 100 );
        //MyAPIGateway.Utilities.ShowMessage( Name, "Random Spawn " + position.ToString() );
        //planet.CorrectSpawnLocation( ref position, (double)planet.MaximumRadius );
      } else {
        //MyAPIGateway.Utilities.ShowMessage( Name, "Fixed Spawn " + position.ToString() );
      }

      MainBase = new CubeGrid(CubeGrid.Spawn("TerranPlanetPod", MatrixD.CreateWorld(position)));
      //MainBase.Init(Session);
      if( MainBase == null ) {
        MyAPIGateway.Utilities.ShowMessage( Name, "Main base never spawned");
      }

      AddControllable( MainBase );

      // MainBase = AddControllable( new CubeGrid(){
      //   Grid = CubeGrid.Spawn("TerranPlanetPod", MatrixD.CreateWorld(position))
      // } as Controllable ) as CubeGrid;

      // if( MainBase.Grid == null ) {
      //   Mulligan();
      //   return false;
      // }

      RespawnPoint = MainBase.GetRespawnBlock();

      position.X += 5;

      Engineer engineer = new Engineer( Engineer.Spawn(MatrixD.CreateWorld(position), this ) ) {
        Color = Color
      };

      AddControllable( engineer as Controllable );

      // AddControllable( new Engineer(){
      //   Character = Engineer.Spawn(MatrixD.CreateWorld(position))
      // } as Controllable );

      return Spawned;
    }


  }
}
