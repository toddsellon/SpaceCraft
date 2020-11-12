using System;
using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using SpaceCraft.Utils;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace SpaceCraft.Utils {


  public class CLI {

    protected Dictionary<string,Action<MyCommandLine>> Actions = new Dictionary<string,Action<MyCommandLine>>();
    protected Dictionary<string,string> Convars = new Dictionary<string,string>();

    public CLI() {
      // MyAPIGateway.Multiplayer.RegisterMessageHandler(8877, ChatCommand.MessageHandler);
      Actions.Add("set",Set);
      Actions.Add("get",Get);
      Actions.Add("attack",Attack);

      Convars.Add("grids",grids);
      Convars.Add("difficulty",difficulty);
      Convars.Add("engineers",engineers);

      MyAPIGateway.Utilities.MessageEntered += MessageEntered;
    }

    public void MessageEntered(string message, ref bool broadcast){
			if(!message.StartsWith("/sc")) return;


			MyCommandLine cmd = new MyCommandLine();
			if( !cmd.TryParse(message) ) return;
			broadcast = false;
      string action = cmd.Argument(1);

      if( Actions.ContainsKey(action) )
        Actions[action](cmd);

		}

    public void Set( MyCommandLine cmd ) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
			if( player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner ) return;
      string convar = cmd.Argument(2);
      if( Convars.ContainsKey(convar) ) {
        Convars[convar] = cmd.Argument(3);
        MyAPIGateway.Utilities.ShowMessage( convar, Convars[convar] );
      }
    }

    public void Get( MyCommandLine cmd ) {
      string convar = cmd.Argument(2);
      if( Convars.ContainsKey(convar) ) {
        MyAPIGateway.Utilities.ShowMessage( convar, Convars[convar] );
      }

    }

    public void Attack( MyCommandLine cmd ) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) return;
      IMyEntity enemy;
      if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
        Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2));
        enemy = faction.GetClosestEnemy(player.GetPosition(),target == null ? null : target.MyFaction);
      } else {
        enemy = faction.GetClosestEnemy(player.GetPosition());
      }

      if( enemy == null ) return;

      foreach( Controllable c in faction.Controlled ) {
        if( !c.IsStatic && c.Fighter ) {
          c.Execute( new Order {
            Type = Orders.Attack,
            Target = enemy
          }, true);
        }
      }

    }

    public string grids
		{
			get
			{
        int val;
        MyAPIGateway.Utilities.GetVariable<int>("SC-Grids", out val);
				return val.ToString();
			}

      set
      {
        int v;
        if( Int32.TryParse(value, out v) ) {
          MyAPIGateway.Utilities.SetVariable<int>("SC-Grids", v);
        }
      }
		}

    public string difficulty
		{
			get
			{
        int val;
        MyAPIGateway.Utilities.GetVariable<int>("SC-Difficulty", out val);
				return val.ToString();
			}

      set
      {
        int v;
        if( Int32.TryParse(value, out v) ) {
          MyAPIGateway.Utilities.SetVariable<int>("SC-Difficulty", v);
        }
      }
		}

    public string engineers
		{
			get
			{
        int val;
        MyAPIGateway.Utilities.GetVariable<int>("SC-Engineers", out val);
				return val.ToString();
			}

      set
      {
        int v;
        if( Int32.TryParse(value, out v) ) {
          MyAPIGateway.Utilities.SetVariable<int>("SC-Engineers", v);
        }
      }
		}

		// public static void MessageHandler(byte[] data){
		//
		// 	var receivedData = MyAPIGateway.Utilities.SerializeFromBinary<SyncData>(data);
		//
		// 	if(receivedData.Instruction.StartsWith("MESChatMsg") == true){
		//
		// 		ServerChatProcessing(receivedData);
		//
		// 	}
		//
		//
		// }
  }

}
