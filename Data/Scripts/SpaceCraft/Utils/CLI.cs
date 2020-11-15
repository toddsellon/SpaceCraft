using System;
using System.Collections;
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

    //protected QueuedAction Queued;
    protected static ushort Id = 8008;
    protected bool Server;
    //public static readonly Convars Settings = new Convars();
    protected Dictionary<string,Action<MyCommandLine,Message>> Actions = new Dictionary<string,Action<MyCommandLine,Message>>(StringComparer.OrdinalIgnoreCase);

    public CLI(bool server) {
      Server = server;

      Actions.Add("get",Get);
      Actions.Add("attack",Attack);
      Actions.Add("build",Build);
      Actions.Add("set",Set);
      Actions.Add("spawn",Spawn);
      Actions.Add("follow",Follow);
      Actions.Add("war",War);
      Actions.Add("peace",Peace);
      Actions.Add("debug",Debug);

      MyAPIGateway.Utilities.MessageEntered += MessageEntered;
      MyAPIGateway.Multiplayer.RegisterMessageHandler(Id, MessageHandler);
    }

    public void Destroy() {
      MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
      MyAPIGateway.Multiplayer.UnregisterMessageHandler(Id, MessageHandler);
    }

    public void MessageEntered(string text, ref bool broadcast){
			if(!text.StartsWith("/sc ",StringComparison.OrdinalIgnoreCase)) return;

      broadcast = false;


      if( !Server ) {
        SendMessageToServer( new Message {
          Text = text
        });
        return;
      }

			ParseMessage(text);

		}

    public void MessageHandler(byte[] data){

      Message message = MyAPIGateway.Utilities.SerializeFromBinary<Message>(data);

      if( Server ) {
        ParseMessage(message.Text,message);
      } else {
        MyAPIGateway.Utilities.ShowMessage( message.Sender, message.Text );
      }

		}

    public bool ParseMessage(string text, Message message = null) {
      MyCommandLine cmd = new MyCommandLine();
			if( !cmd.TryParse(text) ) return false;

      string action = cmd.Argument(1);

      if( Actions.ContainsKey(action) ) {
        Actions[action](cmd, message);
        return true;
      }
      return false;
    }

    public bool SendMessageToServer(Message message) {
      if( message == null ) return false;
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      message.SteamUserId = player.SteamUserId;
      message.PlayerID = player.IdentityId;
      byte[] data = MyAPIGateway.Utilities.SerializeToBinary<Message>(message);
      return MyAPIGateway.Multiplayer.SendMessageToServer(Id, data);
    }

    public bool SendMessageToClient(Message message) {
      if( message == null || message.SteamUserId == 0 ) return false;
      byte[] data = MyAPIGateway.Utilities.SerializeToBinary<Message>(message);
      return MyAPIGateway.Multiplayer.SendMessageTo(Id, data, message.SteamUserId);
    }

    public void Respond( string sender, string text, Message message = null ) {
      if( message == null )
        MyAPIGateway.Utilities.ShowMessage( sender, text );
      else {
        message.Sender = sender;
        message.Text = text;
        SendMessageToClient(message);
      }
    }

    public void Debug( MyCommandLine cmd, Message message ) {
      Convars.Static.Debug = !Convars.Static.Debug;
      Respond("Debug", Convars.Static.Debug ? "On" : "Off", message);
    }

    public void Set( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      //IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
			//if( player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner ) return;
      string convar = cmd.Argument(2);
      Respond(convar, Convars.Static.Set(convar,cmd.Argument(3)), message);
    }

    public void Get( MyCommandLine cmd, Message message ) {
      string convar = cmd.Argument(2);
      Respond(convar, Convars.Static.Get(convar), message);
    }

    public void Attack( MyCommandLine cmd, Message message ) {
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

    public void Build( MyCommandLine cmd, Message message ) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = SpaceCraftSession.GetFactionContaining(message == null ? player.PlayerID : message.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      Prefab prefab = Prefab.Get(cmd.Argument(2));
      if( prefab == null ) {
        Respond("Error", "Prefab not found " + cmd.Argument(2), message);
        return;
      }

      faction.CurrentGoal = new Goal {
        Type = Goals.Construct,
        Prefab = prefab
      };

      Respond("Building", cmd.Argument(2), message);
    }

    public void Spawn( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(3), message);
        return;
      }
      Prefab prefab = Prefab.Get(cmd.Argument(2));
      if( prefab == null ) {
        Respond("Error", "Prefab not found " + cmd.Argument(2), message);
        return;
      }
      CubeGrid grid = new CubeGrid( CubeGrid.Spawn(prefab, faction.GetPlacementLocation(prefab), faction) );
      if( grid != null && grid.Grid != null )
        faction.TakeControl( grid );

      Respond("Spawning", cmd.Argument(2), message);
    }

    public void Follow( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      faction.Follow(player);
      Respond("Following", player.DisplayName, message);
    }

    public void War( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction to declare war", message);
        return;
      }

      MyAPIGateway.Session.Factions.DeclareWar(faction.MyFaction.FactionId, target.MyFaction.FactionId);
      Respond("War", "were declared on " + target.Name, message);
    }

    public void Peace( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }
      Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction to declare war", message);
        return;
      }

      // if( MyAPIGateway.Session.Factions.IsPeaceRequestStateSent (long myFactionId, long foreignFactionId) ) {
      //   MyAPIGateway.Session.Factions.AcceptPeace (long fromFactionId, long toFactionId);
      //   Respond("Peace", "was made with " + target.Name, message);
      // } else{
      //   MyAPIGateway.Session.Factions.SendPeaceRequest (long fromFactionId, long toFactionId);
      //   Respond("Peace", "was requested with " + target.Name, message);
      // }
      Respond("Peace", "Not Implemented yet", message);
    }



  }

}
