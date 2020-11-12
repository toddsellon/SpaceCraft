using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Definitions;

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

    public static Dictionary<string,int> GetCost( MyObjectBuilder_CubeBlock block, Dictionary<string,int> cost = null ) {
			return GetCost(MyDefinitionManager.Static.GetCubeBlockDefinition(block), cost);
		}

    public static Dictionary<string,int> GetCost( IMyCubeBlock block, Dictionary<string,int> cost = null ) {
			return GetCost(MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition), cost);
		}

		public static Dictionary<string,int> GetCost( MyCubeBlockDefinition def, Dictionary<string,int> cost = null ) {
			cost = cost ?? new Dictionary<string,int>();
			foreach( var component in def.Components ){
				//MyBlueprintDefinitionBase blueprint = null;
        MyComponentDefinition blueprint = null;
				//MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);
        MyDefinitionManager.Static.TryGetComponentDefinition(component.Definition.Id, out blueprint);
        string subtypeName = blueprint.Id.SubtypeName;
				if( blueprint == null ) continue;

        if( cost.ContainsKey(subtypeName) )
          cost[subtypeName] += component.Count;
        else
				    cost.Add(subtypeName, component.Count);
			}

			return cost;
		}

  }

}
