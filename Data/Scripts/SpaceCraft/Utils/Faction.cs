using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
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



  public class Faction {
    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();

    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    private List<Engineer> Engineers = new List<Engineer>();
    private List<Controllable> Controlled = new List<Controllable>();
    private List<MyEntity> Enemies = new List<MyEntity>();
    private CubeGrid MainBase;
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};

    public void AssessProgression() {
      switch( Progress ) {
        case Progression.None:
        case Progression.BasicAssembler:
        case Progression.BasicRefinery:
        case Progression.Assembler:
        case Progression.Refinery:
        case Progression.Reactor:
          break;
      }
    }

    public void UpdateBeforeSimulation() {
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
      }
    }

    public void UpdateBeforeSimulation10() {
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation10();
      }
    }

    public void UpdateBeforeSimulation100() {
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation100();
      }
    }

    public void AddControllable( Controllable c ) {
      c.Owner = this;
      c.Init();
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

      } else if( MainBase == null && c is CubeGrid ) {
        MainBase = c as CubeGrid;
        if( RespawnPoint == null ) {
          RespawnPoint = MainBase.GetRespawnBlock();
        }

      }
    }

    public MatrixD Translate(MatrixD matrix, Vector3D direction) {
      matrix.Translation = direction;
      return matrix;
      //return MatrixD.Add(matrix, MatrixD.CreateWorld(offset));
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
        MyAPIGateway.Utilities.ShowNotification("Respawn Point Found");
        return Translate(RespawnPoint.CubeGrid.WorldMatrix, new Vector3D(-5,0,0) );
        //return Translate(RespawnPoint.CubeGrid.WorldMatrix, RespawnPoint.CubeGrid.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward));
      } else {
        MyAPIGateway.Utilities.ShowNotification("DID NOT FIND RESPAWN POINT!!!");
      }

      // TODO: Respawn Drop Pod
      return MatrixD.Zero;
    }


  }
}
