using System;
using System.IO;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using SpaceCraft;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace SpaceCraft.Utils {

  public class EstablishedFaction {
    public string Tag;
    public string Command;
    public string Prefab;
  }

  public sealed class Factions {

    private static Factions instance;

    public static Factions Static
    {
      get {
        if( instance == null ) {
          instance = new Factions();
          if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Factions)) ) {
            instance = Open() ?? new Factions();
          } else {
            instance = new Factions();
          }
        }
        return instance;
      }
    }

    public List<EstablishedFaction> Established = new List<EstablishedFaction>();

    protected static string File = "SCFactions.xml";

    public Faction Establish( string tag, string command, string startingPrefab = "" ) {

      MyCommandLine cmd = new MyCommandLine();

      if( !cmd.TryParse("SpaceCraft F " + command) ) return null;

      Established.Add( new EstablishedFaction{
        Tag = tag,
        Command = command,
        Prefab = startingPrefab
      } );
      Save();


      Faction faction = SpaceCraftSession.CreateIfNotExists( tag, cmd );

      return faction;
    }

    public bool Dissolve( string tag ) {
      foreach( EstablishedFaction faction in Established ) {
        if( faction.Tag == tag ) {
          Established.Remove(faction);
          Save();
          return SpaceCraftSession.RemoveFaction(tag);
        }
      }

      return false;
    }

    private static Factions Open() {
      try {
        TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(File, typeof(Factions));
        return MyAPIGateway.Utilities.SerializeFromXML<Factions>(reader.ReadToEnd());
      }catch(Exception e){
        return null;
      }
      return null;
    }

    public bool Save() {
      try{
        if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Factions)) )
          MyAPIGateway.Utilities.DeleteFileInWorldStorage(File,typeof(Factions));
				TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(File, typeof(Factions));
				using (writer){

					writer.Write(MyAPIGateway.Utilities.SerializeToXML<Factions>(this));

				}

			}catch(Exception exc){
				return false;

			}

      return true;
    }


  }

}
