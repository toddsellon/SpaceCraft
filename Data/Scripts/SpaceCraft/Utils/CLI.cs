using System;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Utils;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.Definitions;
using SpaceCraft.Utils;
using VRage.Game.ModAPI.Ingame.Utilities;
using Sandbox.Game.Entities;
using VRage.Audio;

namespace SpaceCraft.Utils {


  public class CLI {

    //protected QueuedAction Queued;
    protected static ushort Id = 8008;
    protected bool Server;
    protected Dictionary<string,Action<MyCommandLine,Message>> Actions = new Dictionary<string,Action<MyCommandLine,Message>>(StringComparer.OrdinalIgnoreCase);
    protected MyEntity3DSoundEmitter SoundEmitter;

    protected Queue<Message> QueuedMessages = new Queue<Message>();

    protected Dictionary<string,string> Descriptions = new Dictionary<string,string>{
      {"war","Declares war between factions /sc war faction1 faction2"},
      {"peace","Nakes peace between factions /sc peace faction1 faction2"},
      {"get","Checks a setting /sc get setting"},
      {"set","Changes a setting /sc set setting value"},
      {"build","Begins construction of prefab /sc build \"Prefab name\" faction"},
      {"spawn","Spawns in a prefab /sc spawn \"Prefab name\" faction"},
      {"join","Join specified faction /sc join faction"},
      {"gps","Gets GPS locations for your faction /sc gps"},
      {"follow","Orders faction to follow /sc follow distance"},
      {"stop","Stops following /sc stop"},
      {"donate","Donates grid you're controlling /sc donate"},
      {"respawn","Respawns a faction /sc respawn faction planet"},
      {"reset","Resets quest progress /sc reset"},
      {"lock","Locks all technology /sc lock"},
      {"unlock","Unlocks all technology /sc unlock"},
      {"establish","Establishes AI faction /sc establish faction"},
      {"dissolve","Dissolve AI faction /sc dissolve faction"}
    };

    public CLI(bool server) {
      Server = server;

      Actions.Add("get",Get);
      // Actions.Add("attack",Attack);
      Actions.Add("build",Build);
      Actions.Add("set",Set);
      Actions.Add("spawn",Spawn);
      Actions.Add("complete",Complete);
      Actions.Add("pay",Pay);
      Actions.Add("follow",Follow);
      Actions.Add("stop",Stop);
      Actions.Add("war",War);
      Actions.Add("peace",Peace);
      Actions.Add("join",Join);
      Actions.Add("debug",Debug);
      Actions.Add("gps",GPS);
      Actions.Add("control",Control);
      Actions.Add("release",Release);
      Actions.Add("respawn",Respawn);
      Actions.Add("donate",Donate);
      Actions.Add("reset",Reset);
      Actions.Add("unlock",Unlock);
      Actions.Add("lock",Lock);
      Actions.Add("establish",Establish);
      Actions.Add("dissolve",Dissolve);
      Actions.Add("?",Help);

      MyAPIGateway.Utilities.MessageEntered += MessageEntered;
      MyAPIGateway.Multiplayer.RegisterMessageHandler(Id, MessageHandler);

      MyAPIGateway.Utilities.ShowMessage( "SpaceCraft", "For help with commands, visit archmage.co/sccmd or type /sc ?" );


    }

    public bool IsPlaying { get { return SoundEmitter != null && SoundEmitter.IsPlaying; } }

    public void CheckSoundQueue() {

      if( !IsPlaying && QueuedMessages.Count > 0 ) {
        Message message = QueuedMessages.Dequeue();
        PlaySound(message.Sound);
        MyAPIGateway.Utilities.ShowMessage( message.Sender, message.Text );
      }

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


      if( !String.IsNullOrWhiteSpace(message.Sound) ) {
        if( IsPlaying ) {
          QueuedMessages.Enqueue(message);
           return;
        } else
          PlaySound(message.Sound);
      }

      if( Server && String.IsNullOrWhiteSpace(message.Sender) ) {
        ParseMessage(message.Text,message);
      } else {
        MyAPIGateway.Utilities.ShowMessage( message.Sender, message.Text );
      }



		}

    public bool ParseMessage(string text, Message message = null) {
      MyCommandLine cmd = new MyCommandLine();
			if( !cmd.TryParse(text) ) return false;

      string action = cmd.Argument(1);

      if( !String.IsNullOrWhiteSpace(action) && Actions.ContainsKey(action) ) {
        Actions[action](cmd, message);
        return true;
      }
      Respond("Error","Unknown command " + cmd.Argument(1) );
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

    public static bool SendMessageToClient(Message message) {
      if( message == null || message.SteamUserId == 0 ) return false;
      byte[] data = MyAPIGateway.Utilities.SerializeToBinary<Message>(message);
      return MyAPIGateway.Multiplayer.SendMessageTo(Id, data, message.SteamUserId);
    }

    public static bool SendMessageToAll(Message message) {
      if( message == null ) return false;
      List<IMyPlayer> players = new List<IMyPlayer>();
			MyAPIGateway.Players.GetPlayers(players);

			foreach(IMyPlayer player in players) {
        message.SteamUserId = player.SteamUserId;
        message.PlayerID = player.PlayerID;
        SendMessageToClient(message);
      }
      return true;
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

    public void PlaySound( string sound ) {
      if( SoundEmitter != null ) {
        SoundEmitter.StopSound(true);
      }
      IMyEntity ent;

      try {
        ent = MyAPIGateway.Session.Player.Controller?.ControlledEntity?.Entity;
      } catch( Exception e ) {
        return;
      }
      MyEntity ment = ent as MyEntity;
      if( ent == null || ment == null ) return;
      try {
        SoundEmitter = new MyEntity3DSoundEmitter(ment);
        SoundEmitter.StopSound(true);
        SoundEmitter.PlaySingleSound( new MySoundPair(sound) );
      } catch( Exception e ) {
        return;
      }

      // MyVisualScriptLogicProvider.MusicPlayMusicCue(sound);
      // IMyEntity entity = MyAPIGateway.Session.ControlledObject as IMyEntity;
      // MyVisualScriptLogicProvider.SetName(entity.EntityId, entity.EntityId.ToString());
      // MyVisualScriptLogicProvider.PlaySingleSoundAtEntity(sound, entity.EntityId.ToString());
      // MyVisualScriptLogicProvider.PlaySingleSoundAtPosition(sound,MyAPIGateway.Session.LocalHumanPlayer.GetPosition());

    }

    public void Respawn( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      // if( message != null ) {
      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error", "You do not have permission", message);
        return;
      }
      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(2), message);
        return;
      }

      MyPlanet planet = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? null : SpaceCraftSession.GetPlanet(cmd.Argument(3));

      faction.Mulligan("User chat command", !cmd.Switch("remain"), planet );

      // Respond("Respawn", "Attempted to respawn " + faction.Name, message);
    }

    public void Help( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      if( String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
        foreach( string action in Descriptions.Keys ) {
          Respond(action, Descriptions[action], message);
        }
        Respond("Help", "Type \"/sc ? command\" for help with a specific command", message);
        Respond("Command Generator", "https://archmage.co/sccmd", message);
      } else if(Descriptions.ContainsKey(cmd.Argument(2).ToLower())) {
        Respond(cmd.Argument(2), Descriptions[cmd.Argument(2).ToLower()], message);
      }

    }

    public void Establish( MyCommandLine cmd, Message message ) {

      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      Faction scFaction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( scFaction != null ) {
        Respond("Error", "You already belong to a SpaceCraft faction", message);
        return;
      }
      //Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      IMyFaction faction = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID) : MyAPIGateway.Session.Factions.TryGetFactionByTag(cmd.Argument(2).ToUpper());
      if( faction == null ) {
        Respond("Error", "Faction not found", message);
        return;
      }

      string command = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? "-spawned " : "";
      foreach(string s in cmd.Switches) {
        if( s != "nodonate")
        command += "-" + s + " ";
      }

      scFaction = Factions.Static.Establish(faction.Tag,command);
      if( scFaction == null ) {
        Respond("Error", "Failed to establish faction", message);
        return;
      }

      if( !cmd.Switch("nodonate") ) {

        Dictionary<IMyCubeGrid,List<IMyCubeGrid>> subgrids = new Dictionary<IMyCubeGrid,List<IMyCubeGrid>>();
        HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
  			MyAPIGateway.Entities.GetEntities(entities);

        foreach( IMyEntity entity in entities ) {
          IMyCubeGrid grid = entity as IMyCubeGrid;
          if( grid == null ) continue;
          List<long> owners = grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners : grid.SmallOwners;
          // if( !owners.Contains(player.PlayerID) ) continue;
          if( !owners.Contains(faction.FounderId) ) continue;

          IMyCubeGrid parent = SpaceCraftSession.GetParentGrid(grid);

          if( parent == null ) {
            CubeGrid g = new CubeGrid(grid);
            g.FindSubgrids();
            // g.CheckFlags();
            scFaction.TakeControl( g );
            if( scFaction.MainBase == null ) {
              scFaction.MainBase = g;
            } else {
              scFaction.MainBase.ToggleDocked(g);
            }
          } else {
            if(!subgrids.ContainsKey(parent))
              subgrids[parent] = new List<IMyCubeGrid>();

            subgrids[parent].Add(grid);
          }

        }

        foreach( IMyCubeGrid grid in subgrids.Keys ) {
          CubeGrid g = scFaction.GetControllable(grid) as CubeGrid;
          if( g == null ) continue;
          foreach( IMyCubeGrid cg in subgrids[grid] ) {
            g.Subgrids.Add(cg);
          }
          g.CheckFlags();
        }
      }

      scFaction.DetermineTechTier();

      if( scFaction.Tier != Tech.Primitive )
        scFaction.DetermineNextGoal();


      Respond("Established", "Your faction has now been established with SpaceCraft", message);

    }

    public void Dissolve( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      string tag = cmd.Argument(2).ToUpper();
      Faction faction = SpaceCraftSession.GetFaction(tag);
      if( faction != null ) {
        Respond("Error", "Faction " + tag + " not found", message);
        return;
      }

      if( !faction.Established ) {
        Respond("Error", "Faction is not an established faction. The faction's mod must be removed from the game settings.", message);
        return;
      }

      if( !Factions.Static.Dissolve(tag) ) {
        Respond("Error", "Could not dissolve faction", message);
        return;
      }

      Respond("Dissolved", tag + " has been dissolved", message);

    }

    public void Donate( MyCommandLine cmd, Message message ) {

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

      HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities);

			foreach( IMyEntity entity in entities ) {
        IMyPlayer controller = MyAPIGateway.Players.GetPlayerControllingEntity(entity);
        if( controller == null || player.PlayerID != controller.PlayerID ) continue;
        IMyCubeGrid grid = entity as IMyCubeGrid;
        if( grid == null ) continue;
        grid.DisplayName = faction.Name + " " + grid.DisplayName;
        CubeGrid g = new CubeGrid(grid);
        g.FindSubgrids();
        g.CheckFlags();
        faction.TakeControl( g );
        if( faction.MainBase != null ) {
          faction.MainBase.ToggleDocked(g);
        }
        Respond("Donated", "Your cube grid has been donated to the cause", message);

        return;
      }

      Respond("Error", "Not controlling a cube grid", message);

    }


    public void GPS( MyCommandLine cmd, Message message ) {
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

      foreach( Controllable c in faction.Controlled ) {
        //Vector3D position = c.Entity.WorldMatrix.Translation;
        IMyGps gps = MyAPIGateway.Session.GPS.Create(c.Entity.DisplayName, c.Entity.DisplayName, c.Entity.WorldMatrix.Translation, true, false);
        MyAPIGateway.Session.GPS.AddGps(player.PlayerID, gps);
        //Respond("GPS", "GPS:" + c.Entity.DisplayName + ":" + position.X.ToString() + ":" + position.Y.ToString() + ":" + position.Z.ToString(), message);
      }

    }

    public void Reset( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Quests.Reset();

      if( Convars.Static.Quests ) {
        Quests.LockTechnology();
      }
      Respond("Reset", "Quest progress has been reset", message);
    }

    public void Lock( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Quests.LockTechnology( Quests.Technology[Races.Protoss] );
      Respond("Lock", "Technology has been locked", message);
    }

    public void Unlock( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Quests.UnlockTechnology( Quests.Technology[Races.Protoss] );
      Respond("Unlock", "Technology has been unlocked", message);
    }

    public void Debug( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Convars.Static.Debug = !Convars.Static.Debug;
      Respond("Debug", Convars.Static.Debug ? "On" : "Off", message);
    }

    public void Set( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      // if( message != null ) {
      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
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

    /*public void Attack( MyCommandLine cmd, Message message ) {
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

    }*/

    public void Build( MyCommandLine cmd, Message message ) {
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

      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

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
      CubeGrid grid = new CubeGrid( CubeGrid.Spawn(prefab, faction.GetPlacementLocation(prefab, Vector3D.Zero), faction) );

      if( grid == null && grid.Grid == null ) {
          Respond("Error", "Failed to spawn " + cmd.Argument(2), message);
          return;
      }



      List<IMySlimBlock> suspensions = grid.GetBlocks<IMyMotorSuspension>();
      long owner = faction.MyFaction == null ? (long)0 : faction.MyFaction.FounderId;
      foreach(IMySlimBlock block in suspensions) {
        IMyMotorSuspension suspension = block.FatBlock as IMyMotorSuspension;
        Block.DoAction(suspension, "Add Wheel");
        if( suspension.RotorGrid != null ) {
          suspension.RotorGrid.DisplayName = faction.Name + " Subgrid";
          suspension.RotorGrid.ChangeGridOwnership(owner, MyOwnershipShareModeEnum.Faction);
          grid.Subgrids.Add(suspension.RotorGrid);

          IMySlimBlock wheel = suspension.RotorGrid.GetCubeBlock(Vector3I.Zero) as IMySlimBlock;
          if( wheel == null ) continue;
          wheel.SpawnConstructionStockpile();
          wheel.IncreaseMountLevel(100f,owner);
        }
      }

      grid.Grid.ChangeGridOwnership(owner, MyOwnershipShareModeEnum.Faction);
      grid.FindSubgrids();
      grid.CheckFlags();


      List<IMySlimBlock> blocks = grid.GetBlocks<IMySlimBlock>();
      foreach(IMySlimBlock slim in blocks ) {
        faction.BlockCompleted(slim);
      }

      faction.TakeControl( grid );
      if( faction.MainBase != null )
        faction.MainBase.ToggleDocked(grid);

      Respond("Spawning", cmd.Argument(2), message);
    }

    public void Complete( MyCommandLine cmd, Message message ) {

      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(2), message);
        return;
      }

      CubeGrid grid = faction.CurrentGoal.Entity as CubeGrid;

      if( grid == null ) {
        Respond("Error", "Your faction is not constructing anything", message);
        return;
      }


      long owner = faction.MyFaction == null ? (long)0 : faction.MyFaction.FounderId;
      List<IMySlimBlock> blocks = grid.GetBlocks<IMySlimBlock>();
      foreach(IMySlimBlock block in blocks) {
        if( block.IsFullIntegrity ) continue;
        //block.SetToConstructionSite();
        //block.SpawnFirstItemInConstructionStockpile();
        block.ClearConstructionStockpile(null);
        block.SpawnConstructionStockpile();
        block.IncreaseMountLevel(100f,owner,null,0f,true,MyOwnershipShareModeEnum.Faction);
        faction.BlockCompleted(block);
      }
      grid.Grid.ChangeGridOwnership(owner, MyOwnershipShareModeEnum.Faction);
      foreach( IMyCubeGrid g in grid.Subgrids ) {
        g.ChangeGridOwnership(owner, MyOwnershipShareModeEnum.Faction);
      }
      grid.ConstructionSite = null;
      grid.CheckFlags();
      if( faction.MainBase != null )
        foreach( CubeGrid g in faction.MainBase.Docked ) {
          if( g == null ) continue;
          if( g.CurrentOrder != null )
            g.CurrentOrder.Complete();
        }
      //grid.ConstructionSite = null;

      Respond("Completed", grid.Grid.DisplayName, message);
    }

    // Pays off a battery balance if applicable
    public void Pay( MyCommandLine cmd, Message message ) {

      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      if( player == null || (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner) ) {
        Respond("Error","You don't have permission", message);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(2)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(2), message);
        return;
      }

      foreach( Controllable c in faction.Controlled ) {
        CubeGrid grid = c as CubeGrid;
        if( grid != null )
          grid.Balance = null;
      }

      faction.CurrentGoal.Balance = null;

      Respond("Pay", "Paid off all balances", message);
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

      float distance = 0f;
      if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
        Single.TryParse(cmd.Argument(2), out distance);
      }

      faction.FollowDistance = distance;
      faction.Follow(player);
      Respond("Following", player.DisplayName, message);
    }

    public void Stop( MyCommandLine cmd, Message message ) {
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

      faction.FollowDistance = 0f;
      faction.Follow(null);
      Respond("Stopped", "following", message);
    }

    public void Join( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( faction == null || faction.MyFaction == null ) {
        Respond("Error", cmd.Argument(2) + " faction could not be found", message);
        return;
      }

      IMyFaction current = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);
      if( current != null )
        MyAPIGateway.Session.Factions.KickPlayerFromFaction(player.PlayerID);

      if( message == null ) // Force join (I think)
        MyAPIGateway.Session.Factions.AddPlayerToFaction(player.PlayerID,faction.MyFaction.FactionId);
      else {
        MyAPIGateway.Session.Factions.SendJoinRequest(faction.MyFaction.FactionId, player.PlayerID);
        MyAPIGateway.Session.Factions.AcceptJoin(faction.MyFaction.FactionId, player.PlayerID);
      }

      current = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);
      Respond("Join", current == faction.MyFaction ? "Joined faction " + cmd.Argument(2) : "Failed to join faction", message);
    }

    public void War( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      // Faction faction = player == null ? SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper()) : SpaceCraftSession.GetFactionContaining(player.PlayerID);
      //Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) && player != null ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      IMyFaction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) && player != null ? MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID) : MyAPIGateway.Session.Factions.TryGetFactionByTag(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }

      //Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      IMyFaction target = MyAPIGateway.Session.Factions.TryGetFactionByTag(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(2), message);
        return;
      }

      MyAPIGateway.Session.Factions.DeclareWar(faction.FactionId, target.FactionId);
      // MyAPIGateway.Session.Factions.DeclareWar(faction.MyFaction.FactionId, target.MyFaction.FactionId);

      // Respond("War", "were declared on " + target.Name, message);
      Respond("War", "were declared on " + target.Name, message);
    }

    public void Peace( MyCommandLine cmd, Message message ) {

      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);

      // Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) && player != null ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      IMyFaction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) && player != null ? MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID) : MyAPIGateway.Session.Factions.TryGetFactionByTag(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }
      // Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      IMyFaction target = MyAPIGateway.Session.Factions.TryGetFactionByTag(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(2), message);
        return;
      }

      // faction.SetReputation( target.MyFaction.FounderId, 1500 );
      // target.SetReputation( faction.MyFaction.FounderId, 1500 );
      MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(target.FounderId, faction.FactionId, 1500);
      MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(faction.FounderId, target.FactionId, 1500);

      MyAPIGateway.Session.Factions.SendPeaceRequest(faction.FactionId, target.FactionId);
      MyAPIGateway.Session.Factions.AcceptPeace(faction.FactionId, target.FactionId);
      // MyAPIGateway.Session.Factions.SendPeaceRequest(faction.MyFaction.FactionId, target.MyFaction.FactionId);
      // MyAPIGateway.Session.Factions.AcceptPeace(faction.MyFaction.FactionId, target.MyFaction.FactionId);


    }

    public void Control( MyCommandLine cmd, Message message ) {
      if( MyAPIGateway.Session.LocalHumanPlayer != SpaceCraftSession.GetPlayer(message.PlayerID) ) {
        Respond("Error", "You do not have permission", message);
        return; // Host/API Only
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      long id;
      if( !Int64.TryParse(cmd.Argument(2), out id) ) return;

      IMyEntity entity = null;
      if( !MyAPIGateway.Entities.TryGetEntityById(id, out entity) ) {
        Respond("Error", "Entity " + id.ToString() + " not found", null);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }

      if( entity is IMyCubeGrid ) {
        CubeGrid grid = new CubeGrid(entity as IMyCubeGrid);
        grid.FindSubgrids();
        grid.CheckFlags();
        faction.TakeControl( grid );
        if( faction.MainBase != null )
          faction.MainBase.ToggleDocked(grid);
        return;
      } else if( entity is IMyCharacter ) {
        faction.TakeControl( new Engineer(faction, entity as IMyCharacter) );
        return;
      }

      Respond("Error", "Could not take control of " + entity.DisplayName, message);

    }

    public void Release( MyCommandLine cmd, Message message ) {
      if( MyAPIGateway.Session.LocalHumanPlayer != SpaceCraftSession.GetPlayer(message.PlayerID) ) {
        Respond("Error", "You do not have permission", message);
        return; // Host/API Only
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      long id;
      if( !Int64.TryParse(cmd.Argument(2), out id) ) return;

      IMyEntity entity = null;
      if( !MyAPIGateway.Entities.TryGetEntityById(id, out entity) ) {
        Respond("Error", "Entity " + id.ToString() + " not found", null);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }

      Controllable removed = faction.ReleaseControl(entity);
      CubeGrid grid = removed as CubeGrid;
      if( grid != null && grid.DockedTo != null )
        grid.DockedTo.ToggleDocked(grid);

      if( removed == null )
        Respond("Error", "Could not release control of " + entity.DisplayName, message);

    }



  }

}
