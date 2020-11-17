using System;
using System.IO;
using VRage.Game.ModAPI;

namespace SpaceCraft.Utils {

  public sealed class Convars {

    private static Convars instance;

    public static Convars Static
    {
      get {
        if( instance == null ) {
          instance = new Convars();
          if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Convars)) ) {
            //instance.Spawned = true;
            instance = Open() ?? new Convars();
            instance.Spawned = true;
          } else {
            instance = new Convars();
          }
        }
        return instance;
      }
    }

    public float Difficulty = 1f;
    public int Grids = 20;
    public bool Debug = false;
    public int Engineers = 1;
    public bool Spawned = false;

    protected static string File = "SCConvars.xml";

    // private Convars(){
    //   bool spawned;
    //   if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Convars)) ) {
    //     instance = Open() ?? new Convars();
    //   }/* else {
    //     Save();
    //   }*/
    //   // MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out spawned);
    //   // if( !spawned ) {
  	// 	// 	MyAPIGateway.Utilities.GetVariable<int>("SC-Grids", out Grids);
  	// 	// 	MyAPIGateway.Utilities.GetVariable<float>("SC-Difficulty", out Difficulty);
  	// 	// 	MyAPIGateway.Utilities.GetVariable<int>("SC-Engineers", out Engineers);
  	// 	// 	MyAPIGateway.Utilities.GetVariable<bool>("SC-Debug", out Debug);
    //   // }
    // }

    private static Convars Open() {
      try {
        TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(File, typeof(Convars));
        return MyAPIGateway.Utilities.SerializeFromXML<Convars>(reader.ReadToEnd());
      }catch(Exception e){
        return null;
      }
      return null;
    }

    public bool Save() {
      try{
        if( MyAPIGateway.Utilities.FileExistsInWorldStorage(File,typeof(Convars)) )
          MyAPIGateway.Utilities.DeleteFileInWorldStorage(File,typeof(Convars));
				TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(File, typeof(Convars));
				using (writer){

					writer.Write(MyAPIGateway.Utilities.SerializeToXML<Convars>(this));

				}

			}catch(Exception exc){
				return false;

			}

      return true;
    }

    public string Set( string convar, string value ) {
      switch( convar.ToLower() ) {
        case "engineers":
          Int32.TryParse(value, out Engineers);
          Save();
          return Engineers.ToString();
        case "grids":
          Int32.TryParse(value, out Grids);
          Save();
          return Grids.ToString();
        case "difficulty":
          float.TryParse(value, out Difficulty);
          Save();
          return Difficulty.ToString();
      }

      return "null";
    }

    public string Get( string convar ) {
      switch( convar.ToLower() ) {
        case "engineers": return Engineers.ToString();
        case "grids": return Grids.ToString();
        case "difficulty": return Difficulty.ToString();
      }
      return "null";
    }


  }

}