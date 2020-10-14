using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

namespace SpaceCraft.Utils {

  public class Block {
    public static bool DoAction( IMyTerminalBlock block, string name ) {
      List<ITerminalAction> actions = new List<ITerminalAction>();
      block.GetActions( actions );

      foreach( ITerminalAction action in actions ) {
        //MyAPIGateway.Utilities.ShowMessage( "Action", action.ToString() + ": " + action.Name );
        if( action.Name.ToString() == name ) {
          action.Apply( block );
          return true;
        }
      }

      return false;
    }

    // IMyTerminalValueControl
    // Static.autopilotControl = new MyTerminalControlCheckbox<MyShipController>("ArmsAp_OnOff", MyStringId.GetOrCompute("ARMS Autopilot"), MyStringId.GetOrCompute("Enable ARMS Autopilot"));


    public static void ListProperties( IMyTerminalBlock block ) {
      List<ITerminalProperty> props = new List<ITerminalProperty>();
      block.GetProperties( props );

      foreach( ITerminalProperty prop in props ) {
        MyAPIGateway.Utilities.ShowMessage( "Property", prop.ToString() );
      }
    }

  }

}
